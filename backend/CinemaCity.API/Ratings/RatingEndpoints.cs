using System.Security.Claims;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
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
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateTarget(request.MovieId, request.EpisodeId, request.Score);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var userId = GetUserId(principal);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (request.MovieId is Guid movieId)
        {
            var movieExists = await dbContext.Movies
                .AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted, cancellationToken);

            if (!movieExists)
            {
                return Results.NotFound(new { Message = "Movie was not found." });
            }

            var duplicate = await dbContext.Ratings
                .AnyAsync(rating => rating.UserId == userId && rating.MovieId == movieId, cancellationToken);

            if (duplicate)
            {
                return Results.Conflict(new { Message = "Movie was already rated by this user." });
            }
        }
        else if (request.EpisodeId is Guid episodeId)
        {
            var episodeExists = await dbContext.Episodes.AnyAsync(episode => episode.Id == episodeId, cancellationToken);

            if (!episodeExists)
            {
                return Results.NotFound(new { Message = "Episode was not found." });
            }

            var duplicate = await dbContext.Ratings
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

        dbContext.Ratings.Add(rating);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateTargetRatingAsync(dbContext, rating.MovieId, rating.EpisodeId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/ratings/{rating.Id}", ToResponse(rating));
    }

    private static async Task<IResult> UpdateRatingAsync(
        Guid ratingId,
        RatingUpdateRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.Score is < 1 or > 10)
        {
            return Results.BadRequest(new { Message = "Score must be between 1 and 10." });
        }

        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var rating = await dbContext.Ratings.FirstOrDefaultAsync(candidate => candidate.Id == ratingId, cancellationToken);

        if (rating is null)
        {
            return Results.NotFound(new { Message = "Rating was not found." });
        }

        if (!isAdmin && rating.UserId != userId)
        {
            return Results.Forbid();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        rating.Score = request.Score;
        rating.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateTargetRatingAsync(dbContext, rating.MovieId, rating.EpisodeId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(ToResponse(rating));
    }

    private static async Task<IResult> DeleteRatingAsync(
        Guid ratingId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var rating = await dbContext.Ratings.FirstOrDefaultAsync(candidate => candidate.Id == ratingId, cancellationToken);

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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Ratings.Remove(rating);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateTargetRatingAsync(dbContext, movieId, episodeId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task RecalculateTargetRatingAsync(
        AppDbContext dbContext,
        Guid? movieId,
        Guid? episodeId,
        CancellationToken cancellationToken)
    {
        if (movieId is Guid actualMovieId)
        {
            var movie = await dbContext.Movies.FirstAsync(candidate => candidate.Id == actualMovieId, cancellationToken);
            var stats = await dbContext.Ratings
                .Where(rating => rating.MovieId == actualMovieId)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Count = group.Count(),
                    Average = group.Average(rating => rating.Score)
                })
                .FirstOrDefaultAsync(cancellationToken);

            movie.RatingsCount = stats?.Count ?? 0;
            movie.AverageRating = stats is null ? 0 : Math.Round(stats.Average, 1);
            movie.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else if (episodeId is Guid actualEpisodeId)
        {
            var episode = await dbContext.Episodes.FirstAsync(candidate => candidate.Id == actualEpisodeId, cancellationToken);
            var stats = await dbContext.Ratings
                .Where(rating => rating.EpisodeId == actualEpisodeId)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Count = group.Count(),
                    Average = group.Average(rating => rating.Score)
                })
                .FirstOrDefaultAsync(cancellationToken);

            episode.RatingsCount = stats?.Count ?? 0;
            episode.AverageRating = stats is null ? 0 : Math.Round(stats.Average, 1);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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

public sealed record RatingResponse(
    Guid Id,
    Guid? MovieId,
    Guid? EpisodeId,
    decimal Score,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
