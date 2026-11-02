namespace CinemaCity.CatalogService.Models;

public class Genre
{
    public int Id { get; set; }
    public int TmdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public ICollection<Movie> Movies { get; set; } = new List<Movie>();
}
