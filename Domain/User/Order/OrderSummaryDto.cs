namespace SubashaVentures.Domain.Order;

public class OrderSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
    public string? TrackingNumber { get; set; }
    public string DisplayTotal => $"₦{Total:N0}";
}
