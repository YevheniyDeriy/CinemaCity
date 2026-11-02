using CinemaCity.CatalogService.Services;

namespace CinemaCity.CatalogService.Endpoints;

public static class CatalogInternalEndpoints
{
    public static void MapCatalogInternalEndpoints(this IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/internal/catalog").ExcludeFromDescription();
        api.AddEndpointFilter(async (context, next) =>
            global::CinemaCity.ServiceDefaults.ServiceDefaults.IsInternalRequest(context.HttpContext, context.HttpContext.RequestServices.GetRequiredService<IConfiguration>())
                ? await next(context) : Results.Unauthorized());
        api.MapGet("/snapshot", (CatalogSnapshotService service, int page = 1, int pageSize = 100, Guid? movieId = null, Guid? episodeId = null, CancellationToken ct = default) => service.ReadAsync(page, pageSize, movieId, episodeId, ct));
        api.MapPut("/statistics", (CatalogStatisticsRequest request, CatalogSnapshotService service, CancellationToken ct) => service.SetStatisticsAsync(request, ct));
    }
}
