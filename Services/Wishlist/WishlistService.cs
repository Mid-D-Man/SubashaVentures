// Services/Wishlist/WishlistService.cs - UPDATED FOR JSONB DESIGN
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Wishlist;

public class WishlistService : IWishlistService
{
    private readonly Client _supabaseClient;
    private readonly ILogger<WishlistService> _logger;
    
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
                $"💝 Fetching wishlist for user: {userId}",
                LogLevel.Info
            );

            var wishlist = await _supabaseClient
                .From<WishlistModel>()
                .Where(w => w.UserId == userId)
                .Single();

            if (wishlist == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No wishlist found, returning empty",
                    LogLevel.Info
                );
                return new List<WishlistModel>();
            }

            // Update cache
            _wishlistCache[userId] = wishlist.Items.Select(i => i.product_id).ToHashSet();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Retrieved wishlist with {wishlist.Items.Count} items",
                LogLevel.Info
            );

            return new List<WishlistModel> { wishlist };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting user wishlist");
            _logger.LogError(ex, "❌ Failed to get wishlist for user: {UserId}", userId);
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

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Checking if product {productId} is in wishlist for user {userId}",
                LogLevel.Debug
            );

            // Check cache first
            if (_wishlistCache.TryGetValue(userId, out var cachedIds))
            {
                var inCache = cachedIds.Contains(productId);
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📦 Cache hit: {inCache}",
                    LogLevel.Debug
                );
                return inCache;
            }

            // Query database
            var wishlist = await _supabaseClient
                .From<WishlistModel>()
                .Where(w => w.UserId == userId)
                .Single();

            var exists = wishlist?.Items.Any(i => i.product_id == productId) == true;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"💾 Database query result: {exists}",
                LogLevel.Debug
            );

            return exists;
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
                _logger.LogWarning("❌ AddToWishlist called with empty userId or productId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"➕ Adding to wishlist: User={userId}, Product={productId}",
                LogLevel.Info
            );

            // Call Postgres function to add to wishlist
            var result = await _supabaseClient.Rpc<List<WishlistItem>>(
                "add_to_wishlist",
                new { p_product_id = productId }
            );

            if (result != null)
            {
                // Update cache
                if (!_wishlistCache.ContainsKey(userId))
                    _wishlistCache[userId] = new HashSet<string>();
                _wishlistCache[userId].Add(productId);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Successfully added to wishlist! Wishlist now has {result.Count} items",
                    LogLevel.Info
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding to wishlist");
            _logger.LogError(ex, "❌ Failed to add to wishlist: User={UserId}, Product={ProductId}", 
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
                _logger.LogWarning("❌ RemoveFromWishlist called with empty userId or productId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"➖ Removing from wishlist: User={userId}, Product={productId}",
                LogLevel.Info
            );

            // Call Postgres function to remove from wishlist
            var result = await _supabaseClient.Rpc<List<WishlistItem>>(
                "remove_from_wishlist",
                new { p_product_id = productId }
            );

            if (result != null)
            {
                // Update cache
                if (_wishlistCache.ContainsKey(userId))
                {
                    _wishlistCache[userId].Remove(productId);
                }

                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Successfully removed from wishlist",
                    LogLevel.Info
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Removing from wishlist");
            _logger.LogError(ex, "❌ Failed to remove from wishlist: User={UserId}, Product={ProductId}", 
                userId, productId);
            return false;
        }
    }

    public async Task<bool> ToggleWishlistAsync(string userId, string productId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔄 Toggling wishlist: User={userId}, Product={productId}",
                LogLevel.Info
            );

            var isInWishlist = await IsInWishlistAsync(userId, productId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"📊 Current state: isInWishlist={isInWishlist}",
                LogLevel.Info
            );

            if (isInWishlist)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "➖ Product is in wishlist, removing...",
                    LogLevel.Info
                );
                return await RemoveFromWishlistAsync(userId, productId);
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "➕ Product not in wishlist, adding...",
                    LogLevel.Info
                );
                return await AddToWishlistAsync(userId, productId);
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling wishlist");
            _logger.LogError(ex, "❌ Failed to toggle wishlist: User={UserId}, Product={ProductId}", 
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
                .Single();

            return wishlist?.Items.Count ?? 0;
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
                _logger.LogWarning("❌ ClearWishlist called with empty userId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"🗑️ Clearing wishlist for user: {userId}",
                LogLevel.Warning
            );

            var wishlist = await _supabaseClient
                .From<WishlistModel>()
                .Where(w => w.UserId == userId)
                .Single();

            if (wishlist == null)
            {
                return true; // Already empty
            }

            wishlist.Items = new List<WishlistItem>();
            wishlist.UpdatedAt = DateTime.UtcNow;

            await wishlist.Update<WishlistModel>();

            // Clear cache
            _wishlistCache.Remove(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Wishlist cleared",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Clearing wishlist");
            _logger.LogError(ex, "❌ Failed to clear wishlist for user: {UserId}", userId);
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

            var wishlists = await GetUserWishlistAsync(userId);
            var productIds = wishlists.FirstOrDefault()?.Items.Select(i => i.product_id).ToHashSet() 
                ?? new HashSet<string>();

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