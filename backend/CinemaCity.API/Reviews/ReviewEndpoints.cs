using System.Security.Claims;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Reviews;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/movies/{movieId:guid}/reviews", GetMovieReviewsAsync)
            .WithName("GetMovieReviews")
            .WithTags("Reviews");

        var group = routes.MapGroup("/api/reviews")
            .RequireAuthorization()
            .WithTags("Reviews");

        group.MapPost("/", CreateReviewAsync)
            .WithName("CreateReview");

        group.MapPut("/{reviewId:guid}", UpdateReviewAsync)
            .WithName("UpdateReview");

        group.MapDelete("/{reviewId:guid}", DeleteReviewAsync)
            .WithName("DeleteReview");

        group.MapPost("/{reviewId:guid}/like", LikeReviewAsync)
            .WithName("LikeReview");

        group.MapDelete("/{reviewId:guid}/like", UnlikeReviewAsync)
            .WithName("UnlikeReview");

        group.MapPost("/{reviewId:guid}/comments", CreateCommentAsync)
            .WithName("CreateReviewComment");

        routes.MapGet("/api/reviews/{reviewId:guid}/comments", GetReviewCommentsAsync)
            .WithName("GetReviewComments")
            .WithTags("Reviews");

        routes.MapDelete("/api/comments/{commentId:guid}", DeleteCommentAsync)
            .RequireAuthorization()
            .WithName("DeleteReviewComment")
            .WithTags("Reviews");

        var admin = routes.MapGroup("/api/admin/reviews")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin Reviews");

        admin.MapGet("/", GetAdminReviewsAsync)
            .WithName("GetAdminReviews");

        admin.MapPut("/{reviewId:guid}/approve", ApproveReviewAsync)
            .WithName("ApproveReview");

        return routes;
    }

    private static async Task<IResult> GetMovieReviewsAsync(
        Guid movieId,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<Review> reviewRepository,
        CancellationToken cancellationToken)
    {
        var movieExists = await movieRepository.Query()
            .AsNoTracking()
            .AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted, cancellationToken);

        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var reviews = await reviewRepository.Query()
            .AsNoTracking()
            .Include(review => review.User)
            .Include(review => review.Likes)
            .Where(review =>
                review.MovieId == movieId
                && !review.IsDeleted
                && review.Status == ReviewStatus.Approved)
            .OrderByDescending(review => review.CreatedAt)
            .Select(review => ToResponse(review, TryGetUserId(principal)))
            .ToListAsync(cancellationToken);

        return Results.Ok(reviews);
    }

    private static async Task<IResult> CreateReviewAsync(
        ReviewCreateRequest request,
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<Review> reviewRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateReview(request.Content);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var movieExists = await movieRepository.Query()
            .AnyAsync(movie => movie.Id == request.MovieId && !movie.IsDeleted, cancellationToken);

        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var userId = GetUserId(principal);
        var duplicate = await reviewRepository.Query()
            .AnyAsync(review => review.UserId == userId && review.MovieId == request.MovieId, cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { Message = "Movie already has a review from this user." });
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = request.MovieId,
            Content = request.Content.Trim(),
            IsSpoiler = request.IsSpoiler,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        reviewRepository.Add(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await LoadReviewResponseAsync(reviewRepository, review.Id, userId, cancellationToken);

        return Results.Created($"/api/reviews/{review.Id}", response);
    }

    private static async Task<IResult> UpdateReviewAsync(
        Guid reviewId,
        ReviewUpdateRequest request,
        ClaimsPrincipal principal,
        IRepository<Review> reviewRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateReview(request.Content);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var review = await reviewRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == reviewId, cancellationToken);

        if (review is null || review.IsDeleted)
        {
            return Results.NotFound(new { Message = "Review was not found." });
        }

        if (!isAdmin && review.UserId != userId)
        {
            return Results.Forbid();
        }

        review.Content = request.Content.Trim();
        review.IsSpoiler = request.IsSpoiler;
        review.UpdatedAt = DateTimeOffset.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await LoadReviewResponseAsync(reviewRepository, review.Id, userId, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> DeleteReviewAsync(
        Guid reviewId,
        ClaimsPrincipal principal,
        IRepository<Review> reviewRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var review = await reviewRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == reviewId, cancellationToken);

        if (review is null || review.IsDeleted)
        {
            return Results.NotFound(new { Message = "Review was not found." });
        }

        if (!isAdmin && review.UserId != userId)
        {
            return Results.Forbid();
        }

        review.IsDeleted = true;
        review.DeletedAt = DateTimeOffset.UtcNow;
        review.UpdatedAt = DateTimeOffset.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> LikeReviewAsync(
        Guid reviewId,
        ClaimsPrincipal principal,
        IRepository<Review> reviewRepository,
        IRepository<ReviewLike> reviewLikeRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var review = await reviewRepository.Query()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == reviewId
                && !candidate.IsDeleted
                && candidate.Status == ReviewStatus.Approved,
                cancellationToken);

        if (review is null)
        {
            return Results.NotFound(new { Message = "Review was not found." });
        }

        var exists = await reviewLikeRepository.Query()
            .AnyAsync(candidate => candidate.ReviewId == reviewId && candidate.UserId == userId, cancellationToken);

        if (!exists)
        {
            reviewLikeRepository.Add(new ReviewLike
            {
                UserId = userId,
                ReviewId = reviewId,
                CreatedAt = DateTimeOffset.UtcNow
            });

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await RecalculateReviewLikesAsync(reviewRepository, reviewLikeRepository, unitOfWork, reviewId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var response = await LoadReviewResponseAsync(reviewRepository, reviewId, userId, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> UnlikeReviewAsync(
        Guid reviewId,
        ClaimsPrincipal principal,
        IRepository<Review> reviewRepository,
        IRepository<ReviewLike> reviewLikeRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var like = await reviewLikeRepository.Query()
            .FirstOrDefaultAsync(candidate => candidate.ReviewId == reviewId && candidate.UserId == userId, cancellationToken);

        if (like is null)
        {
            var current = await LoadReviewResponseAsync(reviewRepository, reviewId, userId, cancellationToken);
            return Results.Ok(current);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        reviewLikeRepository.Remove(like);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateReviewLikesAsync(reviewRepository, reviewLikeRepository, unitOfWork, reviewId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = await LoadReviewResponseAsync(reviewRepository, reviewId, userId, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetReviewCommentsAsync(
        Guid reviewId,
        IRepository<Review> reviewRepository,
        IRepository<Comment> commentRepository,
        CancellationToken cancellationToken)
    {
        var reviewExists = await reviewRepository.Query()
            .AsNoTracking()
            .AnyAsync(review =>
                review.Id == reviewId
                && !review.IsDeleted
                && review.Status == ReviewStatus.Approved,
                cancellationToken);

        if (!reviewExists)
        {
            return Results.NotFound(new { Message = "Review was not found." });
        }

        var comments = await commentRepository.Query()
            .AsNoTracking()
            .Include(comment => comment.User)
            .Where(comment => comment.ReviewId == reviewId && !comment.IsDeleted)
            .OrderBy(comment => comment.CreatedAt)
            .Select(comment => ToCommentResponse(comment))
            .ToListAsync(cancellationToken);

        return Results.Ok(comments);
    }

    private static async Task<IResult> CreateCommentAsync(
        Guid reviewId,
        CommentCreateRequest request,
        ClaimsPrincipal principal,
        IRepository<Review> reviewRepository,
        IRepository<Comment> commentRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateComment(request.Content);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var reviewExists = await reviewRepository.Query()
            .AnyAsync(review =>
                review.Id == reviewId
                && !review.IsDeleted
                && review.Status == ReviewStatus.Approved,
                cancellationToken);

        if (!reviewExists)
        {
            return Results.NotFound(new { Message = "Review was not found." });
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            UserId = GetUserId(principal),
            Content = request.Content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        commentRepository.Add(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await commentRepository.Query()
            .AsNoTracking()
            .Include(candidate => candidate.User)
            .Where(candidate => candidate.Id == comment.Id)
            .Select(candidate => ToCommentResponse(candidate))
            .FirstAsync(cancellationToken);

        return Results.Created($"/api/reviews/{reviewId}/comments/{comment.Id}", response);
    }

    private static async Task<IResult> DeleteCommentAsync(
        Guid commentId,
        ClaimsPrincipal principal,
        IRepository<Comment> commentRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var comment = await commentRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == commentId, cancellationToken);

        if (comment is null || comment.IsDeleted)
        {
            return Results.NotFound(new { Message = "Comment was not found." });
        }

        if (!isAdmin && comment.UserId != userId)
        {
            return Results.Forbid();
        }

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetAdminReviewsAsync(
        IRepository<Review> reviewRepository,
        ReviewStatus? status,
        CancellationToken cancellationToken)
    {
        var query = reviewRepository.Query()
            .AsNoTracking()
            .Include(review => review.User)
            .Include(review => review.Likes)
            .Where(review => !review.IsDeleted);

        if (status is not null)
        {
            query = query.Where(review => review.Status == status);
        }

        var reviews = await query
            .OrderBy(review => review.Status)
            .ThenByDescending(review => review.CreatedAt)
            .Select(review => ToResponse(review, currentUserId: null))
            .ToListAsync(cancellationToken);

        return Results.Ok(reviews);
    }

    private static Task<IResult> ApproveReviewAsync(
        Guid reviewId,
        IRepository<Review> reviewRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        return SetReviewStatusAsync(reviewId, ReviewStatus.Approved, null, reviewRepository, unitOfWork, cancellationToken);
    }

    private static async Task<IResult> SetReviewStatusAsync(
        Guid reviewId,
        ReviewStatus status,
        string? rejectionReason,
        IRepository<Review> reviewRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var review = await reviewRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == reviewId, cancellationToken);

        if (review is null || review.IsDeleted)
        {
            return Results.NotFound(new { Message = "Review was not found." });
        }

        review.Status = status;
        review.RejectionReason = status == ReviewStatus.Rejected ? rejectionReason : null;
        review.UpdatedAt = DateTimeOffset.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await LoadReviewResponseAsync(reviewRepository, review.Id, null, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<ReviewResponse> LoadReviewResponseAsync(
        IRepository<Review> reviewRepository,
        Guid reviewId,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        return await reviewRepository.Query()
            .AsNoTracking()
            .Include(review => review.User)
            .Include(review => review.Likes)
            .Where(review => review.Id == reviewId)
            .Select(review => ToResponse(review, currentUserId))
            .FirstAsync(cancellationToken);
    }

    private static string? ValidateReview(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.Length is < 10 or > 4000)
        {
            return "Review content must be between 10 and 4000 characters.";
        }

        return null;
    }

    private static string? ValidateComment(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.Length is < 2 or > 1000)
        {
            return "Comment content must be between 2 and 1000 characters.";
        }

        return null;
    }

    private static async Task RecalculateReviewLikesAsync(
        IRepository<Review> reviewRepository,
        IRepository<ReviewLike> reviewLikeRepository,
        IUnitOfWork unitOfWork,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var review = await reviewRepository.Query().FirstAsync(candidate => candidate.Id == reviewId, cancellationToken);
        review.LikesCount = await reviewLikeRepository.Query().CountAsync(like => like.ReviewId == reviewId, cancellationToken);
        review.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private static Guid? TryGetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static bool IsAdmin(ClaimsPrincipal principal)
    {
        return string.Equals(principal.FindFirstValue(ClaimTypes.Role), UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ReviewResponse ToResponse(Review review, Guid? currentUserId)
    {
        return new ReviewResponse(
            review.Id,
            review.UserId,
            review.User.Username,
            review.MovieId,
            review.Content,
            review.IsSpoiler,
            review.Status,
            review.RejectionReason,
            review.LikesCount,
            currentUserId is not null && review.Likes.Any(like => like.UserId == currentUserId),
            review.CreatedAt,
            review.UpdatedAt);
    }

    private static CommentResponse ToCommentResponse(Comment comment)
    {
        return new CommentResponse(
            comment.Id,
            comment.ReviewId,
            comment.UserId,
            comment.User.Username,
            comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt);
    }
}

public sealed record ReviewCreateRequest(Guid MovieId, string Content, bool IsSpoiler);

public sealed record ReviewUpdateRequest(string Content, bool IsSpoiler);

public sealed record CommentCreateRequest(string Content);

public sealed record ReviewResponse(
    Guid Id,
    Guid UserId,
    string Username,
    Guid MovieId,
    string Content,
    bool IsSpoiler,
    ReviewStatus Status,
    string? RejectionReason,
    int LikesCount,
    bool IsLikedByCurrentUser,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CommentResponse(
    Guid Id,
    Guid ReviewId,
    Guid UserId,
    string Username,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
