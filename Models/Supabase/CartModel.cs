// Models/Supabase/CartModel.cs - REDESIGNED FOR JSONB
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Cart model - ONE ROW PER USER with JSONB items array
/// </summary>
[Table("cart")]
public class CartModel : BaseModel
{
    [PrimaryKey("user_id", false)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [Column("items")]
    public List<CartItem> Items { get; set; } = new();
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Cart item - stored as JSON in cart.items
/// </summary>
public class CartItem
{
    public string product_id { get; set; } = string.Empty;
    public int quantity { get; set; }
    public string? size { get; set; }
    public string? color { get; set; }
    public DateTime added_at { get; set; }
}