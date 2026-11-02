using System.Text;
using System.Text.RegularExpressions;
using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.CatalogService.Services;

/// <summary>AdminGenreResponse from the CinemaCity monolith.</summary>
public sealed record AdminGenreResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity AdminGenre.")] int Id,
    [property: System.ComponentModel.Description("Display name.")] string Name,
    [property: System.ComponentModel.Description("Stable URL slug.")] string Slug,
    [property: System.ComponentModel.Description("MoviesCount of the CinemaCity AdminGenre.")] int MoviesCount);

/// <summary>GenreUpsertRequest from the CinemaCity monolith.</summary>
public sealed record GenreUpsertRequest(
    [property: System.ComponentModel.Description("Display name.")] [property: System.ComponentModel.DataAnnotations.Required] [property: System.ComponentModel.DataAnnotations.MaxLength(100)] string Name,
    [property: System.ComponentModel.Description("Stable URL slug.")] [property: System.ComponentModel.DataAnnotations.MaxLength(100)] string? Slug);

/// <summary>AdminPersonResponse from the CinemaCity monolith.</summary>
public sealed record AdminPersonResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity AdminPerson.")] Guid Id,
    [property: System.ComponentModel.Description("Person full name.")] string FullName,
    [property: System.ComponentModel.Description("BirthDate of the CinemaCity AdminPerson.")] DateOnly? BirthDate,
    [property: System.ComponentModel.Description("BirthPlace of the CinemaCity AdminPerson.")] string? BirthPlace,
    [property: System.ComponentModel.Description("Bio of the CinemaCity AdminPerson.")] string? Bio,
    [property: System.ComponentModel.Description("PhotoUrl of the CinemaCity AdminPerson.")] string? PhotoUrl,
    [property: System.ComponentModel.Description("CreditsCount of the CinemaCity AdminPerson.")] int CreditsCount);

/// <summary>PersonUpsertRequest from the CinemaCity monolith.</summary>
public sealed record PersonUpsertRequest(
    [property: System.ComponentModel.Description("Person full name.")] [property: System.ComponentModel.DataAnnotations.Required] [property: System.ComponentModel.DataAnnotations.MaxLength(255)] string FullName,
    [property: System.ComponentModel.Description("BirthDate of the CinemaCity PersonUpsert.")] DateOnly? BirthDate,
    [property: System.ComponentModel.Description("BirthPlace of the CinemaCity PersonUpsert.")] [property: System.ComponentModel.DataAnnotations.MaxLength(255)] string? BirthPlace,
    [property: System.ComponentModel.Description("Bio of the CinemaCity PersonUpsert.")] string? Bio,
    [property: System.ComponentModel.Description("PhotoUrl of the CinemaCity PersonUpsert.")] string? PhotoUrl);

/// <summary>UploadResponse from the CinemaCity monolith.</summary>
public sealed record UploadResponse(
    [property: System.ComponentModel.Description("Path of the CinemaCity Upload.")] string Path,
    [property: System.ComponentModel.Description("Url of the CinemaCity Upload.")] string Url);
