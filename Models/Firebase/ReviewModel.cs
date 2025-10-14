namespace SubashaVentures.Models.Firebase;

public record ReviewModel
{
    public string Id { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? UserAvatar { get; init; }
    public int Rating { get; init; }
    public string? Title { get; init; }
    public string Comment { get; init; } = string.Empty;
    public List<string> Images { get; init; } = new();
    public bool IsVerifiedPurchase { get; init; }
    public int HelpfulCount { get; init; }
    public bool IsApproved { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
