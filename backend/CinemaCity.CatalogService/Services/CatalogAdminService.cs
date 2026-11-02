using System.Text;
using System.Text.RegularExpressions;
using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.CatalogService.Services;

public interface ICatalogAdminService
{
    Task<IResult> GetGenresAsync(CancellationToken cancellationToken);
    Task<IResult> CreateGenreAsync(GenreUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> UpdateGenreAsync(int id, GenreUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> DeleteGenreAsync(int id, CancellationToken cancellationToken);
    Task<IResult> GetPersonsAsync(string? q, CancellationToken cancellationToken);
    Task<IResult> CreatePersonAsync(PersonUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> UpdatePersonAsync(Guid id, PersonUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> DeletePersonAsync(Guid id, CancellationToken cancellationToken);
    Task<IResult> UploadMoviePosterAsync(Guid movieId, HttpRequest request, CancellationToken cancellationToken);
    Task<IResult> UploadPersonPhotoAsync(Guid personId, HttpRequest request, CancellationToken cancellationToken);
    Task<IResult> UploadImageAsync(HttpRequest request, string bucket, CancellationToken cancellationToken);
}

public sealed class CatalogAdminService(IRepository<Genre> genreRepository, IUnitOfWork unitOfWork, IRepository<Person> personRepository, IWebHostEnvironment environment, IRepository<Movie> movieRepository, ILogger<CatalogAdminService> logger) : ICatalogAdminService
{
    public Task<IResult> GetGenresAsync(CancellationToken cancellationToken) => AdminEndpointsWorkflow.GetGenresAsync(genreRepository, cancellationToken);
    public async Task<IResult> CreateGenreAsync(GenreUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await AdminEndpointsWorkflow.CreateGenreAsync(request, genreRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("CreateGenreAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> UpdateGenreAsync(int id, GenreUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await AdminEndpointsWorkflow.UpdateGenreAsync(id, request, genreRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("UpdateGenreAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> DeleteGenreAsync(int id, CancellationToken cancellationToken)
    {
        var result = await AdminEndpointsWorkflow.DeleteGenreAsync(id, genreRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("DeleteGenreAsync completed with status {Status}", status);
        return result;
    }
    public Task<IResult> GetPersonsAsync(string? q, CancellationToken cancellationToken) => AdminEndpointsWorkflow.GetPersonsAsync(personRepository, q, cancellationToken);
    public async Task<IResult> CreatePersonAsync(PersonUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await AdminEndpointsWorkflow.CreatePersonAsync(request, personRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("CreatePersonAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> UpdatePersonAsync(Guid id, PersonUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await AdminEndpointsWorkflow.UpdatePersonAsync(id, request, personRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("UpdatePersonAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> DeletePersonAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await AdminEndpointsWorkflow.DeletePersonAsync(id, personRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("DeletePersonAsync completed with status {Status}", status);
        return result;
    }
    public Task<IResult> UploadMoviePosterAsync(Guid movieId, HttpRequest request, CancellationToken cancellationToken) => AdminEndpointsWorkflow.UploadMoviePosterAsync(movieId, request, environment, movieRepository, unitOfWork, cancellationToken);
    public Task<IResult> UploadPersonPhotoAsync(Guid personId, HttpRequest request, CancellationToken cancellationToken) => AdminEndpointsWorkflow.UploadPersonPhotoAsync(personId, request, environment, personRepository, unitOfWork, cancellationToken);
    public Task<IResult> UploadImageAsync(HttpRequest request, string bucket, CancellationToken cancellationToken) => AdminEndpointsWorkflow.UploadImageAsync(request, environment, bucket, cancellationToken);
}
