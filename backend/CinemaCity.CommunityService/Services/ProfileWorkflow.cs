using System.Security.Claims;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Profile;

public static class ProfileWorkflow
{
    public static async Task<IResult> GetPublicProfileAsync(
        Guid userId,
        IRepository<User> userRepository,
        IRepository<UserMovieList> listRepository,
        IRepository<Review> reviewRepository,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.Query()
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Username,
                candidate.AvatarUrl,
                candidate.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Results.NotFound(new { Message = "User was not found." });
        }

        var publicListsCount = await listRepository.Query()
            .AsNoTracking()
            .CountAsync(list => list.UserId == userId && list.IsPublic, cancellationToken);

        var approvedReviewsCount = await reviewRepository.Query()
            .AsNoTracking()
            .CountAsync(review => review.UserId == userId && !review.IsDeleted && review.Status == ReviewStatus.Approved, cancellationToken);

        return Results.Ok(new PublicUserProfileResponse(
            user.Id,
            user.Username,
            user.AvatarUrl,
            user.CreatedAt,
            publicListsCount,
            approvedReviewsCount));
    }

    public static async Task<IResult> GetPublicMovieListsAsync(
        Guid userId,
        IRepository<User> userRepository,
        IRepository<UserMovieList> listRepository,
        CancellationToken cancellationToken)
    {
        var userExists = await userRepository.Query().AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken);

        if (!userExists)
        {
            return Results.NotFound(new { Message = "User was not found." });
        }

        var lists = await listRepository.Query()
            .AsNoTracking()
            .Where(list => list.UserId == userId && list.IsPublic)
            .OrderByDescending(list => list.UpdatedAt ?? list.CreatedAt)
            .Select(list => new MovieListSummaryResponse(
                list.Id,
                list.Name,
                list.Description,
                list.IsPublic,
                list.Items.Count(item => !item.Movie.IsDeleted),
                list.CreatedAt,
                list.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(lists);
    }

    public static async Task<IResult> GetPublicMovieListAsync(
        Guid userId,
        Guid listId,
        IRepository<UserMovieList> listRepository,
        CancellationToken cancellationToken)
    {
        var list = await listRepository.Query()
            .AsNoTracking()
            .Where(candidate => candidate.Id == listId && candidate.UserId == userId && candidate.IsPublic)
            .Select(candidate => new MovieListDetailsResponse(
                candidate.Id,
                candidate.Name,
                candidate.Description,
                candidate.IsPublic,
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

    public static async Task<IResult> GetPublicReviewsAsync(
        Guid userId,
        IRepository<User> userRepository,
        IRepository<Review> reviewRepository,
        CancellationToken cancellationToken)
    {
        var userExists = await userRepository.Query().AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken);

        if (!userExists)
        {
            return Results.NotFound(new { Message = "User was not found." });
        }

        var reviews = await reviewRepository.Query()
            .AsNoTracking()
            .Where(review =>
                review.UserId == userId
                && !review.IsDeleted
                && review.Status == ReviewStatus.Approved
                && !review.Movie.IsDeleted)
            .OrderByDescending(review => review.CreatedAt)
            .Select(review => new PublicUserReviewResponse(
                review.Id,
                review.MovieId,
                review.Movie.Title,
                review.Movie.PosterUrl,
                review.Movie.ContentType,
                review.Movie.ReleaseYear,
                review.Content,
                review.IsSpoiler,
                review.LikesCount,
                review.CreatedAt,
                review.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(reviews);
    }

    public static async Task<IResult> GetStatsAsync(
        ClaimsPrincipal principal,
        IRepository<WatchlistItem> watchlistRepository,
        IRepository<Rating> ratingRepository,
        IRepository<Review> reviewRepository,
        IRepository<Comment> commentRepository,
        IRepository<ReviewLike> reviewLikeRepository,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var watchlistCount = await watchlistRepository.Query().CountAsync(item => item.UserId == userId && item.Status == WatchlistStatus.Watchlist, cancellationToken);
        var watchedCount = await watchlistRepository.Query().CountAsync(item => item.UserId == userId && item.Status == WatchlistStatus.Watched, cancellationToken);
        var ratings = ratingRepository.Query().AsNoTracking().Where(rating => rating.UserId == userId);
        var ratingsCount = await ratings.CountAsync(cancellationToken);
        var averageScore = ratingsCount == 0 ? 0 : Math.Round(await ratings.AverageAsync(rating => rating.Score, cancellationToken), 1);
        var reviewsCount = await reviewRepository.Query().CountAsync(review => review.UserId == userId && !review.IsDeleted, cancellationToken);
        var approvedReviewsCount = await reviewRepository.Query().CountAsync(review => review.UserId == userId && !review.IsDeleted && review.Status == ReviewStatus.Approved, cancellationToken);
        var commentsCount = await commentRepository.Query().CountAsync(comment => comment.UserId == userId && !comment.IsDeleted, cancellationToken);
        var likedReviewsCount = await reviewLikeRepository.Query().CountAsync(like => like.UserId == userId, cancellationToken);

        var favoriteGenres = await ratingRepository.Query()
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

    public static async Task<IResult> GetWatchlistAsync(
        ClaimsPrincipal principal,
        IRepository<WatchlistItem> watchlistRepository,
        WatchlistStatus? status,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var query = watchlistRepository.Query()
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

    public static async Task<IResult> GetMovieStateAsync(
        Guid movieId,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<WatchlistItem> watchlistRepository,
        IRepository<Rating> ratingRepository,
        IRepository<UserMovieListItem> listItemRepository,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var movieExists = await movieRepository.Query().AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted, cancellationToken);

        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var watchlistItem = await watchlistRepository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.MovieId == movieId, cancellationToken);

        var rating = await ratingRepository.Query()
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId && candidate.MovieId == movieId)
            .Select(candidate => new UserRatingResponse(candidate.Id, candidate.Score))
            .FirstOrDefaultAsync(cancellationToken);

        var listMemberships = await listItemRepository.Query()
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

    public static async Task<IResult> GetMovieListsAsync(
        ClaimsPrincipal principal,
        IRepository<UserMovieList> listRepository,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var lists = await listRepository.Query()
            .AsNoTracking()
            .Where(list => list.UserId == userId)
            .OrderByDescending(list => list.UpdatedAt ?? list.CreatedAt)
            .Select(list => new MovieListSummaryResponse(
                list.Id,
                list.Name,
                list.Description,
                list.IsPublic,
                list.Items.Count,
                list.CreatedAt,
                list.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(lists);
    }

    public static async Task<IResult> GetMovieListAsync(
        Guid listId,
        ClaimsPrincipal principal,
        IRepository<UserMovieList> listRepository,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var list = await listRepository.Query()
            .AsNoTracking()
            .Where(candidate => candidate.Id == listId && candidate.UserId == userId)
            .Select(candidate => new MovieListDetailsResponse(
                candidate.Id,
                candidate.Name,
                candidate.Description,
                candidate.IsPublic,
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

    public static async Task<IResult> CreateMovieListAsync(
        MovieListUpsertRequest request,
        ClaimsPrincipal principal,
        IRepository<UserMovieList> listRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMovieList(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var userId = GetUserId(principal);
        var name = request.Name.Trim();
        var exists = await listRepository.Query()
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
            IsPublic = request.IsPublic,
            CreatedAt = DateTimeOffset.UtcNow
        };

        listRepository.Add(list);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/profile/lists/{list.Id}", new MovieListSummaryResponse(
            list.Id,
            list.Name,
            list.Description,
            list.IsPublic,
            0,
            list.CreatedAt,
            list.UpdatedAt));
    }

    public static async Task<IResult> UpdateMovieListAsync(
        Guid listId,
        MovieListUpsertRequest request,
        ClaimsPrincipal principal,
        IRepository<UserMovieList> listRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMovieList(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var userId = GetUserId(principal);
        var list = await listRepository.Query()
            .FirstOrDefaultAsync(candidate => candidate.Id == listId && candidate.UserId == userId, cancellationToken);

        if (list is null)
        {
            return Results.NotFound(new { Message = "List was not found." });
        }

        var name = request.Name.Trim();
        var nameExists = await listRepository.Query()
            .AnyAsync(candidate => candidate.UserId == userId && candidate.Id != listId && candidate.Name == name, cancellationToken);

        if (nameExists)
        {
            return Results.Conflict(new { Message = "List name already exists." });
        }

        list.Name = name;
        list.Description = TrimOrNull(request.Description);
        list.IsPublic = request.IsPublic;
        list.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    public static async Task<IResult> DeleteMovieListAsync(
        Guid listId,
        ClaimsPrincipal principal,
        IRepository<UserMovieList> listRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var list = await listRepository.Query()
            .FirstOrDefaultAsync(candidate => candidate.Id == listId && candidate.UserId == userId, cancellationToken);

        if (list is null)
        {
            return Results.NoContent();
        }

        listRepository.Remove(list);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    public static async Task<IResult> AddMovieListItemAsync(
        Guid listId,
        MovieListItemRequest request,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<UserMovieList> listRepository,
        IRepository<UserMovieListItem> listItemRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var list = await listRepository.Query()
            .FirstOrDefaultAsync(candidate => candidate.Id == listId && candidate.UserId == userId, cancellationToken);

        if (list is null)
        {
            return Results.NotFound(new { Message = "List was not found." });
        }

        var movieExists = await movieRepository.Query().AnyAsync(movie => movie.Id == request.MovieId && !movie.IsDeleted, cancellationToken);
        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var existing = await listItemRepository.Query()
            .FirstOrDefaultAsync(candidate => candidate.UserMovieListId == listId && candidate.MovieId == request.MovieId, cancellationToken);

        if (existing is null)
        {
            listItemRepository.Add(new UserMovieListItem
            {
                Id = Guid.NewGuid(),
                UserMovieListId = listId,
                MovieId = request.MovieId,
                AddedAt = DateTimeOffset.UtcNow
            });

            list.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    public static async Task<IResult> DeleteMovieListItemAsync(
        Guid listId,
        Guid movieId,
        ClaimsPrincipal principal,
        IRepository<UserMovieListItem> listItemRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var item = await listItemRepository.Query()
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
        listItemRepository.Remove(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    public static Task<IResult> UpdateWatchlistAsync(
        Guid movieId,
        WatchlistUpsertRequest request,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<WatchlistItem> watchlistRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        return SaveWatchlistAsync(movieId, request.Status, principal, movieRepository, watchlistRepository, unitOfWork, cancellationToken);
    }

    public static Task<IResult> UpsertWatchlistAsync(
        WatchlistUpsertRequest request,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<WatchlistItem> watchlistRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        return SaveWatchlistAsync(request.MovieId, request.Status, principal, movieRepository, watchlistRepository, unitOfWork, cancellationToken);
    }

    private static async Task<IResult> SaveWatchlistAsync(
        Guid movieId,
        WatchlistStatus status,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<WatchlistItem> watchlistRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var movieExists = await movieRepository.Query().AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted, cancellationToken);

        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var userId = GetUserId(principal);
        var item = await watchlistRepository.Query()
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
            watchlistRepository.Add(item);
        }
        else
        {
            item.Status = status;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await watchlistRepository.Query()
            .AsNoTracking()
            .Include(candidate => candidate.Movie)
                .ThenInclude(movie => movie.Genres)
            .Where(candidate => candidate.Id == item.Id)
            .Select(candidate => ToWatchlistItemResponse(candidate))
            .FirstAsync(cancellationToken);

        return Results.Ok(response);
    }

    public static async Task<IResult> DeleteWatchlistAsync(
        Guid movieId,
        ClaimsPrincipal principal,
        IRepository<WatchlistItem> watchlistRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var item = await watchlistRepository.Query()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.MovieId == movieId, cancellationToken);

        if (item is null)
        {
            return Results.NoContent();
        }

        watchlistRepository.Remove(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);

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

public sealed record PublicUserProfileResponse(
    Guid Id,
    string Username,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    int PublicListsCount,
    int ApprovedReviewsCount);

public sealed record PublicUserReviewResponse(
    Guid Id,
    Guid MovieId,
    string MovieTitle,
    string? MoviePosterUrl,
    ContentType ContentType,
    int ReleaseYear,
    string Content,
    bool IsSpoiler,
    int LikesCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

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

public sealed record MovieListUpsertRequest(string Name, string? Description, bool IsPublic = false);

public sealed record MovieListItemRequest(Guid MovieId);

public sealed record MovieListSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsPublic,
    int ItemsCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record MovieListDetailsResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsPublic,
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
