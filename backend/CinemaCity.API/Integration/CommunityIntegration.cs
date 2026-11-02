using System.Security.Claims;
using System.Text.Json;
using CinemaCity.Infrastructure.Data;
using CinemaCity.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Integration;

public static class CommunityIntegration
{
    public static IServiceCollection AddCommunityIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ProjectionRefreshState>();
        services.AddScoped<ProjectionRefresher>();
        services.AddHttpClient("AuthOwner", client => { client.BaseAddress = new Uri(configuration["Services:AuthUrl"] ?? throw new InvalidOperationException("Configure Services:AuthUrl.")); client.Timeout = TimeSpan.FromSeconds(15); });
        services.AddHttpClient("CatalogOwner", client => { client.BaseAddress = new Uri(configuration["Services:CatalogUrl"] ?? throw new InvalidOperationException("Configure Services:CatalogUrl.")); client.Timeout = TimeSpan.FromSeconds(30); });
        services.AddHostedService<StatisticsOutboxWorker>();
        return services;
    }

    public static void MapCommunityInternalEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/internal/community/series/{seriesId:guid}/ratings", async (Guid seriesId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var id = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ratings = await db.Ratings.AsNoTracking().Where(r => r.UserId == id && r.EpisodeId != null && r.Episode!.Season.SeriesId == seriesId)
                .Select(r => new { EpisodeId = r.EpisodeId!.Value, r.Id, r.Score }).ToListAsync(ct);
            return Results.Ok(ratings.ToDictionary(r => r.EpisodeId, r => new { r.Id, r.Score }));
        }).RequireAuthorization().ExcludeFromDescription();
        routes.MapPost("/internal/community/rated-episodes", async (RatedEpisodeRequest request, HttpContext context, IConfiguration configuration, AppDbContext db, CancellationToken ct) =>
        {
            if (!global::CinemaCity.ServiceDefaults.ServiceDefaults.IsInternalRequest(context, configuration)) return Results.Unauthorized() as IResult;
            if (request.Ids.Length > 10000) return Results.BadRequest();
            return Results.Ok(await db.Ratings.AsNoTracking().Where(r => r.EpisodeId != null && request.Ids.Contains(r.EpisodeId.Value)).Select(r => r.EpisodeId!.Value).Distinct().ToArrayAsync(ct));
        }).ExcludeFromDescription();
    }
}

public sealed record RatedEpisodeRequest(Guid[] Ids);

public sealed class ReferenceProjectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ProjectionRefresher refresher)
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var ct = context.RequestAborted;
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) await refresher.RefreshUserAsync(userId, ct);
        var segments = context.Request.Path.Value!.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 2 && segments[1] == "users" && Guid.TryParse(segments[2], out var profileId) && profileId != userId) await refresher.RefreshUserAsync(profileId, ct);
        Guid? movieId = null, episodeId = null;
        for (var index = 0; index < segments.Length - 1; index++)
            if (segments[index] == "movies" && Guid.TryParse(segments[index + 1], out var id)) movieId = id;
        // Chunked HTTP requests have no Content-Length. They still need the same owner checks
        // as browser requests; otherwise a cached reference could accept a deleted episode.
        // Kestrel's normal request-body limit remains in effect while the buffered body is read.
        if (context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true && context.Request.ContentLength != 0)
        {
            context.Request.EnableBuffering();
            try
            {
                using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: ct);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (document.RootElement.TryGetProperty("movieId", out var movie) && movie.ValueKind == JsonValueKind.String && movie.TryGetGuid(out var id)) movieId = id;
                    if (document.RootElement.TryGetProperty("episodeId", out var episode) && episode.ValueKind == JsonValueKind.String && episode.TryGetGuid(out var eid)) episodeId = eid;
                }
            }
            catch (JsonException) { /* The normal HTTP binding reports malformed JSON as 400. */ }
            finally { context.Request.Body.Position = 0; }
        }
        await refresher.RefreshCatalogAsync(movieId, episodeId, ct);
        await next(context);
    }
}
