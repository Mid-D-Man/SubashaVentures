using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Order model - UPDATED to use UUID (Guid) for id
/// </summary>
[Table("orders")]
public class OrderModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; } // ✅ MUST BE GUID, NOT STRING
    
    [Column("order_number")]
    public string OrderNumber { get; set; } = string.Empty;
    
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty; // UUID as string
    
    // Customer Info
    [Column("customer_name")]
    public string CustomerName { get; set; } = string.Empty;
    
    [Column("customer_email")]
    public string CustomerEmail { get; set; } = string.Empty;
    
    [Column("customer_phone")]
    public string CustomerPhone { get; set; } = string.Empty;
    
    // Pricing
    [Column("subtotal")]
    public decimal Subtotal { get; set; }
    
    [Column("shipping_cost")]
    public decimal ShippingCost { get; set; }
    
    [Column("discount")]
    public decimal Discount { get; set; }
    
    [Column("tax")]
    public decimal Tax { get; set; }
    
    [Column("total")]
    public decimal Total { get; set; }
    
    // Shipping
    [Column("shipping_address_id")]
    public string ShippingAddressId { get; set; } = string.Empty;
    
    [Column("shipping_address_snapshot")]
    public string ShippingAddressSnapshot { get; set; } = string.Empty;
    
    [Column("shipping_method")]
    public string ShippingMethod { get; set; } = string.Empty;
    
    [Column("tracking_number")]
    public string? TrackingNumber { get; set; }
    
    [Column("courier_name")]
    public string? CourierName { get; set; }
    
    // Payment
    [Column("payment_method")]
    public string PaymentMethod { get; set; } = string.Empty;
    
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "Pending";
    
    [Column("payment_reference")]
    public string? PaymentReference { get; set; }
    
    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }
    
    // Status
    [Column("status")]
    public string Status { get; set; } = "Pending";
    
    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }
    
    [Column("notes")]
    public string? Notes { get; set; }
    
    // Timestamps
    [Column("shipped_at")]
    public DateTime? ShippedAt { get; set; }
    
    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }
    
    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("created_by")]
    public string CreatedBy { get; set; } = string.Empty;
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
    
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
    
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }
    
    [Column("deleted_by")]
    public string? DeletedBy { get; set; }
}