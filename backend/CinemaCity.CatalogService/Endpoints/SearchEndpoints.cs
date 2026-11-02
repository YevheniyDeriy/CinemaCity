using CinemaCity.ServiceDefaults;
using CinemaCity.CatalogService.Services;
namespace CinemaCity.CatalogService.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/catalog/search", (ICatalogSearchService service , string? q, string? scope = "all", int limit = 8, CancellationToken cancellationToken = default) => service.SearchAsync(q, scope, limit, cancellationToken))
            .WithName("GlobalSearch").WithSummary("Global Search")
            .WithTags("Search").Produces(200).Produces(400).Produces(404);

        return routes;
    }
}
