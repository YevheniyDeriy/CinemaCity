using System.Security.Claims;
using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.CatalogService.Services;

internal static class SeriesEndpointsWorkflow
{


    internal static async Task<IResult> GetSeasonsAsync(
        Guid movieId,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<Season> seasonRepository,
        CommunityClient communityClient,
        CancellationToken cancellationToken)
    {
        var seriesExists = await movieRepository.Query()
            .AsNoTracking()
            .AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted && movie.ContentType == ContentType.Series, cancellationToken);

        if (!seriesExists)
        {
            return Results.NotFound(new { Message = "Series was not found." });
        }

        var userRatings = await communityClient.GetEpisodeRatingsAsync(movieId, principal, cancellationToken);
        var seasons = await seasonRepository.Query()
            .AsNoTracking()
            .Where(season => season.SeriesId == movieId)
            .OrderBy(season => season.SeasonNumber)
            .Select(season => new SeasonResponse(
                season.Id,
                season.SeriesId,
                season.SeasonNumber,
                season.Title,
                season.Description,
                season.ReleaseYear,
                season.Episodes
                    .OrderBy(episode => episode.EpisodeNumber)
                    .Select(episode => new EpisodeResponse(
                        episode.Id,
                        episode.SeasonId,
                        episode.EpisodeNumber,
                        episode.Title,
                        episode.Description,
                        episode.AirDate,
                        episode.Duration,
                        episode.AverageRating,
                        episode.RatingsCount,
                        null))
                    .ToArray()))
            .ToListAsync(cancellationToken);

        return Results.Ok(seasons.Select(season => season with { Episodes = season.Episodes.Select(episode => episode with { UserRating = userRatings.GetValueOrDefault(episode.Id) }).ToArray() }));
    }



    internal static async Task<IResult> CreateSeasonAsync(
        Guid movieId,
        SeasonUpsertRequest request,
        IRepository<Movie> movieRepository,
        IRepository<Season> seasonRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var series = await movieRepository.Query()
            .FirstOrDefaultAsync(movie => movie.Id == movieId && !movie.IsDeleted && movie.ContentType == ContentType.Series, cancellationToken);

        if (series is null)
        {
            return Results.NotFound(new { Message = "Series was not found." });
        }

        var validation = ValidateSeason(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var duplicate = await seasonRepository.Query()
            .AnyAsync(season => season.SeriesId == movieId && season.SeasonNumber == request.SeasonNumber, cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { Message = "Season with this number already exists." });
        }

        var season = new Season
        {
            Id = Guid.NewGuid(),
            SeriesId = movieId,
            SeasonNumber = request.SeasonNumber,
            Title = TrimOrNull(request.Title),
            Description = TrimOrNull(request.Description),
            ReleaseYear = request.ReleaseYear
        };

        seasonRepository.Add(season);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/movies/{movieId}/seasons", ToSeasonResponse(season));
    }

    internal static async Task<IResult> UpdateSeasonAsync(
        Guid seasonId,
        SeasonUpsertRequest request,
        IRepository<Season> seasonRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateSeason(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var season = await seasonRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == seasonId, cancellationToken);

        if (season is null)
        {
            return Results.NotFound(new { Message = "Season was not found." });
        }

        var duplicate = await seasonRepository.Query()
            .AnyAsync(candidate =>
                candidate.Id != seasonId
                && candidate.SeriesId == season.SeriesId
                && candidate.SeasonNumber == request.SeasonNumber,
                cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { Message = "Season with this number already exists." });
        }

        season.SeasonNumber = request.SeasonNumber;
        season.Title = TrimOrNull(request.Title);
        season.Description = TrimOrNull(request.Description);
        season.ReleaseYear = request.ReleaseYear;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToSeasonResponse(season));
    }

    internal static async Task<IResult> DeleteSeasonAsync(
        Guid seasonId,
        IRepository<Season> seasonRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var season = await seasonRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == seasonId, cancellationToken);

        if (season is null)
        {
            return Results.NotFound(new { Message = "Season was not found." });
        }

        seasonRepository.Remove(season);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    internal static async Task<IResult> CreateEpisodeAsync(
        Guid seasonId,
        EpisodeUpsertRequest request,
        IRepository<Season> seasonRepository,
        IRepository<Episode> episodeRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var seasonExists = await seasonRepository.Query().AnyAsync(season => season.Id == seasonId, cancellationToken);

        if (!seasonExists)
        {
            return Results.NotFound(new { Message = "Season was not found." });
        }

        var validation = ValidateEpisode(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var duplicate = await episodeRepository.Query()
            .AnyAsync(episode => episode.SeasonId == seasonId && episode.EpisodeNumber == request.EpisodeNumber, cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { Message = "Episode with this number already exists." });
        }

        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            EpisodeNumber = request.EpisodeNumber,
            Title = request.Title.Trim(),
            Description = TrimOrNull(request.Description),
            AirDate = request.AirDate,
            Duration = request.Duration
        };

        episodeRepository.Add(episode);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/episodes/{episode.Id}", ToEpisodeResponse(episode));
    }

    internal static async Task<IResult> UpdateEpisodeAsync(
        Guid episodeId,
        EpisodeUpsertRequest request,
        IRepository<Episode> episodeRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateEpisode(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var episode = await episodeRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == episodeId, cancellationToken);

        if (episode is null)
        {
            return Results.NotFound(new { Message = "Episode was not found." });
        }

        var duplicate = await episodeRepository.Query()
            .AnyAsync(candidate =>
                candidate.Id != episodeId
                && candidate.SeasonId == episode.SeasonId
                && candidate.EpisodeNumber == request.EpisodeNumber,
                cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { Message = "Episode with this number already exists." });
        }

        episode.EpisodeNumber = request.EpisodeNumber;
        episode.Title = request.Title.Trim();
        episode.Description = TrimOrNull(request.Description);
        episode.AirDate = request.AirDate;
        episode.Duration = request.Duration;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToEpisodeResponse(episode));
    }

    internal static async Task<IResult> DeleteEpisodeAsync(
        Guid episodeId,
        IRepository<Episode> episodeRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var episode = await episodeRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == episodeId, cancellationToken);

        if (episode is null)
        {
            return Results.NotFound(new { Message = "Episode was not found." });
        }

        episodeRepository.Remove(episode);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    internal static string? ValidateSeason(SeasonUpsertRequest request)
    {
        if (request.SeasonNumber <= 0)
        {
            return "Season number must be greater than zero.";
        }

        if (request.ReleaseYear is < 1888 or > 2200)
        {
            return "Release year is invalid.";
        }

        return null;
    }

    internal static string? ValidateEpisode(EpisodeUpsertRequest request)
    {
        if (request.EpisodeNumber <= 0)
        {
            return "Episode number must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 255)
        {
            return "Episode title is required and must be at most 255 characters.";
        }

        if (request.Duration is not null and <= 0)
        {
            return "Duration must be greater than zero.";
        }

        var descriptionValidation = ValidateEpisodeDescription(request.Description);
        if (descriptionValidation is not null)
        {
            return descriptionValidation;
        }

        return null;
    }

    internal static SeasonResponse ToSeasonResponse(Season season)
    {
        return new SeasonResponse(
            season.Id,
            season.SeriesId,
            season.SeasonNumber,
            season.Title,
            season.Description,
            season.ReleaseYear,
            []);
    }

    internal static EpisodeResponse ToEpisodeResponse(Episode episode)
    {
        return new EpisodeResponse(
            episode.Id,
            episode.SeasonId,
            episode.EpisodeNumber,
            episode.Title,
            episode.Description,
            episode.AirDate,
            episode.Duration,
            episode.AverageRating,
            episode.RatingsCount,
            null);
    }

    internal static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static string? ValidateEpisodeDescription(string? description)
    {
        return !string.IsNullOrWhiteSpace(description) && description.Length > 800
            ? "Episode description must be at most 800 characters."
            : null;
    }

    internal static Guid? TryGetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}

