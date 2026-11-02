using System.Security.Claims;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Series;

public static class SeriesEndpoints
{
    public static IEndpointRouteBuilder MapSeriesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/movies/{movieId:guid}/seasons", GetSeasonsAsync)
            .WithName("GetSeriesSeasons")
            .WithTags("Series");

        var admin = routes.MapGroup("/api/admin")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin Series");

        admin.MapPost("/movies/{movieId:guid}/seasons", CreateSeasonAsync)
            .WithName("CreateSeason");

        admin.MapPut("/seasons/{seasonId:guid}", UpdateSeasonAsync)
            .WithName("UpdateSeason");

        admin.MapDelete("/seasons/{seasonId:guid}", DeleteSeasonAsync)
            .WithName("DeleteSeason");

        admin.MapPost("/seasons/{seasonId:guid}/episodes", CreateEpisodeAsync)
            .WithName("CreateEpisode");

        admin.MapPut("/episodes/{episodeId:guid}", UpdateEpisodeAsync)
            .WithName("UpdateEpisode");

        admin.MapDelete("/episodes/{episodeId:guid}", DeleteEpisodeAsync)
            .WithName("DeleteEpisode");

        return routes;
    }

    private static async Task<IResult> GetSeasonsAsync(
        Guid movieId,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<Season> seasonRepository,
        IRepository<Rating> ratingRepository,
        IRepository<WatchlistItem> watchlistRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var seriesExists = await movieRepository.Query()
            .AsNoTracking()
            .AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted && movie.ContentType == ContentType.Series, cancellationToken);

        if (!seriesExists)
        {
            return Results.NotFound(new { Message = "Series was not found." });
        }

        var currentUserId = TryGetUserId(principal);
        if (currentUserId is Guid userId)
        {
            await EnsureRatedSeriesIsWatchedAsync(movieId, userId, ratingRepository, watchlistRepository, unitOfWork, cancellationToken);
        }

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
                        currentUserId == null
                            ? null
                            : episode.Ratings
                                .Where(rating => rating.UserId == currentUserId.Value)
                                .Select(rating => new EpisodeUserRatingResponse(rating.Id, rating.Score))
                                .FirstOrDefault()))
                    .ToArray()))
            .ToListAsync(cancellationToken);

        return Results.Ok(seasons);
    }

    private static async Task EnsureRatedSeriesIsWatchedAsync(
        Guid seriesId,
        Guid userId,
        IRepository<Rating> ratingRepository,
        IRepository<WatchlistItem> watchlistRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var hasEpisodeRating = await ratingRepository.Query()
            .AsNoTracking()
            .AnyAsync(rating =>
                rating.UserId == userId
                && rating.EpisodeId != null
                && rating.Episode!.Season.SeriesId == seriesId,
                cancellationToken);

        if (!hasEpisodeRating)
        {
            return;
        }

        var watchlistItem = await watchlistRepository.Query()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.MovieId == seriesId, cancellationToken);

        if (watchlistItem is null)
        {
            watchlistRepository.Add(new WatchlistItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = seriesId,
                Status = WatchlistStatus.Watched,
                AddedAt = DateTimeOffset.UtcNow
            });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        if (watchlistItem.Status != WatchlistStatus.Watched)
        {
            watchlistItem.Status = WatchlistStatus.Watched;
            watchlistItem.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<IResult> CreateSeasonAsync(
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

    private static async Task<IResult> UpdateSeasonAsync(
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

    private static async Task<IResult> DeleteSeasonAsync(
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

    private static async Task<IResult> CreateEpisodeAsync(
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

    private static async Task<IResult> UpdateEpisodeAsync(
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

    private static async Task<IResult> DeleteEpisodeAsync(
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

    private static string? ValidateSeason(SeasonUpsertRequest request)
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

    private static string? ValidateEpisode(EpisodeUpsertRequest request)
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

    private static SeasonResponse ToSeasonResponse(Season season)
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

    private static EpisodeResponse ToEpisodeResponse(Episode episode)
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

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ValidateEpisodeDescription(string? description)
    {
        return !string.IsNullOrWhiteSpace(description) && description.Length > 800
            ? "Episode description must be at most 800 characters."
            : null;
    }

    private static Guid? TryGetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}

public sealed record SeasonResponse(
    Guid Id,
    Guid SeriesId,
    int SeasonNumber,
    string? Title,
    string? Description,
    int? ReleaseYear,
    IReadOnlyCollection<EpisodeResponse> Episodes);

public sealed record EpisodeResponse(
    Guid Id,
    Guid SeasonId,
    int EpisodeNumber,
    string Title,
    string? Description,
    DateOnly? AirDate,
    int? Duration,
    decimal AverageRating,
    int RatingsCount,
    EpisodeUserRatingResponse? UserRating);

public sealed record EpisodeUserRatingResponse(Guid Id, decimal Score);

public sealed record SeasonUpsertRequest(
    int SeasonNumber,
    string? Title,
    string? Description,
    int? ReleaseYear);

public sealed record EpisodeUpsertRequest(
    int EpisodeNumber,
    string Title,
    string? Description,
    DateOnly? AirDate,
    int? Duration);
