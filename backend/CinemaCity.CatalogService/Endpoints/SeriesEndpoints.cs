using System.Security.Claims;
using CinemaCity.ServiceDefaults;
using CinemaCity.CatalogService.Services;
namespace CinemaCity.CatalogService.Endpoints;

public static class SeriesEndpoints
{
    public static IEndpointRouteBuilder MapSeriesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/catalog/movies/{movieId:guid}/seasons", (ISeriesService service , Guid movieId, ClaimsPrincipal principal, CancellationToken cancellationToken) => service.GetSeasonsAsync(movieId, principal, cancellationToken))
            .WithName("GetSeriesSeasons").WithSummary("Get Series Seasons")
            .WithTags("Series").Produces(200).Produces(400).Produces(404);

        var admin = routes.MapGroup("/api/v1/admin")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin Series");

        admin.MapPost("/movies/{movieId:guid}/seasons", (ISeriesService service , Guid movieId, SeasonUpsertRequest request, CancellationToken cancellationToken) => service.CreateSeasonAsync(movieId, request, cancellationToken))
            .WithName("CreateSeason").WithSummary("Create Season").Produces(201).Produces(400).Produces(409);

        admin.MapPut("/seasons/{seasonId:guid}", (ISeriesService service , Guid seasonId, SeasonUpsertRequest request, CancellationToken cancellationToken) => service.UpdateSeasonAsync(seasonId, request, cancellationToken))
            .WithName("UpdateSeason").WithSummary("Update Season").Produces(204).Produces(400).Produces(404);

        admin.MapDelete("/seasons/{seasonId:guid}", (ISeriesService service , Guid seasonId, CancellationToken cancellationToken) => service.DeleteSeasonAsync(seasonId, cancellationToken))
            .WithName("DeleteSeason").WithSummary("Delete Season").Produces(204).Produces(400).Produces(404);

        admin.MapPost("/seasons/{seasonId:guid}/episodes", (ISeriesService service , Guid seasonId, EpisodeUpsertRequest request, CancellationToken cancellationToken) => service.CreateEpisodeAsync(seasonId, request, cancellationToken))
            .WithName("CreateEpisode").WithSummary("Create Episode").Produces(201).Produces(400).Produces(409);

        admin.MapPut("/episodes/{episodeId:guid}", (ISeriesService service , Guid episodeId, EpisodeUpsertRequest request, CancellationToken cancellationToken) => service.UpdateEpisodeAsync(episodeId, request, cancellationToken))
            .WithName("UpdateEpisode").WithSummary("Update Episode").Produces(204).Produces(400).Produces(404);

        admin.MapDelete("/episodes/{episodeId:guid}", (ISeriesService service , Guid episodeId, CancellationToken cancellationToken) => service.DeleteEpisodeAsync(episodeId, cancellationToken))
            .WithName("DeleteEpisode").WithSummary("Delete Episode").Produces(204).Produces(400).Produces(404);

        return routes;
    }
}
