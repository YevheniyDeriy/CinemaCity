using System.Security.Claims;
using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.CatalogService.Services;

/// <summary>SeasonResponse from the CinemaCity monolith.</summary>
public sealed record SeasonResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity Season.")] Guid Id,
    [property: System.ComponentModel.Description("SeriesId of the CinemaCity Season.")] Guid SeriesId,
    [property: System.ComponentModel.Description("Positive season number.")] int SeasonNumber,
    [property: System.ComponentModel.Description("Movie, series or episode title.")] string? Title,
    [property: System.ComponentModel.Description("CinemaCity description.")] string? Description,
    [property: System.ComponentModel.Description("Year between 1888 and 2200.")] int? ReleaseYear,
    [property: System.ComponentModel.Description("Episodes of the CinemaCity Season.")] IReadOnlyCollection<EpisodeResponse> Episodes);

/// <summary>EpisodeResponse from the CinemaCity monolith.</summary>
public sealed record EpisodeResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity Episode.")] Guid Id,
    [property: System.ComponentModel.Description("SeasonId of the CinemaCity Episode.")] Guid SeasonId,
    [property: System.ComponentModel.Description("Positive episode number.")] int EpisodeNumber,
    [property: System.ComponentModel.Description("Movie, series or episode title.")] string Title,
    [property: System.ComponentModel.Description("CinemaCity description.")] string? Description,
    [property: System.ComponentModel.Description("AirDate of the CinemaCity Episode.")] DateOnly? AirDate,
    [property: System.ComponentModel.Description("Running time in minutes.")] int? Duration,
    [property: System.ComponentModel.Description("AverageRating of the CinemaCity Episode.")] decimal AverageRating,
    [property: System.ComponentModel.Description("RatingsCount of the CinemaCity Episode.")] int RatingsCount,
    [property: System.ComponentModel.Description("UserRating of the CinemaCity Episode.")] EpisodeUserRatingResponse? UserRating);

/// <summary>EpisodeUserRatingResponse from the CinemaCity monolith.</summary>
public sealed record EpisodeUserRatingResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity EpisodeUserRating.")] Guid Id,
    [property: System.ComponentModel.Description("Score of the CinemaCity EpisodeUserRating.")] decimal Score);

/// <summary>SeasonUpsertRequest from the CinemaCity monolith.</summary>
public sealed record SeasonUpsertRequest(
    [property: System.ComponentModel.Description("Positive season number.")] [property: System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int SeasonNumber,
    [property: System.ComponentModel.Description("Movie, series or episode title.")] [property: System.ComponentModel.DataAnnotations.MaxLength(255)] string? Title,
    [property: System.ComponentModel.Description("CinemaCity description.")] string? Description,
    [property: System.ComponentModel.Description("Year between 1888 and 2200.")] int? ReleaseYear);

/// <summary>EpisodeUpsertRequest from the CinemaCity monolith.</summary>
public sealed record EpisodeUpsertRequest(
    [property: System.ComponentModel.Description("Positive episode number.")] [property: System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int EpisodeNumber,
    [property: System.ComponentModel.Description("Movie, series or episode title.")] [property: System.ComponentModel.DataAnnotations.Required] [property: System.ComponentModel.DataAnnotations.MaxLength(255)] string Title,
    [property: System.ComponentModel.Description("CinemaCity description.")] [property: System.ComponentModel.DataAnnotations.MaxLength(800)] string? Description,
    [property: System.ComponentModel.Description("AirDate of the CinemaCity EpisodeUpsert.")] DateOnly? AirDate,
    [property: System.ComponentModel.Description("Running time in minutes.")] int? Duration);
