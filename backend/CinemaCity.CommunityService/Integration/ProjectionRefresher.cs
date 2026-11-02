using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CinemaCity.API.Integration;

/// <summary>Serializes periodic full refreshes without sharing a DbContext between requests.</summary>
public sealed class ProjectionRefreshState
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public DateTimeOffset CatalogRefreshedAt { get; set; }
}

/// <summary>Maintains local reference snapshots for the original community queries and foreign keys.</summary>
public sealed class ProjectionRefresher(AppDbContext db, IHttpClientFactory clients, IConfiguration configuration, ProjectionRefreshState state, IHttpContextAccessor accessor)
{
    public async Task RefreshCatalogAsync(Guid? movieId, Guid? episodeId, CancellationToken ct)
    {
        if (movieId is not null || episodeId is not null)
        {
            await state.Gate.WaitAsync(ct);
            try { await ReadCatalogPageAsync(1, movieId, episodeId, ct); }
            finally { state.Gate.Release(); }
            return;
        }
        if (DateTimeOffset.UtcNow - state.CatalogRefreshedAt < TimeSpan.FromSeconds(5)) return;
        await state.Gate.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow - state.CatalogRefreshedAt < TimeSpan.FromSeconds(5)) return;
            var page = 1;
            while (await ReadCatalogPageAsync(page++, null, null, ct)) { }
            state.CatalogRefreshedAt = DateTimeOffset.UtcNow;
        }
        finally { state.Gate.Release(); }
    }

    public async Task RefreshUserAsync(Guid id, CancellationToken ct)
    {
        var client = clients.CreateClient("AuthOwner");
        using var request = InternalRequest(HttpMethod.Get, $"/internal/users/{id}");
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return;
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<UserSnapshot>(ct);
        if (data is null) return;
        // Parameterized upsert prevents races between concurrent requests for a newly registered account.
        var role = data.Role;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO projections.users (id, username, email, avatar_url, role, failed_login_attempts, created_at, updated_at)
            VALUES ({id}, {data.Username}, {data.Email}, {data.AvatarUrl}, {role}, 0, {data.CreatedAt}, {data.UpdatedAt})
            ON CONFLICT (id) DO UPDATE SET username=EXCLUDED.username, email=EXCLUDED.email, avatar_url=EXCLUDED.avatar_url, role=EXCLUDED.role, updated_at=EXCLUDED.updated_at
            """, ct);
    }

    private async Task<bool> ReadCatalogPageAsync(int page, Guid? movieId, Guid? episodeId, CancellationToken ct)
    {
        var path = $"/internal/catalog/snapshot?page={page}&pageSize=100";
        if (movieId is not null) path += $"&movieId={movieId}";
        if (episodeId is not null) path += $"&episodeId={episodeId}";
        using var request = InternalRequest(HttpMethod.Get, path);
        using var response = await clients.CreateClient("CatalogOwner").SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<SnapshotEnvelope>(ct) ?? throw new HttpRequestException("Catalog snapshot was empty.");
        if (envelope.Version != 1) throw new HttpRequestException("Unsupported catalog snapshot version.");
        var data = envelope.Data;
        if (data.GetProperty("movies").GetArrayLength() == 0)
        {
            // Historical local references may remain after a hard deletion in Catalog.
            // A new command must not treat that historical copy as proof the resource still exists.
            if (movieId is not null || episodeId is not null) throw new KeyNotFoundException("The catalog resource no longer exists.");
            return false;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // The table and column identifiers below come from a fixed allow-list and our own schema metadata.
        foreach (var table in new[] { "genres", "countries", "languages", "persons", "movies", "seasons", "episodes", "movie_cast", "movie_genres", "movie_countries", "movie_languages" })
        {
            if (table is "movie_cast" or "movie_genres" or "movie_countries" or "movie_languages")
            {
                var movieIds = data.GetProperty("movies").EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).ToArray();
                var deleteSql = table switch
                {
                    "movie_cast" => "DELETE FROM projections.movie_cast WHERE movie_id = ANY ({0})",
                    "movie_genres" => "DELETE FROM projections.movie_genres WHERE movie_id = ANY ({0})",
                    "movie_countries" => "DELETE FROM projections.movie_countries WHERE movie_id = ANY ({0})",
                    _ => "DELETE FROM projections.movie_languages WHERE movie_id = ANY ({0})"
                };
                await db.Database.ExecuteSqlRawAsync(deleteSql, [movieIds], ct);
            }
            var rows = data.GetProperty(table);
            if (rows.GetArrayLength() == 0) continue;
            await UpsertAsync(table, rows.GetRawText(), ct);
        }
        await transaction.CommitAsync(ct);
        return movieId is null && episodeId is null && page * envelope.PageSize < envelope.TotalCount;
    }

    private async Task UpsertAsync(string table, string rows, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema='projections' AND table_name=@table ORDER BY ordinal_position";
        var nameParameter = command.CreateParameter(); nameParameter.ParameterName = "table"; nameParameter.Value = table; command.Parameters.Add(nameParameter);
        var columns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(0));
        var join = table is "movie_genres" or "movie_countries" or "movie_languages";
        var conflict = join ? "DO NOTHING" : "DO UPDATE SET " + string.Join(",", columns.Where(c => c != "id").Select(c => $"\"{c}\"=EXCLUDED.\"{c}\""));
        var keys = join ? "" : "(id)";
        command.Parameters.Clear();
        command.CommandText = $"INSERT INTO projections.\"{table}\" SELECT * FROM jsonb_populate_recordset(NULL::projections.\"{table}\", CAST(@rows AS jsonb)) ON CONFLICT {keys} {conflict}";
        var parameter = command.CreateParameter(); parameter.ParameterName = "rows"; parameter.Value = rows; command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(ct);
    }

    private HttpRequestMessage InternalRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Service-Key", configuration["Services:InternalKey"]);
        if (accessor.HttpContext is { } context) request.Headers.TryAddWithoutValidation("X-Correlation-Id", context.TraceIdentifier);
        return request;
    }

    private sealed record SnapshotEnvelope(int Version, int Page, int PageSize, int TotalCount, JsonElement Data);
    private sealed record UserSnapshot(Guid Id, string Username, string Email, string? AvatarUrl, string Role, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
}
