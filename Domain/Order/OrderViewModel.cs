// ===== Domain/Order/OrderViewModel.cs ===== FIXED FOR GUID
namespace SubashaVentures.Domain.Order;

using SubashaVentures.Models.Supabase;
using System.Text.Json;

public class OrderViewModel
{
    public string Id { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    
    public List<OrderItemViewModel> Items { get; set; } = new();
    
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    
    public string ShippingAddressId { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingMethod { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string? CourierName { get; set; }
    
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    
    public OrderStatus Status { get; set; }
    public string? CancellationReason { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    public int TotalItems => Items.Sum(i => i.Quantity);
    public string DisplayTotal => $"₦{Total:N0}";
    public bool CanCancel => Status == OrderStatus.Pending || Status == OrderStatus.Processing;
    public bool CanTrack => !string.IsNullOrEmpty(TrackingNumber);
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Supabase OrderModel to OrderViewModel
    /// </summary>
    public static OrderViewModel FromCloudModel(OrderModel model, List<OrderItemModel> itemModels)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        var orderItems = itemModels?.Select(OrderItemViewModel.FromCloudModel).ToList() ?? new List<OrderItemViewModel>();
            
        return new OrderViewModel
        {
            Id = model.Id.ToString(), // ✅ FIXED: Convert Guid to string
            OrderNumber = model.OrderNumber,
            UserId = model.UserId,
            CustomerName = model.CustomerName,
            CustomerEmail = model.CustomerEmail,
            CustomerPhone = model.CustomerPhone,
            Items = orderItems,
            Subtotal = model.Subtotal,
            ShippingCost = model.ShippingCost,
            Discount = model.Discount,
            Tax = model.Tax,
            Total = model.Total,
            ShippingAddressId = model.ShippingAddressId,
            ShippingAddress = model.ShippingAddressSnapshot,
            ShippingMethod = model.ShippingMethod,
            TrackingNumber = model.TrackingNumber,
            CourierName = model.CourierName,
            PaymentMethod = Enum.Parse<PaymentMethod>(model.PaymentMethod, true),
            PaymentStatus = Enum.Parse<PaymentStatus>(model.PaymentStatus, true),
            PaymentReference = model.PaymentReference,
            PaidAt = model.PaidAt,
            Status = Enum.Parse<OrderStatus>(model.Status, true),
            CancellationReason = model.CancellationReason,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            ShippedAt = model.ShippedAt,
            DeliveredAt = model.DeliveredAt,
            CancelledAt = model.CancelledAt
        };
    }
    
    /// <summary>
    /// Convert from OrderViewModel to Supabase OrderModel
    /// </summary>
    public OrderModel ToCloudModel()
    {
        return new OrderModel
        {
            Id = string.IsNullOrEmpty(this.Id) ? Guid.NewGuid() : Guid.Parse(this.Id), // ✅ FIXED: Convert string to Guid
            OrderNumber = this.OrderNumber,
            UserId = this.UserId,
            CustomerName = this.CustomerName,
            CustomerEmail = this.CustomerEmail,
            CustomerPhone = this.CustomerPhone,
            Subtotal = this.Subtotal,
            ShippingCost = this.ShippingCost,
            Discount = this.Discount,
            Tax = this.Tax,
            Total = this.Total,
            ShippingAddressId = this.ShippingAddressId,
            ShippingAddressSnapshot = this.ShippingAddress,
            ShippingMethod = this.ShippingMethod,
            TrackingNumber = this.TrackingNumber,
            CourierName = this.CourierName,
            PaymentMethod = this.PaymentMethod.ToString(),
            PaymentStatus = this.PaymentStatus.ToString(),
            PaymentReference = this.PaymentReference,
            PaidAt = this.PaidAt,
            Status = this.Status.ToString(),
            CancellationReason = this.CancellationReason,
            CreatedAt = this.CreatedAt,
            CreatedBy = this.UserId,
            UpdatedAt = this.UpdatedAt,
            ShippedAt = this.ShippedAt,
            DeliveredAt = this.DeliveredAt,
            CancelledAt = this.CancelledAt
        };
    }
}

public class OrderItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty; // ✅ ADDED: OrderId for reference
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Subtotal => Price * Quantity;
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Supabase OrderItemModel to OrderItemViewModel
    /// </summary>
    public static OrderItemViewModel FromCloudModel(OrderItemModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        return new OrderItemViewModel
        {
            Id = model.Id.ToString(), // ✅ FIXED: Convert Guid to string
            OrderId = model.OrderId.ToString(), // ✅ FIXED: Convert Guid to string
            ProductId = model.ProductId,
            ProductName = model.ProductName,
            ImageUrl = model.ImageUrl,
            Price = model.Price,
            Quantity = model.Quantity,
            Size = model.Size,
            Color = model.Color,
            Sku = model.ProductSku
        };
    }
    
    /// <summary>
    /// Convert from OrderItemViewModel to Supabase OrderItemModel
    /// </summary>
    public OrderItemModel ToCloudModel(string orderId)
    {
        return new OrderItemModel
        {
            Id = string.IsNullOrEmpty(this.Id) ? Guid.NewGuid() : Guid.Parse(this.Id), // ✅ FIXED: Convert string to Guid
            OrderId = Guid.Parse(orderId), // ✅ FIXED: Convert string to Guid
            ProductId = this.ProductId,
            ProductName = this.ProductName,
            ProductSku = this.Sku,
            ImageUrl = this.ImageUrl,
            Price = this.Price,
            Quantity = this.Quantity,
            Size = this.Size,
            Color = this.Color,
            Subtotal = this.Subtotal,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
    }
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