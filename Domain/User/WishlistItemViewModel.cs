namespace SubashaVentures.Domain.User;

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
}
