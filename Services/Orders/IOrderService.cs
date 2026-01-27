// Services/Orders/IOrderService.cs
using SubashaVentures.Domain.Order;
using SubashaVentures.Models.Supabase;


namespace SubashaVentures.Services.Orders;


/// <summary>
/// Service for managing orders
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Check if user has purchased and received a specific product
    /// </summary>
    Task<bool> HasUserReceivedProductAsync(string userId, string productId);
    
    /// <summary>
    /// Get all orders for a user (summary view)
    /// </summary>
    Task<List<OrderSummaryDto>> GetUserOrdersAsync(string userId);
    
    /// <summary>
    /// Get all orders for a user with pagination (detailed view)
    /// </summary>
    Task<List<OrderViewModel>> GetUserOrdersAsync(string userId, int skip = 0, int take = 100);
    
    /// <summary>
    /// Get order by ID
    /// </summary>
    Task<OrderViewModel?> GetOrderByIdAsync(string orderId);
    
    /// <summary>
    /// Get order by order number
    /// </summary>
    Task<OrderViewModel?> GetOrderByNumberAsync(string orderNumber);
    
    /// <summary>
    /// Get all orders (admin)
    /// </summary>
    Task<List<OrderSummaryDto>> GetAllOrdersAsync(int skip = 0, int take = 100);
    
    /// <summary>
    /// Get orders by status
    /// </summary>
    Task<List<OrderSummaryDto>> GetOrdersByStatusAsync(OrderStatus status);
    
    /// <summary>
    /// Create a new order
    /// </summary>
    Task<string> CreateOrderAsync(CreateOrderRequest request);
    
    /// <summary>
    /// Update order status
    /// </summary>
    Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus, string? notes = null);
    
    /// <summary>
    /// Cancel order
    /// </summary>
    Task<bool> CancelOrderAsync(string orderId, string cancellationReason);
    
    /// <summary>
    /// Get order statistics
    /// </summary>
    Task<OrderStatistics> GetOrderStatisticsAsync();
}


public class CreateOrderRequest
{
    public string UserId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public List<OrderItemRequest> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string ShippingAddressId { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingMethod { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
}


public class OrderItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
}


public class OrderStatistics
{
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ProcessingOrders { get; set; }
    public int ShippedOrders { get; set; }
    public int DeliveredOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    
    public string DisplayTotalRevenue => $"₦{TotalRevenue:N0}";
    public string DisplayAverageOrderValue => $"₦{AverageOrderValue:N0}";
}