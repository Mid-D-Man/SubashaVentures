namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Audit trail for all user and admin actions
/// </summary>
public record ActivityLogModel
{
    public string Id { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty; // Order, Product, User, etc.
    public string EntityId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty; // Created, Updated, Deleted, Viewed
    public string? OldValue { get; init; } // JSON snapshot
    public string? NewValue { get; init; } // JSON snapshot
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime CreatedAt { get; init; }
}
