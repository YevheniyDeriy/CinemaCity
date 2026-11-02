namespace CinemaCity.Application.Imports;

public interface ITmdbImportService
{
    Task<ImportResultDto> ImportPopularMoviesAsync(
        int pages,
        CancellationToken cancellationToken = default);

    Task<ImportResultDto> ImportPopularTvShowsAsync(
        int pages,
        CancellationToken cancellationToken = default);

    Task<ImportResultDto> ImportMovieDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);
}
