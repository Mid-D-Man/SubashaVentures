namespace SubashaVentures.Domain.User;

using SubashaVentures.Models.Supabase;

public class WishlistItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public bool IsOnSale { get; set; }
    public bool IsInStock { get; set; }
    public DateTime AddedAt { get; set; }
    
    public string DisplayPrice => $"₦{Price:N0}";
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Supabase WishlistModel to WishlistItemViewModel
    /// Note: This requires product information
    /// </summary>
    public static WishlistItemViewModel FromCloudModel(WishlistModel model, string productName, 
        string productSlug, string imageUrl, decimal price, decimal? originalPrice, 
        bool isOnSale, bool isInStock)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        return new WishlistItemViewModel
        {
            Id = model.Id.ToString(),
            ProductId = model.ProductId,
            ProductName = productName,
            ProductSlug = productSlug,
            ImageUrl = imageUrl,
            Price = price,
            OriginalPrice = originalPrice,
            IsOnSale = isOnSale,
            IsInStock = isInStock,
            AddedAt = model.CreatedAt
        };
    }
    
    /// <summary>
    /// Convert from WishlistItemViewModel to Supabase WishlistModel
    /// </summary>
    public WishlistModel ToCloudModel(string userId)
    {
        return new WishlistModel
        {
            Id = Guid.Parse(this.Id),
            UserId = userId,
            ProductId = this.ProductId,
            CreatedAt = this.AddedAt,
            CreatedBy = userId,
            IsDeleted = false
        };
    }
}
