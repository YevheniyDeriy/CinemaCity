using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CinemaCity.CatalogService.Models;
using CinemaCity.CatalogService.Services;

namespace CinemaCity.CatalogService.Dtos;

/// <summary>Write fields of the original CinemaCity Movie; generated IDs, ratings and timestamps are server-owned.</summary>
public class CreateMovieDto
{
    [Required, MaxLength(255), Description("Movie or series title.")] public string Title { get; set; } = string.Empty;
    [MaxLength(255), Description("Original-language title.")] public string? OriginalTitle { get; set; }
    [EnumDataType(typeof(ContentType)), Description("Movie or Series.")] public ContentType ContentType { get; set; }
    [Range(1888, 2200), Description("Release year.")] public int ReleaseYear { get; set; }
    [Range(1, int.MaxValue), Description("Duration in minutes, if known.")] public int? Duration { get; set; }
    [Description("CinemaCity description.")] public string? Description { get; set; }
    [Description("Poster URL or uploaded asset path.")] public string? PosterUrl { get; set; }
    [Description("Backdrop URL or uploaded asset path.")] public string? BackdropUrl { get; set; }
    [MaxLength(64), Description("YouTube trailer identifier.")] public string? TrailerYoutubeId { get; set; }
    [Required, Description("Existing genre IDs.")] public int[] GenreIds { get; set; } = [];
    [Required, Description("Existing country IDs.")] public int[] CountryIds { get; set; } = [];
    [Required, Description("Existing language IDs.")] public int[] LanguageIds { get; set; } = [];
    [Required, Description("Existing people and their cast/crew credits.")] public MovieCreditDto[] Cast { get; set; } = [];
    public MovieUpsertRequest ToRequest() => new(Title, OriginalTitle, ContentType, ReleaseYear, Duration, Description, PosterUrl, BackdropUrl, TrailerYoutubeId,
        GenreIds, CountryIds, LanguageIds, Cast.Select(c => new MovieCastUpsertRequest(c.PersonId, c.CreditType, c.RoleName, c.SortOrder)).ToArray());
}

/// <summary>Complete replacement of the editable fields of a CinemaCity movie or series.</summary>
public sealed class UpdateMovieDto : CreateMovieDto;

/// <summary>A cast or crew member in the original CinemaCity catalog.</summary>
public sealed class MovieCreditDto
{
    [Description("Existing person UUID.")] public Guid PersonId { get; set; }
    [EnumDataType(typeof(CreditType)), Description("Actor, director, writer, producer or other supported original credit.")] public CreditType CreditType { get; set; }
    [MaxLength(255), Description("Character or credit role.")] public string? RoleName { get; set; }
    [Range(0, int.MaxValue), Description("Nonnegative display order.")] public int SortOrder { get; set; }
}
