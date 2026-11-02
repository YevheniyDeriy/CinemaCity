namespace CinemaCity.CatalogService.Models;

public class Person
{
    public Guid Id { get; set; }
    public int TmdbId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string? BirthPlace { get; set; }
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<MovieCast> Credits { get; set; } = new List<MovieCast>();
}
