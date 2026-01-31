// Models/Supabase/ProductModel.cs - UPDATED: Removed duplicate analytics columns
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
using JsonPropertyName = Newtonsoft.Json.JsonPropertyAttribute;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Product model - ANALYTICS COLUMNS REMOVED (now in product_analytics table)
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
    
    // ==================== METADATA ====================
    
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    [Column("is_featured")]
    public bool IsFeatured { get; set; }
    
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
/// Product variant information stored in products.variants JSONB
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
    
    public static int GetVariantStock(this ProductModel product, string? variantKey)
    {
        if (string.IsNullOrEmpty(variantKey))
            return product.Stock;
        
        if (product.Variants.TryGetValue(variantKey, out var variant))
        {
            return variant.Stock;
        }
        
        return 0;
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
    
    public static string BuildVariantKey(string? size, string? color)
    {
        var normalizedSize = string.IsNullOrWhiteSpace(size) ? null : size.Trim();
        var normalizedColor = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        
        if (normalizedSize == null && normalizedColor == null)
            return string.Empty;
            
        var parts = new List<string>();
        if (normalizedSize != null) parts.Add(normalizedSize);
        if (normalizedColor != null) parts.Add(normalizedColor);
        
        return string.Join("_", parts);
    }
    
    public static bool RequiresVariantSelection(this ProductModel product)
    {
        return product.Variants.Any();
    }
    
    public static List<string> GetAvailableVariantKeys(this ProductModel product)
    {
        return product.Variants.Keys.ToList();
    }
    
    public static (bool isValid, string? errorMessage) ValidateVariantSelection(
        this ProductModel product, 
        string? size, 
        string? color)
    {
        if (!product.Variants.Any())
        {
            return (true, null);
        }
        
        var normalizedSize = string.IsNullOrWhiteSpace(size) ? null : size.Trim();
        var normalizedColor = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        
        var variantKey = BuildVariantKey(normalizedSize, normalizedColor);
        
        if (string.IsNullOrEmpty(variantKey))
        {
            var firstVariant = product.Variants.FirstOrDefault().Value;
            var requiredOptions = new List<string>();
            
            if (!string.IsNullOrEmpty(firstVariant?.Size))
                requiredOptions.Add("size");
            if (!string.IsNullOrEmpty(firstVariant?.Color))
                requiredOptions.Add("color");
            
            if (!requiredOptions.Any())
            {
                return (false, "Please select a variant for this product.");
            }
            
            var optionsText = string.Join(" and ", requiredOptions);
            return (false, $"Please select a {optionsText} for this product.");
        }
        
        if (!product.Variants.ContainsKey(variantKey))
        {
            return (false, $"The selected combination ({variantKey}) is not available.");
        }
        
        return (true, null);
    }
}
