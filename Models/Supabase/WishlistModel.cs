// Models/Supabase/WishlistModel.cs - REDESIGNED FOR JSONB
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Wishlist model - ONE ROW PER USER with JSONB items array
/// </summary>
[Table("wishlist")]
public class WishlistModel : BaseModel
{
    [PrimaryKey("user_id", false)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [Column("items")]
    public List<WishlistItem> Items { get; set; } = new();
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Wishlist item - stored as JSON in wishlist.items
/// </summary>
public class WishlistItem
{
    public string product_id { get; set; } = string.Empty;
    public DateTime added_at { get; set; }
}