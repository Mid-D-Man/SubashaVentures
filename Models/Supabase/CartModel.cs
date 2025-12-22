// Models/Supabase/CartModel.cs - UPDATED
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

[Table("cart")]
public class CartModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;
    
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [Column("product_id")]
    public string ProductId { get; set; } = string.Empty;
    
    [Column("quantity")]
    public int Quantity { get; set; }
    
    [Column("size")]
    public string? Size { get; set; }
    
    [Column("color")]
    public string? Color { get; set; }
    
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
