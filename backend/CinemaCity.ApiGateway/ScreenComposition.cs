using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace CinemaCity.ApiGateway;

public sealed record DependencyResult(int Status, JsonElement? Data, string State);
public sealed record ScreenMovie(Guid Id, string Title, int ReleaseYear, string? PosterUrl);
public sealed record ScreenList(Guid Id, string Name);
public sealed record ScreenUserState(string? WatchlistStatus, decimal? MyScore, IReadOnlyList<ScreenList> Lists);
public sealed record MovieScreen(ScreenMovie Movie, ScreenUserState? MyState, bool Partial, string CommunityStatus);

public interface IScreenComposer
{
    bool Matches(PathString path, out Guid id);
    Task<IResult> ComposeAsync(Guid id, HttpContext context, CancellationToken ct);
}

public sealed class CatalogCache(IMemoryCache cache, IConfiguration config)
{
    private long generation, hits, misses;
    public object Metrics => new { hits = Interlocked.Read(ref hits), misses = Interlocked.Read(ref misses) };
    public void Invalidate() => Interlocked.Increment(ref generation);
    public async Task<DependencyResult> GetAsync(string path, Func<Task<DependencyResult>> fetch)
    {
        var key = $"catalog:{Interlocked.Read(ref generation)}:{path}";
        if (cache.TryGetValue(key, out DependencyResult? found)) { Interlocked.Increment(ref hits); return found!; }
        Interlocked.Increment(ref misses);
        var result = await fetch();
        if (result.Status == 200) cache.Set(key, result, TimeSpan.FromSeconds(config.GetValue("Gateway:CacheTtlSeconds", 60)));
        return result;
    }
}

public sealed class ScreenClients(IHttpClientFactory factory, CatalogCache cache)
{
    public Task<DependencyResult> CatalogAsync(Guid id, HttpContext context, CancellationToken ct) =>
        cache.GetAsync($"/api/v1/catalog/items/{id}", () => GetAsync("Catalog", $"/api/v1/catalog/items/{id}", context, false, ct));

    public async Task<DependencyResult> GetAsync(string service, string path, HttpContext context, bool personal, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", context.TraceIdentifier);
            if (personal) request.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers.Authorization.ToString());
            using var response = await factory.CreateClient(service).SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                JsonElement? problem = null;
                try { problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct); } catch (JsonException) { }
                return new((int)response.StatusCode, problem, response.StatusCode == HttpStatusCode.NotFound ? "notFound" : "unavailable");
            }
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return new((int)response.StatusCode, data, "available");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(504, null, "timeout"); }
        catch (HttpRequestException) { return new(503, null, "unavailable"); }
        catch (JsonException) { return new(502, null, "invalidResponse"); }
    }
}

public sealed class MovieScreenComposer(ScreenClients clients) : IScreenComposer
{
    public bool Matches(PathString path, out Guid id)
    {
        id = default;
        return path.StartsWithSegments("/api/v1/screens/movies", out var remaining) && Guid.TryParse(remaining.Value?.Trim('/'), out id);
    }
    public async Task<IResult> ComposeAsync(Guid id, HttpContext context, CancellationToken ct)
    {
        var movieTask = clients.CatalogAsync(id, context, ct);
        var stateTask = clients.GetAsync("Community", $"/api/profile/movies/{id}/state", context, true, ct);
        await Task.WhenAll(movieTask, stateTask);
        var movie = await movieTask; var state = await stateTask;
        if (movie.Status != 200 || movie.Data is null)
            return Results.Problem(statusCode: movie.Status == 404 ? 404 : movie.Status == 504 ? 504 : 503,
                title: movie.Status == 404 ? "Movie not found" : "Catalog is unavailable", extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier });
        var data = movie.Data.Value;
        var dto = new ScreenMovie(data.GetProperty("id").GetGuid(), data.GetProperty("title").GetString()!, data.GetProperty("releaseYear").GetInt32(), data.TryGetProperty("posterUrl", out var poster) && poster.ValueKind == JsonValueKind.String ? poster.GetString() : null);
        context.Response.Headers.CacheControl = "no-store";
        ScreenUserState? personal = null;
        if (state.Status == 200 && state.Data is JsonElement raw)
        {
            var watchlist = raw.TryGetProperty("watchlistStatus", out var watch) && watch.ValueKind == JsonValueKind.String ? watch.GetString() : null;
            decimal? score = raw.TryGetProperty("rating", out var rating) && rating.ValueKind == JsonValueKind.Object && rating.TryGetProperty("score", out var value) ? value.GetDecimal() : null;
            var lists = raw.TryGetProperty("lists", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Select(item => new ScreenList(item.GetProperty("id").GetGuid(), item.GetProperty("name").GetString()!)).ToArray() : [];
            personal = new ScreenUserState(watchlist, score, lists);
        }
        return Results.Ok(new MovieScreen(dto, personal, state.Status != 200, state.State));
    }
}
