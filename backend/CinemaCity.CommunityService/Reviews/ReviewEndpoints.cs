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
        routes.MapGet("/api/movies/{movieId:guid}/reviews", ReviewWorkflow.GetMovieReviewsAsync)
            .WithName("GetMovieReviews")
            .WithTags("Reviews");

        var group = routes.MapGroup("/api/reviews")
            .RequireAuthorization()
            .WithTags("Reviews");

        group.MapPost("/", ReviewWorkflow.CreateReviewAsync)
            .WithName("CreateReview");

        group.MapPut("/{reviewId:guid}", ReviewWorkflow.UpdateReviewAsync)
            .WithName("UpdateReview");

        group.MapDelete("/{reviewId:guid}", ReviewWorkflow.DeleteReviewAsync)
            .WithName("DeleteReview");

        group.MapPost("/{reviewId:guid}/like", ReviewWorkflow.LikeReviewAsync)
            .WithName("LikeReview");

        group.MapDelete("/{reviewId:guid}/like", ReviewWorkflow.UnlikeReviewAsync)
            .WithName("UnlikeReview");

        group.MapPost("/{reviewId:guid}/comments", ReviewWorkflow.CreateCommentAsync)
            .WithName("CreateReviewComment");

        routes.MapGet("/api/reviews/{reviewId:guid}/comments", ReviewWorkflow.GetReviewCommentsAsync)
            .WithName("GetReviewComments")
            .WithTags("Reviews");

        routes.MapDelete("/api/comments/{commentId:guid}", ReviewWorkflow.DeleteCommentAsync)
            .RequireAuthorization()
            .WithName("DeleteReviewComment")
            .WithTags("Reviews");

        var admin = routes.MapGroup("/api/admin/reviews")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin Reviews");

        admin.MapGet("/", ReviewWorkflow.GetAdminReviewsAsync)
            .WithName("GetAdminReviews");

        admin.MapPut("/{reviewId:guid}/approve", ReviewWorkflow.ApproveReviewAsync)
            .WithName("ApproveReview");

        return routes;
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
