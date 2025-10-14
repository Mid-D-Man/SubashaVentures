// ===== Domain/Order/OrderViewModel.cs =====
namespace SubashaVentures.Domain.Order;

public class OrderViewModel
{
    public string Id { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    
    // Customer Info
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    
    // Order Items
    public List<OrderItemViewModel> Items { get; set; } = new();
    
    // Pricing
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    
    // Shipping
    public string ShippingAddressId { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingMethod { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string? CourierName { get; set; }
    
    // Payment
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    
    // Status
    public OrderStatus Status { get; set; }
    public string? CancellationReason { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    // Computed
    public int TotalItems => Items.Sum(i => i.Quantity);
    public string DisplayTotal => $"₦{Total:N0}";
    public bool CanCancel => Status == OrderStatus.Pending || Status == OrderStatus.Processing;
    public bool CanTrack => !string.IsNullOrEmpty(TrackingNumber);
}

public class OrderItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Subtotal => Price * Quantity;
}

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    Refunded,
    Failed
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,
    PartiallyRefunded
}

public enum PaymentMethod
{
    Card,
    BankTransfer,
    USSD,
    PayOnDelivery,
    Wallet
}
