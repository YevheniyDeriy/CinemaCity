using CinemaCity.Application.Imports;

namespace CinemaCity.API.Admin;

public static partial class AdminEndpoints
{
    private static void MapTmdbImportEndpoints(RouteGroupBuilder admin)
    {
        admin.MapPost("/import/tmdb/movies", ImportPopularMoviesAsync)
            .WithName("AdminImportTmdbMovies");

        admin.MapPost("/import/tmdb/tv-shows", ImportPopularTvShowsAsync)
            .WithName("AdminImportTmdbTvShows");

        admin.MapPost("/import/tmdb/movie/{tmdbId:int}", ImportMovieDetailsAsync)
            .WithName("AdminImportTmdbMovie");
    }

    private static async Task<IResult> ImportPopularMoviesAsync(
        ITmdbImportService importService,
        int pages = 5,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePages(pages);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var result = await importService.ImportPopularMoviesAsync(pages, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ImportPopularTvShowsAsync(
        ITmdbImportService importService,
        int pages = 3,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePages(pages);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var result = await importService.ImportPopularTvShowsAsync(pages, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ImportMovieDetailsAsync(
        int tmdbId,
        ITmdbImportService importService,
        CancellationToken cancellationToken)
    {
        if (tmdbId <= 0)
        {
            return Results.BadRequest(new { Message = "TMDB ID must be greater than zero." });
        }

        var result = await importService.ImportMovieDetailsAsync(tmdbId, cancellationToken);
        return Results.Ok(result);
    }

    private static string? ValidatePages(int pages)
    {
        return pages < 1 ? "Pages must be greater than zero." : null;
    }
}
