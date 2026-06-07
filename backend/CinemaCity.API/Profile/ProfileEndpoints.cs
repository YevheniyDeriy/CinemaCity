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

        group.MapGet("/lists", GetMovieListsAsync)
            .WithName("GetProfileMovieLists");

        group.MapGet("/lists/{listId:guid}", GetMovieListAsync)
            .WithName("GetProfileMovieList");

        group.MapPost("/lists", CreateMovieListAsync)
            .WithName("CreateProfileMovieList");

        group.MapPut("/lists/{listId:guid}", UpdateMovieListAsync)
            .WithName("UpdateProfileMovieList");

        group.MapDelete("/lists/{listId:guid}", DeleteMovieListAsync)
            .WithName("DeleteProfileMovieList");

        group.MapPost("/lists/{listId:guid}/items", AddMovieListItemAsync)
            .WithName("AddProfileMovieListItem");

        group.MapDelete("/lists/{listId:guid}/items/{movieId:guid}", DeleteMovieListItemAsync)
            .WithName("DeleteProfileMovieListItem");

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

        var listMemberships = await dbContext.UserMovieListItems
            .AsNoTracking()
            .Where(item => item.MovieId == movieId && item.UserMovieList.UserId == userId)
            .OrderBy(item => item.UserMovieList.Name)
            .Select(item => new MovieListMembershipResponse(
                item.UserMovieListId,
                item.UserMovieList.Name))
            .ToListAsync(cancellationToken);

        return Results.Ok(new MovieUserStateResponse(
            watchlistItem?.Status,
            watchlistItem?.AddedAt,
            rating,
            listMemberships));
    }

    private static async Task<IResult> GetMovieListsAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var lists = await dbContext.UserMovieLists
            .AsNoTracking()
            .Where(list => list.UserId == userId)
            .OrderByDescending(list => list.UpdatedAt ?? list.CreatedAt)
            .Select(list => new MovieListSummaryResponse(
                list.Id,
                list.Name,
                list.Description,
                list.Items.Count,
                list.CreatedAt,
                list.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(lists);
    }

    private static async Task<IResult> GetMovieListAsync(
        Guid listId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var list = await dbContext.UserMovieLists
            .AsNoTracking()
            .Where(candidate => candidate.Id == listId && candidate.UserId == userId)
            .Select(candidate => new MovieListDetailsResponse(
                candidate.Id,
                candidate.Name,
                candidate.Description,
                candidate.CreatedAt,
                candidate.UpdatedAt,
                candidate.Items
                    .Where(item => !item.Movie.IsDeleted)
                    .OrderByDescending(item => item.AddedAt)
                    .Select(item => new MovieListItemResponse(
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
                        item.AddedAt))
                    .ToArray()))
            .FirstOrDefaultAsync(cancellationToken);

        if (list is null)
        {
            return Results.NotFound(new { Message = "List was not found." });
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> CreateMovieListAsync(
        MovieListUpsertRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMovieList(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var userId = GetUserId(principal);
        var name = request.Name.Trim();
        var exists = await dbContext.UserMovieLists
            .AnyAsync(list => list.UserId == userId && list.Name == name, cancellationToken);

        if (exists)
        {
            return Results.Conflict(new { Message = "List name already exists." });
        }

        var list = new UserMovieList
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Description = TrimOrNull(request.Description),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.UserMovieLists.Add(list);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/profile/lists/{list.Id}", new MovieListSummaryResponse(
            list.Id,
            list.Name,
            list.Description,
            0,
            list.CreatedAt,
            list.UpdatedAt));
    }

    private static async Task<IResult> UpdateMovieListAsync(
        Guid listId,
        MovieListUpsertRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMovieList(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var userId = GetUserId(principal);
        var list = await dbContext.UserMovieLists
            .FirstOrDefaultAsync(candidate => candidate.Id == listId && candidate.UserId == userId, cancellationToken);

        if (list is null)
        {
            return Results.NotFound(new { Message = "List was not found." });
        }

        var name = request.Name.Trim();
        var nameExists = await dbContext.UserMovieLists
            .AnyAsync(candidate => candidate.UserId == userId && candidate.Id != listId && candidate.Name == name, cancellationToken);

        if (nameExists)
        {
            return Results.Conflict(new { Message = "List name already exists." });
        }

        list.Name = name;
        list.Description = TrimOrNull(request.Description);
        list.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteMovieListAsync(
        Guid listId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var list = await dbContext.UserMovieLists
            .FirstOrDefaultAsync(candidate => candidate.Id == listId && candidate.UserId == userId, cancellationToken);

        if (list is null)
        {
            return Results.NoContent();
        }

        dbContext.UserMovieLists.Remove(list);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> AddMovieListItemAsync(
        Guid listId,
        MovieListItemRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var list = await dbContext.UserMovieLists
            .FirstOrDefaultAsync(candidate => candidate.Id == listId && candidate.UserId == userId, cancellationToken);

        if (list is null)
        {
            return Results.NotFound(new { Message = "List was not found." });
        }

        var movieExists = await dbContext.Movies.AnyAsync(movie => movie.Id == request.MovieId && !movie.IsDeleted, cancellationToken);
        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var existing = await dbContext.UserMovieListItems
            .FirstOrDefaultAsync(candidate => candidate.UserMovieListId == listId && candidate.MovieId == request.MovieId, cancellationToken);

        if (existing is null)
        {
            dbContext.UserMovieListItems.Add(new UserMovieListItem
            {
                Id = Guid.NewGuid(),
                UserMovieListId = listId,
                MovieId = request.MovieId,
                AddedAt = DateTimeOffset.UtcNow
            });

            list.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteMovieListItemAsync(
        Guid listId,
        Guid movieId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var item = await dbContext.UserMovieListItems
            .Include(candidate => candidate.UserMovieList)
            .FirstOrDefaultAsync(candidate =>
                candidate.UserMovieListId == listId
                && candidate.MovieId == movieId
                && candidate.UserMovieList.UserId == userId,
                cancellationToken);

        if (item is null)
        {
            return Results.NoContent();
        }

        item.UserMovieList.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.UserMovieListItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
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

    private static MovieListItemResponse ToMovieListItemResponse(UserMovieListItem item)
    {
        return new MovieListItemResponse(
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
            item.AddedAt);
    }

    private static string? ValidateMovieList(MovieListUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80)
        {
            return "List name is required and must be at most 80 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Length > 500)
        {
            return "List description must be at most 500 characters.";
        }

        return null;
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
    UserRatingResponse? Rating,
    IReadOnlyCollection<MovieListMembershipResponse> Lists);

public sealed record UserRatingResponse(Guid Id, decimal Score);

public sealed record MovieListMembershipResponse(Guid Id, string Name);

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

public sealed record MovieListUpsertRequest(string Name, string? Description);

public sealed record MovieListItemRequest(Guid MovieId);

public sealed record MovieListSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    int ItemsCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record MovieListDetailsResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyCollection<MovieListItemResponse> Items);

public sealed record MovieListItemResponse(
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
    DateTimeOffset AddedAt);
