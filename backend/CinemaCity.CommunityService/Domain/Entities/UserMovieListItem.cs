namespace CinemaCity.Domain.Entities;

public class UserMovieListItem
{
    public Guid Id { get; set; }
    public Guid UserMovieListId { get; set; }
    public Guid MovieId { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    public UserMovieList UserMovieList { get; set; } = null!;
    public Movie Movie { get; set; } = null!;
}
