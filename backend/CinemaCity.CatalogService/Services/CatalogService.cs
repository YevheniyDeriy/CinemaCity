using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.CatalogService.Services;

public interface ICatalogService
{
    Task<IResult> GetPageAsync(CatalogQuery query, CancellationToken cancellationToken);
    Task<IResult> GetGenresAsync(CancellationToken cancellationToken);
    Task<IResult> GetMoviesAsync(string? q, string? genre, int? yearFrom, int? yearTo, decimal? ratingMin, string? contentType, string? sort, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<IResult> SearchMoviesAsync(string? q, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<IResult> GetMovieAsync(Guid id, CancellationToken cancellationToken);
    Task<IResult> GetSimilarMoviesAsync(Guid id, int limit = 8, CancellationToken cancellationToken = default);
    Task<IResult> CreateMovieAsync(MovieUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> UpdateMovieAsync(Guid id, MovieUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> DeleteMovieAsync(Guid id, CancellationToken cancellationToken);
    Task<IResult> RestoreMovieAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class CatalogService(IRepository<Genre> genreRepository, IRepository<Movie> movieRepository, IRepository<Country> countryRepository, IRepository<Language> languageRepository, IRepository<Person> personRepository, IUnitOfWork unitOfWork, IRepository<MovieCast> movieCastRepository, ICatalogRepository catalogRepository, ILogger<CatalogService> logger) : ICatalogService
{
    public async Task<IResult> GetPageAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        var errors = query.Validate();
        return errors.Count > 0 ? Results.ValidationProblem(errors) : Results.Ok(await catalogRepository.GetPageAsync(query, cancellationToken));
    }
    public Task<IResult> GetGenresAsync(CancellationToken cancellationToken) => MovieEndpointsWorkflow.GetGenresAsync(genreRepository, cancellationToken);
    public Task<IResult> GetMoviesAsync(string? q, string? genre, int? yearFrom, int? yearTo, decimal? ratingMin, string? contentType, string? sort, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => GetPageAsync(new CatalogQuery(page, pageSize, q, null, sort ?? "rating", sort != "title", genre, yearFrom, yearTo, ratingMin, contentType), cancellationToken);
    public Task<IResult> SearchMoviesAsync(string? q, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => GetPageAsync(new CatalogQuery(page, pageSize, q, Desc: true), cancellationToken);
    public Task<IResult> GetMovieAsync(Guid id, CancellationToken cancellationToken) => MovieEndpointsWorkflow.GetMovieAsync(id, movieRepository, cancellationToken);
    public Task<IResult> GetSimilarMoviesAsync(Guid id, int limit = 8, CancellationToken cancellationToken = default) => MovieEndpointsWorkflow.GetSimilarMoviesAsync(id, movieRepository, limit, cancellationToken);
    public async Task<IResult> CreateMovieAsync(MovieUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await MovieEndpointsWorkflow.CreateMovieAsync(request, movieRepository, genreRepository, countryRepository, languageRepository, personRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("CreateMovieAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> UpdateMovieAsync(Guid id, MovieUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await MovieEndpointsWorkflow.UpdateMovieAsync(id, request, movieRepository, movieCastRepository, genreRepository, countryRepository, languageRepository, personRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("UpdateMovieAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> DeleteMovieAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await MovieEndpointsWorkflow.DeleteMovieAsync(id, movieRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("DeleteMovieAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> RestoreMovieAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await MovieEndpointsWorkflow.RestoreMovieAsync(id, movieRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("RestoreMovieAsync completed with status {Status}", status);
        return result;
    }
}
