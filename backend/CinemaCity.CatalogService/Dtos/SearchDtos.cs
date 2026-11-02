using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.CatalogService.Services;

/// <summary>SearchResponse from the CinemaCity monolith.</summary>
public sealed record SearchResponse(
    [property: System.ComponentModel.Description("Scope of the CinemaCity Search.")] string Scope,
    [property: System.ComponentModel.Description("Query of the CinemaCity Search.")] string Query,
    [property: System.ComponentModel.Description("Movies of the CinemaCity Search.")] IReadOnlyCollection<SearchMovieResult> Movies,
    [property: System.ComponentModel.Description("Episodes of the CinemaCity Search.")] IReadOnlyCollection<SearchEpisodeResult> Episodes,
    IReadOnlyCollection<SearchPersonResult> People)
{
    public static SearchResponse Empty(string scope)
    {
        return new SearchResponse(scope,
    string.Empty,
    [],
    [],
    []);
    }
}

/// <summary>SearchMovieResult from the CinemaCity monolith.</summary>
public sealed record SearchMovieResult(
    [property: System.ComponentModel.Description("Id of the CinemaCity SearchMovieResult.")] Guid Id,
    [property: System.ComponentModel.Description("Movie, series or episode title.")] string Title,
    [property: System.ComponentModel.Description("Original language title.")] string? OriginalTitle,
    [property: System.ComponentModel.Description("Movie or Series.")] ContentType ContentType,
    [property: System.ComponentModel.Description("Year between 1888 and 2200.")] int ReleaseYear,
    [property: System.ComponentModel.Description("Poster URL or uploaded asset path.")] string? PosterUrl,
    [property: System.ComponentModel.Description("AverageRating of the CinemaCity SearchMovieResult.")] decimal AverageRating);

/// <summary>SearchEpisodeResult from the CinemaCity monolith.</summary>
public sealed record SearchEpisodeResult(
    [property: System.ComponentModel.Description("Id of the CinemaCity SearchEpisodeResult.")] Guid Id,
    [property: System.ComponentModel.Description("Movie, series or episode title.")] string Title,
    [property: System.ComponentModel.Description("SeriesId of the CinemaCity SearchEpisodeResult.")] Guid SeriesId,
    [property: System.ComponentModel.Description("SeriesTitle of the CinemaCity SearchEpisodeResult.")] string SeriesTitle,
    [property: System.ComponentModel.Description("Positive season number.")] int SeasonNumber,
    [property: System.ComponentModel.Description("Positive episode number.")] int EpisodeNumber,
    [property: System.ComponentModel.Description("AirDate of the CinemaCity SearchEpisodeResult.")] DateOnly? AirDate,
    [property: System.ComponentModel.Description("AverageRating of the CinemaCity SearchEpisodeResult.")] decimal AverageRating);

/// <summary>SearchPersonResult from the CinemaCity monolith.</summary>
public sealed record SearchPersonResult(
    [property: System.ComponentModel.Description("Id of the CinemaCity SearchPersonResult.")] Guid Id,
    [property: System.ComponentModel.Description("Person full name.")] string FullName,
    [property: System.ComponentModel.Description("PhotoUrl of the CinemaCity SearchPersonResult.")] string? PhotoUrl,
    [property: System.ComponentModel.Description("BirthPlace of the CinemaCity SearchPersonResult.")] string? BirthPlace);
