using CinemaCity.CatalogService.Imports;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.CatalogService.Services;

public interface ICatalogImportService
{
    Task<IResult> ImportPopularMoviesAsync(int pages = 5, CancellationToken cancellationToken = default);
    Task<IResult> ImportPopularTvShowsAsync(int pages = 3, CancellationToken cancellationToken = default);
    Task<IResult> ImportMovieDetailsAsync(int tmdbId, CancellationToken cancellationToken);
}

public sealed class CatalogImportService(ITmdbImportService importService) : ICatalogImportService
{
    public Task<IResult> ImportPopularMoviesAsync(int pages = 5, CancellationToken cancellationToken = default) => AdminEndpointsWorkflow.ImportPopularMoviesAsync(importService, pages, cancellationToken);
    public Task<IResult> ImportPopularTvShowsAsync(int pages = 3, CancellationToken cancellationToken = default) => AdminEndpointsWorkflow.ImportPopularTvShowsAsync(importService, pages, cancellationToken);
    public Task<IResult> ImportMovieDetailsAsync(int tmdbId, CancellationToken cancellationToken) => AdminEndpointsWorkflow.ImportMovieDetailsAsync(tmdbId, importService, cancellationToken);
}
