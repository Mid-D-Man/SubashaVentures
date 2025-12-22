
// Services/Wishlist/IWishlistService.cs
using SubashaVentures.Models.Supabase;


namespace SubashaVentures.Services.Wishlist;


public interface IWishlistService
{
    /// <summary>
    /// Get all wishlist items for current user
    /// </summary>
    Task<List<WishlistModel>> GetUserWishlistAsync(string userId);
    
    /// <summary>
    /// Check if product is in user's wishlist
    /// </summary>
    Task<bool> IsInWishlistAsync(string userId, string productId);
    
    /// <summary>
    /// Add product to wishlist
    /// </summary>
    Task<bool> AddToWishlistAsync(string userId, string productId);
    
    /// <summary>
    /// Remove product from wishlist
    /// </summary>
    Task<bool> RemoveFromWishlistAsync(string userId, string productId);
    
    /// <summary>
    /// Toggle product in wishlist (add if not exists, remove if exists)
    /// </summary>
    Task<bool> ToggleWishlistAsync(string userId, string productId);
    
    /// <summary>
    /// Get wishlist count for user
    /// </summary>
    Task<int> GetWishlistCountAsync(string userId);
    
    /// <summary>
    /// Clear entire wishlist
    /// </summary>
    Task<bool> ClearWishlistAsync(string userId);
    
    /// <summary>
    /// Get wishlist product IDs (for quick lookup)
    /// </summary>
    Task<HashSet<string>> GetWishlistProductIdsAsync(string userId);
}
