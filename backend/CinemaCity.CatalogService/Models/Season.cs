namespace CinemaCity.CatalogService.Models;

public class Season
{
    public Guid Id { get; set; }
    public Guid SeriesId { get; set; }
    public int SeasonNumber { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? ReleaseYear { get; set; }

    public Movie Series { get; set; } = null!;
    public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
}
