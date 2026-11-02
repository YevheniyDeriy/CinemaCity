namespace CinemaCity.Domain.Entities;

/// <summary>Durable delivery of community-owned rating aggregates to the catalog read model.</summary>
public sealed class CatalogStatisticsMessage
{
    public long Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingsCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}
