namespace SubashaVentures.Models.Supabase;

public record NotificationModel : ISecureEntity
{
    public string Id { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ActionUrl { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
    
    // ISecureEntity
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = "system";
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
}
