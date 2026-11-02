using System.Security.Claims;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Profile;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var publicGroup = routes.MapGroup("/api/users")
            .WithTags("Public Profiles");

        publicGroup.MapGet("/{userId:guid}", ProfileWorkflow.GetPublicProfileAsync)
            .WithName("GetPublicProfile");

        publicGroup.MapGet("/{userId:guid}/lists", ProfileWorkflow.GetPublicMovieListsAsync)
            .WithName("GetPublicMovieLists");

        publicGroup.MapGet("/{userId:guid}/lists/{listId:guid}", ProfileWorkflow.GetPublicMovieListAsync)
            .WithName("GetPublicMovieList");

        publicGroup.MapGet("/{userId:guid}/reviews", ProfileWorkflow.GetPublicReviewsAsync)
            .WithName("GetPublicUserReviews");

        var group = routes.MapGroup("/api/profile")
            .RequireAuthorization()
            .WithTags("Profile");

        group.MapGet("/stats", ProfileWorkflow.GetStatsAsync)
            .WithName("GetProfileStats");

        group.MapGet("/watchlist", ProfileWorkflow.GetWatchlistAsync)
            .WithName("GetProfileWatchlist");

        group.MapGet("/movies/{movieId:guid}/state", ProfileWorkflow.GetMovieStateAsync)
            .WithName("GetProfileMovieState");

        group.MapGet("/lists", ProfileWorkflow.GetMovieListsAsync)
            .WithName("GetProfileMovieLists");

        group.MapGet("/lists/{listId:guid}", ProfileWorkflow.GetMovieListAsync)
            .WithName("GetProfileMovieList");

        group.MapPost("/lists", ProfileWorkflow.CreateMovieListAsync)
            .WithName("CreateProfileMovieList");

        group.MapPut("/lists/{listId:guid}", ProfileWorkflow.UpdateMovieListAsync)
            .WithName("UpdateProfileMovieList");

        group.MapDelete("/lists/{listId:guid}", ProfileWorkflow.DeleteMovieListAsync)
            .WithName("DeleteProfileMovieList");

        group.MapPost("/lists/{listId:guid}/items", ProfileWorkflow.AddMovieListItemAsync)
            .WithName("AddProfileMovieListItem");

        group.MapDelete("/lists/{listId:guid}/items/{movieId:guid}", ProfileWorkflow.DeleteMovieListItemAsync)
            .WithName("DeleteProfileMovieListItem");

        group.MapPost("/watchlist", ProfileWorkflow.UpsertWatchlistAsync)
            .WithName("UpsertWatchlistItem");

        group.MapPut("/watchlist/{movieId:guid}", ProfileWorkflow.UpdateWatchlistAsync)
            .WithName("UpdateWatchlistItem");

        group.MapDelete("/watchlist/{movieId:guid}", ProfileWorkflow.DeleteWatchlistAsync)
            .WithName("DeleteWatchlistItem");

        return routes;
    }

}
