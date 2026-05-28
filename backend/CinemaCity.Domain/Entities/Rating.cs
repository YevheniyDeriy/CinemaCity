namespace CinemaCity.Domain.Entities;

public class Rating
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? MovieId { get; set; }
    public Guid? EpisodeId { get; set; }
    public decimal Score { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Movie? Movie { get; set; }
    public Episode? Episode { get; set; }
}
