using System.Data;
using System.Text.Json;
using CinemaCity.CatalogService.Data;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.CatalogService.Services;

/// <summary>Versioned scalar snapshots for community read models. This is an API, not shared database access.</summary>
public sealed class CatalogSnapshotService(CatalogDbContext db)
{
    public async Task<IResult> ReadAsync(int page, int pageSize, Guid? movieId, Guid? episodeId, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) return Results.BadRequest();
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH selected AS (SELECT * FROM movies WHERE (@movie IS NULL OR id = @movie)
                AND (@episode IS NULL OR id = (SELECT s.series_id FROM seasons s JOIN episodes e ON e.season_id=s.id WHERE e.id=@episode))
                ORDER BY id LIMIT @limit OFFSET @offset),
                credits AS (SELECT c.* FROM movie_cast c JOIN selected m ON m.id=c.movie_id),
                season_rows AS (SELECT s.* FROM seasons s JOIN selected m ON m.id=s.series_id)
            SELECT jsonb_build_object(
                'movies', COALESCE((SELECT jsonb_agg(to_jsonb(m)) FROM selected m),'[]'::jsonb),
                'genres', COALESCE((SELECT jsonb_agg(to_jsonb(g)) FROM genres g WHERE g.id IN (SELECT genre_id FROM movie_genres j JOIN selected m ON m.id=j.movie_id)),'[]'::jsonb),
                'countries', COALESCE((SELECT jsonb_agg(to_jsonb(g)) FROM countries g WHERE g.id IN (SELECT country_id FROM movie_countries j JOIN selected m ON m.id=j.movie_id)),'[]'::jsonb),
                'languages', COALESCE((SELECT jsonb_agg(to_jsonb(g)) FROM languages g WHERE g.id IN (SELECT language_id FROM movie_languages j JOIN selected m ON m.id=j.movie_id)),'[]'::jsonb),
                'persons', COALESCE((SELECT jsonb_agg(to_jsonb(p)) FROM persons p WHERE p.id IN (SELECT person_id FROM credits)),'[]'::jsonb),
                'movie_cast', COALESCE((SELECT jsonb_agg(to_jsonb(c)) FROM credits c),'[]'::jsonb),
                'seasons', COALESCE((SELECT jsonb_agg(to_jsonb(s)) FROM season_rows s),'[]'::jsonb),
                'episodes', COALESCE((SELECT jsonb_agg(to_jsonb(e)) FROM episodes e WHERE e.season_id IN (SELECT id FROM season_rows)),'[]'::jsonb),
                'movie_genres', COALESCE((SELECT jsonb_agg(to_jsonb(j)) FROM movie_genres j WHERE j.movie_id IN (SELECT id FROM selected)),'[]'::jsonb),
                'movie_countries', COALESCE((SELECT jsonb_agg(to_jsonb(j)) FROM movie_countries j WHERE j.movie_id IN (SELECT id FROM selected)),'[]'::jsonb),
                'movie_languages', COALESCE((SELECT jsonb_agg(to_jsonb(j)) FROM movie_languages j WHERE j.movie_id IN (SELECT id FROM selected)),'[]'::jsonb))
            """;
        Add("movie", movieId, DbType.Guid); Add("episode", episodeId, DbType.Guid); Add("limit", pageSize, DbType.Int32); Add("offset", (page - 1) * pageSize, DbType.Int32);
        var json = (string)(await command.ExecuteScalarAsync(ct))!;
        var totalCount = await db.Movies.CountAsync(ct);
        return Results.Ok(new CatalogSnapshotPage(1, page, pageSize, totalCount, JsonSerializer.Deserialize<JsonElement>(json)));

        void Add(string name, object? value, DbType type)
        { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; parameter.DbType = type; command.Parameters.Add(parameter); }
    }

    public async Task<IResult> SetStatisticsAsync(CatalogStatisticsRequest request, CancellationToken ct)
    {
        if (request.Version < 1 || request.RatingsCount < 0 || request.AverageRating is < 0 or > 10 || request.Kind is not ("Movie" or "Episode")) return Results.BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var version = await db.Set<CommunityStatisticsVersion>().SingleOrDefaultAsync(v => v.Kind == request.Kind && v.TargetId == request.Id, ct);
        if (version is not null && version.Version >= request.Version) return Results.NoContent();
        if (version is null)
        {
            version = new CommunityStatisticsVersion { Kind = request.Kind, TargetId = request.Id };
            db.Add(version);
        }
        version.Version = request.Version;
        if (request.Kind == "Movie")
            await db.Movies.Where(m => m.Id == request.Id).ExecuteUpdateAsync(update => update.SetProperty(m => m.AverageRating, request.AverageRating).SetProperty(m => m.RatingsCount, request.RatingsCount), ct);
        else if (request.Kind == "Episode")
            await db.Episodes.Where(e => e.Id == request.Id).ExecuteUpdateAsync(update => update.SetProperty(e => e.AverageRating, request.AverageRating).SetProperty(e => e.RatingsCount, request.RatingsCount), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }
}

public sealed record CatalogSnapshotPage(int Version, int Page, int PageSize, int TotalCount, JsonElement Data);
public sealed record CatalogStatisticsRequest(string Kind, Guid Id, decimal AverageRating, int RatingsCount, long Version);
