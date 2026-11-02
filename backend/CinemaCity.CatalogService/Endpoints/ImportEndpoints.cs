using CinemaCity.CatalogService.Imports;
using CinemaCity.ServiceDefaults;
using CinemaCity.CatalogService.Services;
namespace CinemaCity.CatalogService.Endpoints;

public static class ImportEndpoints
{
    public static void MapTmdbImportEndpoints(RouteGroupBuilder admin)
    {
        admin.MapPost("/import/tmdb/movies", (ICatalogImportService service , int pages = 5, CancellationToken cancellationToken = default) => service.ImportPopularMoviesAsync(pages, cancellationToken))
            .WithName("AdminImportTmdbMovies").WithSummary("Admin Import Tmdb Movies").Produces(200).Produces(400).Produces(401);

        admin.MapPost("/import/tmdb/tv-shows", (ICatalogImportService service , int pages = 3, CancellationToken cancellationToken = default) => service.ImportPopularTvShowsAsync(pages, cancellationToken))
            .WithName("AdminImportTmdbTvShows").WithSummary("Admin Import Tmdb Tv Shows").Produces(200).Produces(400).Produces(401);

        admin.MapPost("/import/tmdb/movie/{tmdbId:int}", (ICatalogImportService service , int tmdbId, CancellationToken cancellationToken) => service.ImportMovieDetailsAsync(tmdbId, cancellationToken))
            .WithName("AdminImportTmdbMovie").WithSummary("Admin Import Tmdb Movie").Produces(200).Produces(400).Produces(401);
    }
}
