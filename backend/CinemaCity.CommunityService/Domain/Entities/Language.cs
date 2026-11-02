namespace CinemaCity.Domain.Entities;

public class Language
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }

    public ICollection<Movie> Movies { get; set; } = new List<Movie>();
}
