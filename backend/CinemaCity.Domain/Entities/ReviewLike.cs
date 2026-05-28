namespace CinemaCity.Domain.Entities;

public class ReviewLike
{
    public Guid UserId { get; set; }
    public Guid ReviewId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
    public Review Review { get; set; } = null!;
}
