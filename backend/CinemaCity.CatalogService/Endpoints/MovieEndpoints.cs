using CinemaCity.ServiceDefaults;
using CinemaCity.CatalogService.Services;
namespace CinemaCity.CatalogService.Endpoints;

public static class MovieEndpoints
{
    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/catalog/genres", (ICatalogService service , CancellationToken cancellationToken) => service.GetGenresAsync(cancellationToken))
            .WithName("GetGenres").WithSummary("Get Genres").Produces(200).Produces(400).Produces(404);

        routes.MapGet("/api/v1/catalog/movies", (ICatalogService service , string? q, string? genre, int? yearFrom, int? yearTo, decimal? ratingMin, string? contentType, string? sort, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => service.GetMoviesAsync(q, genre, yearFrom, yearTo, ratingMin, contentType, sort, page, pageSize, cancellationToken))
            .WithName("GetMovies").WithSummary("Get Movies").Produces(200).Produces(400).Produces(404);

        routes.MapGet("/api/v1/catalog/movies/search", (ICatalogService service , string? q, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => service.SearchMoviesAsync(q, page, pageSize, cancellationToken))
            .WithName("SearchMovies").WithSummary("Search Movies").Produces(200).Produces(400).Produces(404);

        routes.MapGet("/api/v1/catalog/movies/{id:guid}", (ICatalogService service , Guid id, CancellationToken cancellationToken) => service.GetMovieAsync(id, cancellationToken))
            .WithName("GetMovie").WithSummary("Get Movie").Produces(200).Produces(400).Produces(404);

        routes.MapGet("/api/v1/catalog/movies/{id:guid}/similar", (ICatalogService service , Guid id, int limit = 8, CancellationToken cancellationToken = default) => service.GetSimilarMoviesAsync(id, limit, cancellationToken))
            .WithName("GetSimilarMovies").WithSummary("Get Similar Movies").Produces(200).Produces(400).Produces(404);

        var admin = routes.MapGroup("/api/v1/admin/movies")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin Movies");

        admin.MapPost("/", (ICatalogService service , MovieUpsertRequest request, CancellationToken cancellationToken) => service.CreateMovieAsync(request, cancellationToken))
            .WithName("CreateMovie").WithSummary("Create Movie").Produces(201).Produces(400).Produces(409);

        admin.MapPut("/{id:guid}", (ICatalogService service , Guid id, MovieUpsertRequest request, CancellationToken cancellationToken) => service.UpdateMovieAsync(id, request, cancellationToken))
            .WithName("UpdateMovie").WithSummary("Update Movie").Produces(204).Produces(400).Produces(404);

        admin.MapDelete("/{id:guid}", (ICatalogService service , Guid id, CancellationToken cancellationToken) => service.DeleteMovieAsync(id, cancellationToken))
            .WithName("DeleteMovie").WithSummary("Delete Movie").Produces(204).Produces(400).Produces(404);

        admin.MapPut("/{id:guid}/restore", (ICatalogService service , Guid id, CancellationToken cancellationToken) => service.RestoreMovieAsync(id, cancellationToken))
            .WithName("RestoreMovie").WithSummary("Restore Movie").Produces(204).Produces(400).Produces(404);

        return routes;
    }
}
