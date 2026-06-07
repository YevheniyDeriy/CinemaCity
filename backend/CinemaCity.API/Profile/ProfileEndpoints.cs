using System.Security.Claims;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Profile;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/profile")
            .RequireAuthorization()
            .WithTags("Profile");

        group.MapGet("/stats", GetStatsAsync)
            .WithName("GetProfileStats");

        group.MapGet("/watchlist", GetWatchlistAsync)
            .WithName("GetProfileWatchlist");

        group.MapGet("/movies/{movieId:guid}/state", GetMovieStateAsync)
            .WithName("GetProfileMovieState");

        group.MapPost("/watchlist", UpsertWatchlistAsync)
            .WithName("UpsertWatchlistItem");

        group.MapPut("/watchlist/{movieId:guid}", UpdateWatchlistAsync)
            .WithName("UpdateWatchlistItem");

        group.MapDelete("/watchlist/{movieId:guid}", DeleteWatchlistAsync)
            .WithName("DeleteWatchlistItem");

        return routes;
    }

    private static async Task<IResult> GetStatsAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var watchlistCount = await dbContext.WatchlistItems.CountAsync(item => item.UserId == userId && item.Status == WatchlistStatus.Watchlist, cancellationToken);
        var watchedCount = await dbContext.WatchlistItems.CountAsync(item => item.UserId == userId && item.Status == WatchlistStatus.Watched, cancellationToken);
        var ratings = dbContext.Ratings.AsNoTracking().Where(rating => rating.UserId == userId);
        var ratingsCount = await ratings.CountAsync(cancellationToken);
        var averageScore = ratingsCount == 0 ? 0 : Math.Round(await ratings.AverageAsync(rating => rating.Score, cancellationToken), 1);
        var reviewsCount = await dbContext.Reviews.CountAsync(review => review.UserId == userId && !review.IsDeleted, cancellationToken);
        var approvedReviewsCount = await dbContext.Reviews.CountAsync(review => review.UserId == userId && !review.IsDeleted && review.Status == ReviewStatus.Approved, cancellationToken);
        var commentsCount = await dbContext.Comments.CountAsync(comment => comment.UserId == userId && !comment.IsDeleted, cancellationToken);
        var likedReviewsCount = await dbContext.ReviewLikes.CountAsync(like => like.UserId == userId, cancellationToken);

        var favoriteGenres = await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId && rating.MovieId != null)
            .SelectMany(rating => rating.Movie!.Genres.Select(genre => genre.Name))
            .GroupBy(name => name)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(3)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);

        return Results.Ok(new ProfileStatsResponse(
            watchlistCount,
            watchedCount,
            ratingsCount,
            averageScore,
            reviewsCount,
            approvedReviewsCount,
            commentsCount,
            likedReviewsCount,
            favoriteGenres));
    }

    private static async Task<IResult> GetWatchlistAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        WatchlistStatus? status,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var query = dbContext.WatchlistItems
            .AsNoTracking()
            .Include(item => item.Movie)
                .ThenInclude(movie => movie.Genres)
            .Where(item => item.UserId == userId && !item.Movie.IsDeleted);

        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        var items = await query
            .OrderByDescending(item => item.UpdatedAt ?? item.AddedAt)
            .Select(item => ToWatchlistItemResponse(item))
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> GetMovieStateAsync(
        Guid movieId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var movieExists = await dbContext.Movies.AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted, cancellationToken);

        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var watchlistItem = await dbContext.WatchlistItems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.MovieId == movieId, cancellationToken);

        var rating = await dbContext.Ratings
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId && candidate.MovieId == movieId)
            .Select(candidate => new UserRatingResponse(candidate.Id, candidate.Score))
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(new MovieUserStateResponse(
            watchlistItem?.Status,
            watchlistItem?.AddedAt,
            rating));
    }

    private static Task<IResult> UpdateWatchlistAsync(
        Guid movieId,
        WatchlistUpsertRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return SaveWatchlistAsync(movieId, request.Status, principal, dbContext, cancellationToken);
    }

    private static Task<IResult> UpsertWatchlistAsync(
        WatchlistUpsertRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return SaveWatchlistAsync(request.MovieId, request.Status, principal, dbContext, cancellationToken);
    }

    private static async Task<IResult> SaveWatchlistAsync(
        Guid movieId,
        WatchlistStatus status,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var movieExists = await dbContext.Movies.AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted, cancellationToken);

        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var userId = GetUserId(principal);
        var item = await dbContext.WatchlistItems
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.MovieId == movieId, cancellationToken);

        if (item is null)
        {
            item = new WatchlistItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movieId,
                Status = status,
                AddedAt = DateTimeOffset.UtcNow
            };
            dbContext.WatchlistItems.Add(item);
        }
        else
        {
            item.Status = status;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await dbContext.WatchlistItems
            .AsNoTracking()
            .Include(candidate => candidate.Movie)
                .ThenInclude(movie => movie.Genres)
            .Where(candidate => candidate.Id == item.Id)
            .Select(candidate => ToWatchlistItemResponse(candidate))
            .FirstAsync(cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> DeleteWatchlistAsync(
        Guid movieId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var item = await dbContext.WatchlistItems
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.MovieId == movieId, cancellationToken);

        if (item is null)
        {
            return Results.NoContent();
        }

        dbContext.WatchlistItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private static WatchlistItemResponse ToWatchlistItemResponse(WatchlistItem item)
    {
        return new WatchlistItemResponse(
            item.Id,
            item.MovieId,
            item.Movie.Title,
            item.Movie.OriginalTitle,
            item.Movie.ContentType,
            item.Movie.ReleaseYear,
            item.Movie.PosterUrl,
            item.Movie.BackdropUrl,
            item.Movie.AverageRating,
            item.Movie.RatingsCount,
            item.Movie.Genres.OrderBy(genre => genre.Name).Select(genre => new WatchlistGenreResponse(genre.Id, genre.Name, genre.Slug)).ToArray(),
            item.Status,
            item.AddedAt,
            item.UpdatedAt);
    }
}

public sealed record WatchlistUpsertRequest(Guid MovieId, WatchlistStatus Status);

public sealed record ProfileStatsResponse(
    int WatchlistCount,
    int WatchedCount,
    int RatingsCount,
    decimal AverageScore,
    int ReviewsCount,
    int ApprovedReviewsCount,
    int CommentsCount,
    int LikedReviewsCount,
    IReadOnlyCollection<string> FavoriteGenres);

public sealed record MovieUserStateResponse(
    WatchlistStatus? WatchlistStatus,
    DateTimeOffset? AddedAt,
    UserRatingResponse? Rating);

public sealed record UserRatingResponse(Guid Id, decimal Score);

public sealed record WatchlistGenreResponse(int Id, string Name, string Slug);

public sealed record WatchlistItemResponse(
    Guid Id,
    Guid MovieId,
    string Title,
    string? OriginalTitle,
    ContentType ContentType,
    int ReleaseYear,
    string? PosterUrl,
    string? BackdropUrl,
    decimal AverageRating,
    int RatingsCount,
    IReadOnlyCollection<WatchlistGenreResponse> Genres,
    WatchlistStatus Status,
    DateTimeOffset AddedAt,
    DateTimeOffset? UpdatedAt);
