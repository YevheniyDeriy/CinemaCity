using System.Security.Claims;
using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.CatalogService.Services;

public interface ISeriesService
{
    Task<IResult> GetSeasonsAsync(Guid movieId, ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<IResult> CreateSeasonAsync(Guid movieId, SeasonUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> UpdateSeasonAsync(Guid seasonId, SeasonUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> DeleteSeasonAsync(Guid seasonId, CancellationToken cancellationToken);
    Task<IResult> CreateEpisodeAsync(Guid seasonId, EpisodeUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> UpdateEpisodeAsync(Guid episodeId, EpisodeUpsertRequest request, CancellationToken cancellationToken);
    Task<IResult> DeleteEpisodeAsync(Guid episodeId, CancellationToken cancellationToken);
}

public sealed class SeriesService(IRepository<Movie> movieRepository, IRepository<Season> seasonRepository, CommunityClient communityClient, IUnitOfWork unitOfWork, IRepository<Episode> episodeRepository, ILogger<SeriesService> logger) : ISeriesService
{
    public Task<IResult> GetSeasonsAsync(Guid movieId, ClaimsPrincipal principal, CancellationToken cancellationToken) => SeriesEndpointsWorkflow.GetSeasonsAsync(movieId, principal, movieRepository, seasonRepository, communityClient, cancellationToken);
    public async Task<IResult> CreateSeasonAsync(Guid movieId, SeasonUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await SeriesEndpointsWorkflow.CreateSeasonAsync(movieId, request, movieRepository, seasonRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("CreateSeasonAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> UpdateSeasonAsync(Guid seasonId, SeasonUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await SeriesEndpointsWorkflow.UpdateSeasonAsync(seasonId, request, seasonRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("UpdateSeasonAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> DeleteSeasonAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var result = await SeriesEndpointsWorkflow.DeleteSeasonAsync(seasonId, seasonRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("DeleteSeasonAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> CreateEpisodeAsync(Guid seasonId, EpisodeUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await SeriesEndpointsWorkflow.CreateEpisodeAsync(seasonId, request, seasonRepository, episodeRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("CreateEpisodeAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> UpdateEpisodeAsync(Guid episodeId, EpisodeUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await SeriesEndpointsWorkflow.UpdateEpisodeAsync(episodeId, request, episodeRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("UpdateEpisodeAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> DeleteEpisodeAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        var result = await SeriesEndpointsWorkflow.DeleteEpisodeAsync(episodeId, episodeRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("DeleteEpisodeAsync completed with status {Status}", status);
        return result;
    }
}
