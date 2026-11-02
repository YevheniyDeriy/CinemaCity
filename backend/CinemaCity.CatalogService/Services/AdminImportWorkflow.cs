using CinemaCity.CatalogService.Imports;

namespace CinemaCity.CatalogService.Services;

internal static partial class AdminEndpointsWorkflow
{


    internal static async Task<IResult> ImportPopularMoviesAsync(
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

    internal static async Task<IResult> ImportPopularTvShowsAsync(
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

    internal static async Task<IResult> ImportMovieDetailsAsync(
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

    internal static string? ValidatePages(int pages)
    {
        return pages < 1 ? "Pages must be greater than zero." : null;
    }
}

