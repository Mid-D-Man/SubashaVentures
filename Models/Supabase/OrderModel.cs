namespace SubashaVentures.Models.Supabase;

public record OrderModel : ISecureEntity
{
    public string Id { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    
    // Customer Info (denormalized for easier access)
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    
    // Pricing
    public decimal Subtotal { get; init; }
    public decimal ShippingCost { get; init; }
    public decimal Discount { get; init; }
    public decimal Tax { get; init; }
    public decimal Total { get; init; }
    
    // Shipping
    public string ShippingAddressId { get; init; } = string.Empty;
    public string ShippingAddressSnapshot { get; init; } = string.Empty; // JSON snapshot
    public string ShippingMethod { get; init; } = string.Empty;
    public string? TrackingNumber { get; init; }
    public string? CourierName { get; init; }
    
    // Payment
    public string PaymentMethod { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = "Pending";
    public string? PaymentReference { get; init; }
    public DateTime? PaidAt { get; init; }
    
    // Status
    public string Status { get; init; } = "Pending";
    public string? CancellationReason { get; init; }
    public string? Notes { get; init; }
    
    // Timestamps
    public DateTime? ShippedAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    
    // ISecureEntity
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
}
