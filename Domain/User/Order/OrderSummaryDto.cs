namespace SubashaVentures.Domain.Order;

using SubashaVentures.Models.Supabase;

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
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Supabase OrderModel to OrderSummaryDto
    /// </summary>
    public static OrderSummaryDto FromCloudModel(OrderModel model, int itemCount = 0)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        return new OrderSummaryDto
        {
            Id = model.Id,
            OrderNumber = model.OrderNumber,
            CreatedAt = model.CreatedAt,
            Status = Enum.Parse<OrderStatus>(model.Status, true),
            Total = model.Total,
            ItemCount = itemCount,
            TrackingNumber = model.TrackingNumber
        };
    }
    
    /// <summary>
    /// Convert list of OrderModels to list of OrderSummaryDtos
    /// </summary>
    public static List<OrderSummaryDto> FromCloudModels(IEnumerable<(OrderModel order, int itemCount)> models)
    {
        if (models == null)
            return new List<OrderSummaryDto>();
            
        return models.Select(m => FromCloudModel(m.order, m.itemCount)).ToList();
    }
}
