using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
using JsonPropertyName = Newtonsoft.Json.JsonPropertyAttribute;
namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Product model with proper variant structure
/// IMPORTANT: Variants JSONB is the single source of truth
/// - sizes/colors arrays are AUTO-POPULATED from variants by database trigger
/// - stock is AUTO-CALCULATED from variants by database trigger
/// </summary>
[Table("products")]
public class ProductModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int Id { get; set; }
    
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;
    
    [Column("description")]
    public string Description { get; set; } = string.Empty;
    
    [Column("long_description")]
    public string LongDescription { get; set; } = string.Empty;
    
    // ==================== PARTNERSHIP ====================
    
    [Column("is_owned_by_store")]
    public bool IsOwnedByStore { get; set; } = true;
    
    [Column("partner_id")]
    public Guid? PartnerId { get; set; }
    
    // ==================== PRICING ====================
    
    [Column("price")]
    public decimal Price { get; set; }
    
    [Column("original_price")]
    public decimal? OriginalPrice { get; set; }
    
    [Column("is_on_sale")]
    public bool IsOnSale { get; set; }
    
    [Column("discount")]
    public int Discount { get; set; }
    
    // ==================== MEDIA ====================
    
    [Column("images")]
    public List<string> Images { get; set; } = new();
    
    [Column("video_url")]
    public string? VideoUrl { get; set; }
    
    // ==================== VARIANTS (SINGLE SOURCE OF TRUTH) ====================
    
    /// <summary>
    /// Variant information stored as JSONB
    /// This is the ONLY place you should manage variants
    /// sizes/colors/stock are auto-populated from this by triggers
    /// </summary>
    [Column("variants")]
    public Dictionary<string, ProductVariant> Variants { get; set; } = new();
    
    // ==================== AUTO-POPULATED (READ-ONLY) ====================
    
    [Column("sizes")]
    public List<string> Sizes { get; set; } = new();
    
    [Column("colors")]
    public List<string> Colors { get; set; } = new();
    
    [Column("stock")]
    public int Stock { get; set; }
    
    // ==================== BASE SHIPPING INFO ====================
    
    [Column("base_weight")]
    public decimal BaseWeight { get; set; } = 1.0m;
    
    [Column("base_shipping_cost")]
    public decimal BaseShippingCost { get; set; } = 2000m;
    
    [Column("has_free_shipping")]
    public bool HasFreeShipping { get; set; } = false;
    
    // ==================== INVENTORY ====================
    
    [Column("sku")]
    public string Sku { get; set; } = string.Empty;
    
    // ==================== CLASSIFICATION ====================
    
    [Column("category_id")]
    public string CategoryId { get; set; } = string.Empty;
    
    [Column("category")]
    public string Category { get; set; } = string.Empty;
    
    [Column("sub_category")]
    public string? SubCategory { get; set; }
    
    [Column("brand")]
    public string Brand { get; set; } = string.Empty;
    
    [Column("tags")]
    public List<string> Tags { get; set; } = new();
    
    // ==================== RATING & REVIEWS ====================
    
    [Column("rating")]
    public float Rating { get; set; }
    
    [Column("review_count")]
    public int ReviewCount { get; set; }
    
    // ==================== ANALYTICS ====================
    
    [Column("view_count")]
    public int ViewCount { get; set; }
    
    [Column("click_count")]
    public int ClickCount { get; set; }
    
    [Column("add_to_cart_count")]
    public int AddToCartCount { get; set; }
    
    [Column("purchase_count")]
    public int PurchaseCount { get; set; }
    
    [Column("sales_count")]
    public int SalesCount { get; set; }
    
    [Column("total_revenue")]
    public decimal TotalRevenue { get; set; }
    
    // ==================== METADATA ====================
    
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    [Column("is_featured")]
    public bool IsFeatured { get; set; }
    
    [Column("last_viewed_at")]
    public DateTime? LastViewedAt { get; set; }
    
    [Column("last_purchased_at")]
    public DateTime? LastPurchasedAt { get; set; }
    
    // ==================== AUDIT FIELDS ====================
    
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

/// <summary>
/// Complete variant information stored in products.variants JSONB
/// </summary>
public class ProductVariant
{
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;
    
    [JsonPropertyName("size")]
    public string? Size { get; set; }
    
    [JsonPropertyName("color")]
    public string? Color { get; set; }
    
    [JsonPropertyName("color_hex")]
    public string? ColorHex { get; set; }
    
    [JsonPropertyName("stock")]
    public int Stock { get; set; }
    
    [JsonPropertyName("price_adjustment")]
    public decimal PriceAdjustment { get; set; } = 0m;
    
    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }
    
    [JsonPropertyName("length")]
    public decimal? Length { get; set; }
    
    [JsonPropertyName("width")]
    public decimal? Width { get; set; }
    
    [JsonPropertyName("height")]
    public decimal? Height { get; set; }
    
    [JsonPropertyName("shipping_cost")]
    public decimal? ShippingCost { get; set; }
    
    [JsonPropertyName("has_free_shipping")]
    public bool HasFreeShipping { get; set; } = false;
    
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }
    
    [JsonPropertyName("is_available")]
    public bool IsAvailable { get; set; } = true;
    
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

public static class ProductModelExtensions
{
    public static decimal GetVariantPrice(this ProductModel product, string? variantKey)
    {
        if (string.IsNullOrEmpty(variantKey))
            return product.Price;
            
        if (product.Variants.TryGetValue(variantKey, out var variant))
        {
            return product.Price + variant.PriceAdjustment;
        }
        
        return product.Price;
    }
    
    public static decimal GetVariantShippingCost(this ProductModel product, string? variantKey)
    {
        if (!string.IsNullOrEmpty(variantKey) && 
            product.Variants.TryGetValue(variantKey, out var variant))
        {
            if (variant.HasFreeShipping)
                return 0m;
                
            return variant.ShippingCost ?? product.BaseShippingCost;
        }
        
        return product.HasFreeShipping ? 0m : product.BaseShippingCost;
    }
    
    public static decimal GetVariantWeight(this ProductModel product, string? variantKey)
    {
        if (!string.IsNullOrEmpty(variantKey) && 
            product.Variants.TryGetValue(variantKey, out var variant) &&
            variant.Weight.HasValue)
        {
            return variant.Weight.Value;
        }
        
        return product.BaseWeight;
    }
    
    public static (decimal length, decimal width, decimal height) GetVariantDimensions(
        this ProductModel product, string? variantKey)
    {
        if (!string.IsNullOrEmpty(variantKey) && 
            product.Variants.TryGetValue(variantKey, out var variant))
        {
            return (
                variant.Length ?? 30m,
                variant.Width ?? 20m,
                variant.Height ?? 10m
            );
        }
        
        return (30m, 20m, 10m);
    }
    
    /// <summary>
    /// Get stock for a specific variant or total product stock
    /// ✅ FIXED: Now handles missing variants correctly
    /// </summary>
    public static int GetVariantStock(this ProductModel product, string? variantKey)
    {
        // If no variant key provided, return total product stock
        if (string.IsNullOrEmpty(variantKey))
            return product.Stock;
        
        // Try to find the specific variant
        if (product.Variants.TryGetValue(variantKey, out var variant))
        {
            return variant.Stock;
        }
        
        // ✅ FIX: If variant key provided but not found, check if product has any variants
        // If product has variants but this specific one doesn't exist, it's truly unavailable
        if (product.Variants.Any())
        {
            // Variant was specified but doesn't exist - return 0
            return 0;
        }
        
        // ✅ FIX: If product has no variants at all, fall back to total stock
        // This handles products that don't use variants
        return product.Stock;
    }
    
    public static bool IsVariantInStock(this ProductModel product, string? variantKey)
    {
        return product.GetVariantStock(variantKey) > 0;
    }
    
    public static string GetVariantImage(this ProductModel product, string? variantKey)
    {
        if (!string.IsNullOrEmpty(variantKey) && 
            product.Variants.TryGetValue(variantKey, out var variant) &&
            !string.IsNullOrEmpty(variant.ImageUrl))
        {
            return variant.ImageUrl;
        }
        
        return product.Images.FirstOrDefault() ?? string.Empty;
    }
    
    public static bool VariantHasFreeShipping(this ProductModel product, string? variantKey)
    {
        if (!string.IsNullOrEmpty(variantKey) && 
            product.Variants.TryGetValue(variantKey, out var variant))
        {
            return variant.HasFreeShipping;
        }
        
        return product.HasFreeShipping;
    }
    
    /// <summary>
    /// Build a variant key from size and color
    /// ✅ Format: "size_color" or just "size" or just "color"
    /// </summary>
    public static string BuildVariantKey(string? size, string? color)
    {
        if (string.IsNullOrEmpty(size) && string.IsNullOrEmpty(color))
            return string.Empty;
            
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(size)) parts.Add(size.Trim());
        if (!string.IsNullOrEmpty(color)) parts.Add(color.Trim());
        
        return string.Join("_", parts);
    }
    
    /// <summary>
    /// Try to find a variant key using case-insensitive search
    /// ✅ NEW: Helps handle case sensitivity issues
    /// </summary>
    public static string? FindVariantKeyCaseInsensitive(this ProductModel product, string? size, string? color)
    {
        if (string.IsNullOrEmpty(size) && string.IsNullOrEmpty(color))
            return null;
            
        // First try exact match
        var exactKey = BuildVariantKey(size, color);
        if (product.Variants.ContainsKey(exactKey))
            return exactKey;
        
        // Try case-insensitive match
        foreach (var kvp in product.Variants)
        {
            var variant = kvp.Value;
            var sizeMatches = string.IsNullOrEmpty(size) || 
                             (variant.Size?.Equals(size, StringComparison.OrdinalIgnoreCase) ?? false);
            var colorMatches = string.IsNullOrEmpty(color) || 
                              (variant.Color?.Equals(color, StringComparison.OrdinalIgnoreCase) ?? false);
            
            if (sizeMatches && colorMatches)
                return kvp.Key;
        }
        
        return null;
    }
}
