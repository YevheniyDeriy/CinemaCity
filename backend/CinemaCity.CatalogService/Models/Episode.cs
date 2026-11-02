namespace CinemaCity.CatalogService.Models;

public class Episode
{
    public Guid Id { get; set; }
    public Guid SeasonId { get; set; }
    public int EpisodeNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? AirDate { get; set; }
    public int? Duration { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingsCount { get; set; }

    public Season Season { get; set; } = null!;
}
