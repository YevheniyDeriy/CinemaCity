using CinemaCity.CatalogService.Models;

namespace CinemaCity.CatalogService.Models;

public class MovieCast
{
    public Guid Id { get; set; }
    public Guid MovieId { get; set; }
    public Guid PersonId { get; set; }
    public CreditType CreditType { get; set; }
    public string? RoleName { get; set; }
    public int SortOrder { get; set; }

    public Movie Movie { get; set; } = null!;
    public Person Person { get; set; } = null!;
}
