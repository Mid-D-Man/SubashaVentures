using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;


/// <summary>
/// Order item model - UPDATED to use UUID for id and order_id
/// </summary>
[Table("order_items")]
public class OrderItemModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }
    
    [Column("order_id")]
    public Guid OrderId { get; set; }
    
    [Column("product_id")]
    public string ProductId { get; set; } = string.Empty;
    
    // Product snapshot (for historical accuracy)
    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;
    
    [Column("product_sku")]
    public string ProductSku { get; set; } = string.Empty;
    
    [Column("image_url")]
    public string ImageUrl { get; set; } = string.Empty;
    
    // Purchase details
    [Column("price")]
    public decimal Price { get; set; }
    
    [Column("quantity")]
    public int Quantity { get; set; }
    
    [Column("size")]
    public string? Size { get; set; }
    
    [Column("color")]
    public string? Color { get; set; }
    
    [Column("subtotal")]
    public decimal Subtotal { get; set; }
    
    // ISecureEntity
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