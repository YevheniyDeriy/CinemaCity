using CinemaCity.Domain.Enums;

namespace CinemaCity.Domain.Entities;

public class Movie
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public ContentType ContentType { get; set; }
    public int ReleaseYear { get; set; }
    public int? Duration { get; set; }
    public string? Description { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? TrailerYoutubeId { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingsCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<Genre> Genres { get; set; } = new List<Genre>();
    public ICollection<Country> Countries { get; set; } = new List<Country>();
    public ICollection<Language> Languages { get; set; } = new List<Language>();
    public ICollection<MovieCast> Cast { get; set; } = new List<MovieCast>();
    public ICollection<Season> Seasons { get; set; } = new List<Season>();
    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();
}
