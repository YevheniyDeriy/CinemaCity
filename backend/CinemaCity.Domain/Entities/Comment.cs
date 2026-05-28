namespace CinemaCity.Domain.Entities;

public class Comment
{
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public Review Review { get; set; } = null!;
    public User User { get; set; } = null!;
}
