using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.CatalogService.Services;

public interface ICatalogSearchService
{
    Task<IResult> SearchAsync(string? q, string? scope = "all", int limit = 8, CancellationToken cancellationToken = default);
}

public sealed class CatalogSearchService(IRepository<Movie> movieRepository, IRepository<Episode> episodeRepository, IRepository<Person> personRepository) : ICatalogSearchService
{
    public Task<IResult> SearchAsync(string? q, string? scope = "all", int limit = 8, CancellationToken cancellationToken = default) => SearchEndpointsWorkflow.SearchAsync(movieRepository, episodeRepository, personRepository, q, scope, limit, cancellationToken);
}
