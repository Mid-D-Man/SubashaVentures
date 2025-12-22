// Services/Wishlist/WishlistService.cs
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Wishlist;

public class WishlistService : IWishlistService
{
    private readonly Client _supabaseClient;
    private readonly ILogger<WishlistService> _logger;
    
    // Local cache for wishlist state (cleared on page reload)
    private Dictionary<string, HashSet<string>> _wishlistCache = new();

    public WishlistService(
        Client supabaseClient,
        ILogger<WishlistService> logger)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    public async Task<List<WishlistModel>> GetUserWishlistAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUserWishlist called with empty userId");
                return new List<WishlistModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Fetching wishlist for user: {userId}",
                LogLevel.Info
            );

            var wishlist = await _supabaseClient
                .From<WishlistModel>()
                .Where(w => w.UserId == userId)
                .Where(w => w.IsDeleted == false)
                .Order("created_at", Constants.Ordering.Descending)
                .Get();

            var items = wishlist?.Models ?? new List<WishlistModel>();

            // Update cache
            _wishlistCache[userId] = items.Select(w => w.ProductId).ToHashSet();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {items.Count} wishlist items",
                LogLevel.Info
            );

            return items;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting user wishlist");
            _logger.LogError(ex, "Failed to get wishlist for user: {UserId}", userId);
            return new List<WishlistModel>();
        }
    }

    public async Task<bool> IsInWishlistAsync(string userId, string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(productId))
            {
                return false;
            }

            // Check cache first
            if (_wishlistCache.TryGetValue(userId, out var cachedIds))
            {
                return cachedIds.Contains(productId);
            }

            // Query database
            var wishlist = await _supabaseClient
                .From<WishlistModel>()
                .Where(w => w.UserId == userId)
                .Where(w => w.ProductId == productId)
                .Where(w => w.IsDeleted == false)
                .Single();

            return wishlist != null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Checking wishlist: {productId}");
            return false;
        }
    }

    public async Task<bool> AddToWishlistAsync(string userId, string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning("AddToWishlist called with empty userId or productId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Adding to wishlist: User={userId}, Product={productId}",
                LogLevel.Info
            );

            // Check if already exists
            var existing = await _supabaseClient
                .From<WishlistModel>()
                .Where(w => w.UserId == userId)
                .Where(w => w.ProductId == productId)
                .Single();

            if (existing != null)
            {
                // If soft deleted, restore it
                if (existing.IsDeleted)
                {
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    existing.DeletedBy = null;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = userId;
                    
                    await existing.Update<WishlistModel>();
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        "✓ Restored soft-deleted wishlist item",
                        LogLevel.Info
                    );
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "Product already in wishlist",
                        LogLevel.Info
                    );
                }
                
                // Update cache
                if (!_wishlistCache.ContainsKey(userId))
                    _wishlistCache[userId] = new HashSet<string>();
                _wishlistCache[userId].Add(productId);
                
                return true;
            }

            // Create new wishlist item
            var wishlistItem = new WishlistModel
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                IsDeleted = false
            };

            var result = await _supabaseClient
                .From<WishlistModel>()
                .Insert(wishlistItem);

            if (result?.Models?.Any() == true)
            {
                // Update cache
                if (!_wishlistCache.ContainsKey(userId))
                    _wishlistCache[userId] = new HashSet<string>();
                _wishlistCache[userId].Add(productId);

                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Added to wishlist successfully",
                    LogLevel.Info
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding to wishlist");
            _logger.LogError(ex, "Failed to add to wishlist: User={UserId}, Product={ProductId}", 
                userId, productId);
            return false;
        }
    }

    public async Task<bool> RemoveFromWishlistAsync(string userId, string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning("RemoveFromWishlist called with empty userId or productId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Removing from wishlist: User={userId}, Product={productId}",
                LogLevel.Info
            );

            // Soft delete
            var existing = await _supabaseClient
                .From<WishlistModel>()
                .Where(w => w.UserId == userId)
                .Where(w => w.ProductId == productId)
                .Where(w => w.IsDeleted == false)
                .Single();

            if (existing == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Wishlist item not found",
                    LogLevel.Warning
                );
                return false;
            }

            existing.IsDeleted = true;
            existing.DeletedAt = DateTime.UtcNow;
            existing.DeletedBy = userId;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;

            await existing.Update<WishlistModel>();

            // Update cache
            if (_wishlistCache.ContainsKey(userId))
            {
                _wishlistCache[userId].Remove(productId);
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Removed from wishlist successfully",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Removing from wishlist");
            _logger.LogError(ex, "Failed to remove from wishlist: User={UserId}, Product={ProductId}", 
                userId, productId);
            return false;
        }
    }

    public async Task<bool> ToggleWishlistAsync(string userId, string productId)
    {
        try
        {
            var isInWishlist = await IsInWishlistAsync(userId, productId);

            if (isInWishlist)
            {
                return await RemoveFromWishlistAsync(userId, productId);
            }
            else
            {
                return await AddToWishlistAsync(userId, productId);
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling wishlist");
            _logger.LogError(ex, "Failed to toggle wishlist: User={UserId}, Product={ProductId}", 
                userId, productId);
            return false;
        }
    }

    public async Task<int> GetWishlistCountAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return 0;
            }

            // Check cache first
            if (_wishlistCache.TryGetValue(userId, out var cachedIds))
            {
                return cachedIds.Count;
            }

            var wishlist = await _supabaseClient
                .From<WishlistModel>()
                .Where(w => w.UserId == userId)
                .Where(w => w.IsDeleted == false)
                .Get();

            return wishlist?.Models?.Count ?? 0;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting wishlist count");
            return 0;
        }
    }

    public async Task<bool> ClearWishlistAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("ClearWishlist called with empty userId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Clearing wishlist for user: {userId}",
                LogLevel.Warning
            );

            var wishlist = await _supabaseClient
                .From<WishlistModel>()
                .Where(w => w.UserId == userId)
                .Where(w => w.IsDeleted == false)
                .Get();

            if (wishlist?.Models == null || !wishlist.Models.Any())
            {
                return true; // Already empty
            }

            // Soft delete all items
            foreach (var item in wishlist.Models)
            {
                item.IsDeleted = true;
                item.DeletedAt = DateTime.UtcNow;
                item.DeletedBy = userId;
                item.UpdatedAt = DateTime.UtcNow;
                item.UpdatedBy = userId;
                
                await item.Update<WishlistModel>();
            }

            // Clear cache
            _wishlistCache.Remove(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Cleared {wishlist.Models.Count} wishlist items",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Clearing wishlist");
            _logger.LogError(ex, "Failed to clear wishlist for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<HashSet<string>> GetWishlistProductIdsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new HashSet<string>();
            }

            // Check cache first
            if (_wishlistCache.TryGetValue(userId, out var cachedIds))
            {
                return cachedIds;
            }

            var wishlist = await GetUserWishlistAsync(userId);
            var productIds = wishlist.Select(w => w.ProductId).ToHashSet();

            // Update cache
            _wishlistCache[userId] = productIds;

            return productIds;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting wishlist product IDs");
            return new HashSet<string>();
        }
    }
}
