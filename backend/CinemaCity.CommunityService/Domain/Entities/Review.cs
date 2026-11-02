using CinemaCity.Domain.Enums;

namespace CinemaCity.Domain.Entities;

public class Review
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MovieId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsSpoiler { get; set; }
    public int LikesCount { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public string? RejectionReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Movie Movie { get; set; } = null!;
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<ReviewLike> Likes { get; set; } = new List<ReviewLike>();
}
