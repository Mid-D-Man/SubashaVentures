namespace SubashaVentures.Models.Supabase;

public record OrderItemModel : ISecureEntity
{
    public string Id { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    
    // Product snapshot (for historical accuracy)
    public string ProductName { get; init; } = string.Empty;
    public string ProductSku { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    
    // Purchase details
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public string? Size { get; init; }
    public string? Color { get; init; }
    public decimal Subtotal { get; init; }
    
    // ISecureEntity
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
}
