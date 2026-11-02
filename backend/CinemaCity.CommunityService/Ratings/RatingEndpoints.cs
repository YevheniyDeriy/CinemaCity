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

        group.MapGet("/", RatingWorkflow.MyRatingsAsync).WithName("MyRatings")
            .WithSummary("Current user's published ratings; cancellation uses the existing DELETE route.")
            .Produces<RatingOperationResponse[]>(200).ProducesProblem(401);

        group.MapPost("/", RatingWorkflow.CreateRatingAsync)
            .WithName("CreateRating");

        group.MapPut("/{ratingId:guid}", RatingWorkflow.UpdateRatingAsync)
            .WithName("UpdateRating");

        group.MapDelete("/{ratingId:guid}", RatingWorkflow.DeleteRatingAsync)
            .WithName("DeleteRating");

        return routes;
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

/// <summary>A current user operation. Active means the rating remains published; DELETE cancels it.</summary>
public sealed record RatingOperationResponse(Guid Id, Guid? MovieId, Guid? EpisodeId, string Title, decimal Score, string Status, DateTimeOffset CreatedAt);
