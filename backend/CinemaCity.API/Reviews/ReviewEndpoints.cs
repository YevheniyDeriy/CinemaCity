using System.Security.Claims;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
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

        admin.MapPut("/{reviewId:guid}/reject", RejectReviewAsync)
            .WithName("RejectReview");

        return routes;
    }

    private static async Task<IResult> GetMovieReviewsAsync(Guid movieId, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var movieExists = await dbContext.Movies
            .AsNoTracking()
            .AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted, cancellationToken);

        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var reviews = await dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.User)
            .Where(review =>
                review.MovieId == movieId
                && !review.IsDeleted
                && review.Status == ReviewStatus.Approved)
            .OrderByDescending(review => review.CreatedAt)
            .Select(review => ToResponse(review))
            .ToListAsync(cancellationToken);

        return Results.Ok(reviews);
    }

    private static async Task<IResult> CreateReviewAsync(
        ReviewCreateRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateReview(request.Content);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var movieExists = await dbContext.Movies
            .AnyAsync(movie => movie.Id == request.MovieId && !movie.IsDeleted, cancellationToken);

        if (!movieExists)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var userId = GetUserId(principal);
        var duplicate = await dbContext.Reviews
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

        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadReviewResponseAsync(dbContext, review.Id, cancellationToken);

        return Results.Created($"/api/reviews/{review.Id}", response);
    }

    private static async Task<IResult> UpdateReviewAsync(
        Guid reviewId,
        ReviewUpdateRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateReview(request.Content);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var review = await dbContext.Reviews.FirstOrDefaultAsync(candidate => candidate.Id == reviewId, cancellationToken);

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

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadReviewResponseAsync(dbContext, review.Id, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> DeleteReviewAsync(
        Guid reviewId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var review = await dbContext.Reviews.FirstOrDefaultAsync(candidate => candidate.Id == reviewId, cancellationToken);

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

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> LikeReviewAsync(
        Guid reviewId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var review = await dbContext.Reviews
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == reviewId
                && !candidate.IsDeleted
                && candidate.Status == ReviewStatus.Approved,
                cancellationToken);

        if (review is null)
        {
            return Results.NotFound(new { Message = "Review was not found." });
        }

        var exists = await dbContext.ReviewLikes
            .AnyAsync(candidate => candidate.ReviewId == reviewId && candidate.UserId == userId, cancellationToken);

        if (!exists)
        {
            dbContext.ReviewLikes.Add(new ReviewLike
            {
                UserId = userId,
                ReviewId = reviewId,
                CreatedAt = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await RecalculateReviewLikesAsync(dbContext, reviewId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var response = await LoadReviewResponseAsync(dbContext, reviewId, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> UnlikeReviewAsync(
        Guid reviewId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var like = await dbContext.ReviewLikes
            .FirstOrDefaultAsync(candidate => candidate.ReviewId == reviewId && candidate.UserId == userId, cancellationToken);

        if (like is null)
        {
            return Results.NoContent();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.ReviewLikes.Remove(like);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateReviewLikesAsync(dbContext, reviewId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetReviewCommentsAsync(
        Guid reviewId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var reviewExists = await dbContext.Reviews
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

        var comments = await dbContext.Comments
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
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateComment(request.Content);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var reviewExists = await dbContext.Reviews
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

        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await dbContext.Comments
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
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var isAdmin = IsAdmin(principal);
        var comment = await dbContext.Comments.FirstOrDefaultAsync(candidate => candidate.Id == commentId, cancellationToken);

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
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetAdminReviewsAsync(
        AppDbContext dbContext,
        ReviewStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.User)
            .Where(review => !review.IsDeleted);

        if (status is not null)
        {
            query = query.Where(review => review.Status == status);
        }

        var reviews = await query
            .OrderBy(review => review.Status)
            .ThenByDescending(review => review.CreatedAt)
            .Select(review => ToResponse(review))
            .ToListAsync(cancellationToken);

        return Results.Ok(reviews);
    }

    private static Task<IResult> ApproveReviewAsync(
        Guid reviewId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return SetReviewStatusAsync(reviewId, ReviewStatus.Approved, null, dbContext, cancellationToken);
    }

    private static Task<IResult> RejectReviewAsync(
        Guid reviewId,
        ReviewModerationRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return SetReviewStatusAsync(reviewId, ReviewStatus.Rejected, TrimOrNull(request.RejectionReason), dbContext, cancellationToken);
    }

    private static async Task<IResult> SetReviewStatusAsync(
        Guid reviewId,
        ReviewStatus status,
        string? rejectionReason,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var review = await dbContext.Reviews.FirstOrDefaultAsync(candidate => candidate.Id == reviewId, cancellationToken);

        if (review is null || review.IsDeleted)
        {
            return Results.NotFound(new { Message = "Review was not found." });
        }

        review.Status = status;
        review.RejectionReason = status == ReviewStatus.Rejected ? rejectionReason : null;
        review.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadReviewResponseAsync(dbContext, review.Id, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<ReviewResponse> LoadReviewResponseAsync(
        AppDbContext dbContext,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.User)
            .Where(review => review.Id == reviewId)
            .Select(review => ToResponse(review))
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

    private static async Task RecalculateReviewLikesAsync(AppDbContext dbContext, Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await dbContext.Reviews.FirstAsync(candidate => candidate.Id == reviewId, cancellationToken);
        review.LikesCount = await dbContext.ReviewLikes.CountAsync(like => like.ReviewId == reviewId, cancellationToken);
        review.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private static bool IsAdmin(ClaimsPrincipal principal)
    {
        return string.Equals(principal.FindFirstValue(ClaimTypes.Role), UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ReviewResponse ToResponse(Review review)
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

public sealed record ReviewModerationRequest(string? RejectionReason);

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
