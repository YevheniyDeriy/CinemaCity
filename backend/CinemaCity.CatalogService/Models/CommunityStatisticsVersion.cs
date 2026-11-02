namespace CinemaCity.CatalogService.Models;

/// <summary>Idempotency and ordering for community-owned aggregate snapshots.</summary>
public sealed class CommunityStatisticsVersion
{
    public string Kind { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public long Version { get; set; }
}
