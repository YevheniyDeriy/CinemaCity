using CinemaCity.Domain.Enums;

namespace CinemaCity.Domain.Entities;

public class WatchlistItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MovieId { get; set; }
    public WatchlistStatus Status { get; set; } = WatchlistStatus.Watchlist;
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Movie Movie { get; set; } = null!;
}
