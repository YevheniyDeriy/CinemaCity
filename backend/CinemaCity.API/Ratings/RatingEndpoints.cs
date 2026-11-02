using System.Security.Claims;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Ratings;

public static class RatingEndpoints
{
    public static IEndpointRouteBuilder MapRatingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/ratings")
            .RequireAuthorization()
            .WithTags("Ratings");

        group.MapPost("/", CreateRatingAsync)
            .WithName("CreateRating");

        group.MapPut("/{ratingId:guid}", UpdateRatingAsync)
            .WithName("UpdateRating");

        group.MapDelete("/{ratingId:guid}", DeleteRatingAsync)
            .WithName("DeleteRating");

        return routes;
    }

    private static async Task<IResult> CreateRatingAsync(
        RatingCreateRequest request,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<Episode> episodeRepository,
        IRepository<Rating> ratingRepository,
        IRepository<WatchlistItem> watchlistRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateTarget(request.MovieId, request.EpisodeId, request.Score);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var userId = GetUserId(principal);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (request.MovieId is Guid movieId)
        {
            var movieExists = await movieRepository.Query()
                .AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted, cancellationToken);

            if (!movieExists)
            {
                return Results.NotFound(new { Message = "Movie was not found." });
            }

            var duplicate = await ratingRepository.Query()
                .AnyAsync(rating => rating.UserId == userId && rating.MovieId == movieId, cancellationToken);

            if (duplicate)
            {
                return Results.Conflict(new { Message = "Movie was already rated by this user." });
            }
        }
        else if (request.EpisodeId is Guid episodeId)
        {
            var episodeExists = await episodeRepository.Query().AnyAsync(episode => episode.Id == episodeId, cancellationToken);

            if (!episodeExists)
            {
                return Results.NotFound(new { Message = "Episode was not found." });
            }

            var duplicate = await ratingRepository.Query()
                .AnyAsync(rating => rating.UserId == userId && rating.EpisodeId == episodeId, cancellationToken);

            if (duplicate)
            {
                return Results.Conflict(new { Message = "Episode was already rated by this user." });
            }
        }

        var rating = new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = request.MovieId,
            EpisodeId = request.EpisodeId,
            Score = request.Score,
            CreatedAt = DateTimeOffset.UtcNow
        };

        ratingRepository.Add(rating);
        await ApplyTargetRatingChangeAsync(movieRepository, episodeRepository, unitOfWork, rating.MovieId, rating.EpisodeId, null, rating.Score, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var watchedMovieId = await ResolveWatchedMovieIdAsync(episodeRepository, rating.MovieId, rating.EpisodeId, cancellationToken);
        await MarkMovieWatchedAsync(watchlistRepository, unitOfWork, userId, watchedMovieId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/ratings/{rating.Id}", ToResponse(rating));
    }

    private static async Task<IResult> UpdateRatingAsync(
        Guid ratingId,
        RatingUpdateRequest request,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<Episode> episodeRepository,
        IRepository<Rating> ratingRepository,
        IRepository<WatchlistItem> watchlistRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (request.Score is < 1 or > 10)
        {
            return Results.BadRequest(new { Message = "Score must be between 1 and 10." });
        }

        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var rating = await ratingRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == ratingId, cancellationToken);

        if (rating is null)
        {
            return Results.NotFound(new { Message = "Rating was not found." });
        }

        if (!isAdmin && rating.UserId != userId)
        {
            return Results.Forbid();
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var previousScore = rating.Score;
        rating.Score = request.Score;
        rating.UpdatedAt = DateTimeOffset.UtcNow;

        await ApplyTargetRatingChangeAsync(movieRepository, episodeRepository, unitOfWork, rating.MovieId, rating.EpisodeId, previousScore, rating.Score, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var watchedMovieId = await ResolveWatchedMovieIdAsync(episodeRepository, rating.MovieId, rating.EpisodeId, cancellationToken);
        await MarkMovieWatchedAsync(watchlistRepository, unitOfWork, rating.UserId, watchedMovieId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(ToResponse(rating));
    }

    private static async Task<IResult> DeleteRatingAsync(
        Guid ratingId,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<Episode> episodeRepository,
        IRepository<Rating> ratingRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var rating = await ratingRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == ratingId, cancellationToken);

        if (rating is null)
        {
            return Results.NotFound(new { Message = "Rating was not found." });
        }

        if (!isAdmin && rating.UserId != userId)
        {
            return Results.Forbid();
        }

        var movieId = rating.MovieId;
        var episodeId = rating.EpisodeId;
        var previousScore = rating.Score;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        ratingRepository.Remove(rating);
        await ApplyTargetRatingChangeAsync(movieRepository, episodeRepository, unitOfWork, movieId, episodeId, previousScore, null, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task ApplyTargetRatingChangeAsync(
        IRepository<Movie> movieRepository,
        IRepository<Episode> episodeRepository,
        IUnitOfWork unitOfWork,
        Guid? movieId,
        Guid? episodeId,
        decimal? oldScore,
        decimal? newScore,
        CancellationToken cancellationToken)
    {
        if (movieId is Guid actualMovieId)
        {
            var movie = await movieRepository.Query().FirstAsync(candidate => candidate.Id == actualMovieId, cancellationToken);
            ApplyRatingChange(movie, oldScore, newScore);
            movie.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else if (episodeId is Guid actualEpisodeId)
        {
            var episode = await episodeRepository.Query().FirstAsync(candidate => candidate.Id == actualEpisodeId, cancellationToken);
            ApplyRatingChange(episode, oldScore, newScore);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyRatingChange(Movie movie, decimal? oldScore, decimal? newScore)
    {
        var stats = ApplyRatingChange(movie.AverageRating, movie.RatingsCount, oldScore, newScore);
        movie.AverageRating = stats.AverageRating;
        movie.RatingsCount = stats.RatingsCount;
    }

    private static void ApplyRatingChange(Episode episode, decimal? oldScore, decimal? newScore)
    {
        var stats = ApplyRatingChange(episode.AverageRating, episode.RatingsCount, oldScore, newScore);
        episode.AverageRating = stats.AverageRating;
        episode.RatingsCount = stats.RatingsCount;
    }

    private static RatingStats ApplyRatingChange(decimal currentAverage, int currentCount, decimal? oldScore, decimal? newScore)
    {
        var count = Math.Max(0, currentCount);
        var total = currentAverage * count;

        if (oldScore is not null && count > 0)
        {
            total -= oldScore.Value;
            count--;
        }

        if (newScore is not null)
        {
            total += newScore.Value;
            count++;
        }

        if (count == 0)
        {
            return new RatingStats(0, 0);
        }

        return new RatingStats(Math.Round(total / count, 1), count);
    }

    private static async Task MarkMovieWatchedAsync(
        IRepository<WatchlistItem> watchlistRepository,
        IUnitOfWork unitOfWork,
        Guid userId,
        Guid? movieId,
        CancellationToken cancellationToken)
    {
        if (movieId is null)
        {
            return;
        }

        var item = await watchlistRepository.Query()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.MovieId == movieId, cancellationToken);

        if (item is null)
        {
            watchlistRepository.Add(new WatchlistItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movieId.Value,
                Status = WatchlistStatus.Watched,
                AddedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            item.Status = WatchlistStatus.Watched;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Guid?> ResolveWatchedMovieIdAsync(
        IRepository<Episode> episodeRepository,
        Guid? movieId,
        Guid? episodeId,
        CancellationToken cancellationToken)
    {
        if (movieId is not null)
        {
            return movieId;
        }

        if (episodeId is null)
        {
            return null;
        }

        return await episodeRepository.Query()
            .AsNoTracking()
            .Where(episode => episode.Id == episodeId)
            .Select(episode => (Guid?)episode.Season.SeriesId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? ValidateTarget(Guid? movieId, Guid? episodeId, decimal score)
    {
        if (score is < 1 or > 10)
        {
            return "Score must be between 1 and 10.";
        }

        if ((movieId is null && episodeId is null) || (movieId is not null && episodeId is not null))
        {
            return "Rating must target either a movie or an episode.";
        }

        return null;
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private static bool IsAdmin(ClaimsPrincipal principal)
    {
        return string.Equals(principal.FindFirstValue(ClaimTypes.Role), UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static RatingResponse ToResponse(Rating rating)
    {
        return new RatingResponse(
            rating.Id,
            rating.MovieId,
            rating.EpisodeId,
            rating.Score,
            rating.CreatedAt,
            rating.UpdatedAt);
    }
}

public sealed record RatingCreateRequest(Guid? MovieId, Guid? EpisodeId, decimal Score);

public sealed record RatingUpdateRequest(decimal Score);

public sealed record RatingStats(decimal AverageRating, int RatingsCount);

public sealed record RatingResponse(
    Guid Id,
    Guid? MovieId,
    Guid? EpisodeId,
    decimal Score,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
