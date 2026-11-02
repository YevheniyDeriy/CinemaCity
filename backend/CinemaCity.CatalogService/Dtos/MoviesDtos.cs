using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.CatalogService.Services;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total)
{
    /// <summary>Total rows matching filters before pagination.</summary>
    public int TotalCount => Total;
}

/// <summary>GenreResponse from the CinemaCity monolith.</summary>
public sealed record GenreResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity Genre.")] int Id,
    [property: System.ComponentModel.Description("Display name.")] string Name,
    [property: System.ComponentModel.Description("Stable URL slug.")] string Slug);

/// <summary>DictionaryResponse from the CinemaCity monolith.</summary>
public sealed record DictionaryResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity Dictionary.")] int Id,
    [property: System.ComponentModel.Description("Display name.")] string Name,
    [property: System.ComponentModel.Description("Code of the CinemaCity Dictionary.")] string? Code);

/// <summary>MovieCardResponse from the CinemaCity monolith.</summary>
public sealed record MovieCardResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity MovieCard.")] Guid Id,
    [property: System.ComponentModel.Description("Movie, series or episode title.")] string Title,
    [property: System.ComponentModel.Description("Original language title.")] string? OriginalTitle,
    [property: System.ComponentModel.Description("Movie or Series.")] ContentType ContentType,
    [property: System.ComponentModel.Description("Year between 1888 and 2200.")] int ReleaseYear,
    [property: System.ComponentModel.Description("Poster URL or uploaded asset path.")] string? PosterUrl,
    [property: System.ComponentModel.Description("Backdrop image URL.")] string? BackdropUrl,
    [property: System.ComponentModel.Description("AverageRating of the CinemaCity MovieCard.")] decimal AverageRating,
    [property: System.ComponentModel.Description("RatingsCount of the CinemaCity MovieCard.")] int RatingsCount,
    [property: System.ComponentModel.Description("Genres of the CinemaCity MovieCard.")] IReadOnlyCollection<GenreResponse> Genres);

/// <summary>MovieDetailsResponse from the CinemaCity monolith.</summary>
public sealed record MovieDetailsResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity MovieDetails.")] Guid Id,
    [property: System.ComponentModel.Description("Movie, series or episode title.")] string Title,
    [property: System.ComponentModel.Description("Original language title.")] string? OriginalTitle,
    [property: System.ComponentModel.Description("Movie or Series.")] ContentType ContentType,
    [property: System.ComponentModel.Description("Year between 1888 and 2200.")] int ReleaseYear,
    [property: System.ComponentModel.Description("Running time in minutes.")] int? Duration,
    [property: System.ComponentModel.Description("CinemaCity description.")] string? Description,
    [property: System.ComponentModel.Description("Poster URL or uploaded asset path.")] string? PosterUrl,
    [property: System.ComponentModel.Description("Backdrop image URL.")] string? BackdropUrl,
    [property: System.ComponentModel.Description("YouTube trailer identifier.")] string? TrailerYoutubeId,
    [property: System.ComponentModel.Description("AverageRating of the CinemaCity MovieDetails.")] decimal AverageRating,
    [property: System.ComponentModel.Description("RatingsCount of the CinemaCity MovieDetails.")] int RatingsCount,
    [property: System.ComponentModel.Description("Genres of the CinemaCity MovieDetails.")] IReadOnlyCollection<GenreResponse> Genres,
    [property: System.ComponentModel.Description("Countries of the CinemaCity MovieDetails.")] IReadOnlyCollection<DictionaryResponse> Countries,
    [property: System.ComponentModel.Description("Languages of the CinemaCity MovieDetails.")] IReadOnlyCollection<DictionaryResponse> Languages,
    [property: System.ComponentModel.Description("Existing people and their credits.")] IReadOnlyCollection<CastResponse> Cast);

/// <summary>CastResponse from the CinemaCity monolith.</summary>
public sealed record CastResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity Cast.")] Guid Id,
    [property: System.ComponentModel.Description("PersonId of the CinemaCity Cast.")] Guid PersonId,
    [property: System.ComponentModel.Description("Person full name.")] string FullName,
    [property: System.ComponentModel.Description("PhotoUrl of the CinemaCity Cast.")] string? PhotoUrl,
    [property: System.ComponentModel.Description("CreditType of the CinemaCity Cast.")] CreditType CreditType,
    [property: System.ComponentModel.Description("RoleName of the CinemaCity Cast.")] string? RoleName,
    [property: System.ComponentModel.Description("Nonnegative credit order.")] int SortOrder);

/// <summary>MovieUpsertRequest from the CinemaCity monolith.</summary>
public sealed record MovieUpsertRequest(
    [property: System.ComponentModel.Description("Movie, series or episode title.")] [property: System.ComponentModel.DataAnnotations.Required] [property: System.ComponentModel.DataAnnotations.MaxLength(255)] string Title,
    [property: System.ComponentModel.Description("Original language title.")] [property: System.ComponentModel.DataAnnotations.MaxLength(255)] string? OriginalTitle,
    [property: System.ComponentModel.Description("Movie or Series.")] [property: System.ComponentModel.DataAnnotations.EnumDataType(typeof(ContentType))] ContentType ContentType,
    [property: System.ComponentModel.Description("Year between 1888 and 2200.")] [property: System.ComponentModel.DataAnnotations.Range(1888, 2200)] int ReleaseYear,
    [property: System.ComponentModel.Description("Running time in minutes.")] int? Duration,
    [property: System.ComponentModel.Description("CinemaCity description.")] string? Description,
    [property: System.ComponentModel.Description("Poster URL or uploaded asset path.")] string? PosterUrl,
    [property: System.ComponentModel.Description("Backdrop image URL.")] string? BackdropUrl,
    [property: System.ComponentModel.Description("YouTube trailer identifier.")] [property: System.ComponentModel.DataAnnotations.MaxLength(64)] string? TrailerYoutubeId,
    [property: System.ComponentModel.Description("Existing genre IDs from the extracted catalog.")] [property: System.ComponentModel.DataAnnotations.Required] int[] GenreIds,
    [property: System.ComponentModel.Description("Existing country IDs.")] [property: System.ComponentModel.DataAnnotations.Required] int[] CountryIds,
    [property: System.ComponentModel.Description("Existing language IDs.")] [property: System.ComponentModel.DataAnnotations.Required] int[] LanguageIds,
    [property: System.ComponentModel.Description("Existing people and their credits.")] [property: System.ComponentModel.DataAnnotations.Required] MovieCastUpsertRequest[] Cast);

/// <summary>MovieCastUpsertRequest from the CinemaCity monolith.</summary>
public sealed record MovieCastUpsertRequest(
    [property: System.ComponentModel.Description("PersonId of the CinemaCity MovieCastUpsert.")] Guid PersonId,
    [property: System.ComponentModel.Description("CreditType of the CinemaCity MovieCastUpsert.")] [property: System.ComponentModel.DataAnnotations.EnumDataType(typeof(CreditType))] CreditType CreditType,
    [property: System.ComponentModel.Description("RoleName of the CinemaCity MovieCastUpsert.")] [property: System.ComponentModel.DataAnnotations.MaxLength(255)] string? RoleName,
    [property: System.ComponentModel.Description("Nonnegative credit order.")] [property: System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)] int SortOrder);
