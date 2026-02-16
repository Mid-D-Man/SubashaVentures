// Services/Users/UserSegmentationService.cs
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;

namespace SubashaVentures.Services.Users;

public class UserSegmentationService : IUserSegmentationService
{
    private readonly IPermissionService _permissionService;
    private readonly ISupabaseDatabaseService _database;
    private readonly Client _supabaseClient;
    private readonly ILogger<UserSegmentationService> _logger;

    public UserSegmentationService(
        IPermissionService permissionService,
        ISupabaseDatabaseService database,
        Client supabaseClient,
        ILogger<UserSegmentationService> logger)
    {
        _permissionService = permissionService;
        _database = database;
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    private async Task<bool> EnsureAdminAccessAsync()
    {
        var isSuperiorAdmin = await _permissionService.IsSuperiorAdminAsync();
        if (!isSuperiorAdmin)
        {
            _logger.LogWarning("Unauthorized access attempt to UserSegmentationService");
            return false;
        }
        return true;
    }

    public async Task<List<UserProfileViewModel>> GetUsersBySpendingRangeAsync(decimal minSpent, decimal? maxSpent = null)
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Segmenting users by spending: min={minSpent}, max={maxSpent}",
                LogLevel.Info
            );

            var query = _supabaseClient
                .From<UserModel>()
                .Where(u => u.TotalSpent >= minSpent);

            if (maxSpent.HasValue)
            {
                query = query.Where(u => u.TotalSpent <= maxSpent.Value);
            }

            var users = await query.Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Segmenting users by spending");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByLoyaltyPointsAsync(int minPoints, int? maxPoints = null)
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            var query = _supabaseClient
                .From<UserModel>()
                .Where(u => u.LoyaltyPoints >= minPoints);

            if (maxPoints.HasValue)
            {
                query = query.Where(u => u.LoyaltyPoints <= maxPoints.Value);
            }

            var users = await query.Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Segmenting users by loyalty points");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByMembershipTierAsync(MembershipTier tier)
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            var users = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.MembershipTier == tier.ToString())
                .Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Segmenting users by tier");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersWithItemsInCartAsync()
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            var carts = await _supabaseClient
                .From<CartModel>()
                .Get();

            if (carts?.Models == null || !carts.Models.Any())
                return new List<UserProfileViewModel>();

            var userIdsWithCart = carts.Models
                .Where(c => c.Items != null && c.Items.Any())
                .Select(c => c.UserId)
                .ToList();

            if (!userIdsWithCart.Any())
                return new List<UserProfileViewModel>();

            var users = await _supabaseClient
                .From<UserModel>()
                .Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Where(u => userIdsWithCart.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting users with cart items");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersWithItemsInWishlistAsync()
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            var wishlists = await _supabaseClient
                .From<WishlistModel>()
                .Get();

            if (wishlists?.Models == null || !wishlists.Models.Any())
                return new List<UserProfileViewModel>();

            var userIdsWithWishlist = wishlists.Models
                .Where(w => w.Items != null && w.Items.Any())
                .Select(w => w.UserId)
                .ToList();

            if (!userIdsWithWishlist.Any())
                return new List<UserProfileViewModel>();

            var users = await _supabaseClient
                .From<UserModel>()
                .Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Where(u => userIdsWithWishlist.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting users with wishlist items");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetInactiveUsersAsync(int inactiveDays)
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            var cutoffDate = DateTime.UtcNow.AddDays(-inactiveDays);

            var users = await _supabaseClient
                .From<UserModel>()
                .Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Where(u => u.LastLoginAt.HasValue && u.LastLoginAt.Value < cutoffDate)
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting inactive users");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetNewUsersAsync(int daysAgo)
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            var cutoffDate = DateTime.UtcNow.AddDays(-daysAgo);

            var users = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.CreatedAt >= cutoffDate)
                .Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting new users");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByOrderCountAsync(int minOrders, int? maxOrders = null)
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            var query = _supabaseClient
                .From<UserModel>()
                .Where(u => u.TotalOrders >= minOrders);

            if (maxOrders.HasValue)
            {
                query = query.Where(u => u.TotalOrders <= maxOrders.Value);
            }

            var users = await query.Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Segmenting users by order count");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersInterestedInProductAsync(string productId)
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            var carts = await _supabaseClient
                .From<CartModel>()
                .Get();

            var wishlists = await _supabaseClient
                .From<WishlistModel>()
                .Get();

            var userIdsWithProduct = new HashSet<string>();

            if (carts?.Models != null)
            {
                foreach (var cart in carts.Models)
                {
                    if (cart.Items?.Any(i => i.product_id == productId) == true)
                    {
                        userIdsWithProduct.Add(cart.UserId);
                    }
                }
            }

            if (wishlists?.Models != null)
            {
                foreach (var wishlist in wishlists.Models)
                {
                    if (wishlist.Items?.Any(i => i.product_id == productId) == true)
                    {
                        userIdsWithProduct.Add(wishlist.UserId);
                    }
                }
            }

            if (!userIdsWithProduct.Any())
                return new List<UserProfileViewModel>();

            var users = await _supabaseClient
                .From<UserModel>()
                .Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Where(u => userIdsWithProduct.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting users interested in product: {productId}");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByPurchasedCategoryAsync(string category)
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            var orders = await _supabaseClient
                .From<OrderModel>()
                .Get();

            if (orders?.Models == null || !orders.Models.Any())
                return new List<UserProfileViewModel>();

            var userIds = orders.Models
                .Select(o => o.UserId.ToString())
                .Distinct()
                .ToList();

            if (!userIds.Any())
                return new List<UserProfileViewModel>();

            var users = await _supabaseClient
                .From<UserModel>()
                .Get();

            if (users?.Models == null || !users.Models.Any())
                return new List<UserProfileViewModel>();

            return users.Models
                .Where(u => userIds.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting users by category: {category}");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByMultipleCriteriaAsync(UserSegmentationCriteria criteria)
    {
        try
        {
            if (!await EnsureAdminAccessAsync())
                return new List<UserProfileViewModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                "Segmenting users with multiple criteria",
                LogLevel.Info
            );

            var allUsers = await _supabaseClient
                .From<UserModel>()
                .Get();

            if (allUsers?.Models == null || !allUsers.Models.Any())
                return new List<UserProfileViewModel>();

            var filteredUsers = allUsers.Models.AsEnumerable();

            if (criteria.MinSpent.HasValue)
                filteredUsers = filteredUsers.Where(u => u.TotalSpent >= criteria.MinSpent.Value);

            if (criteria.MaxSpent.HasValue)
                filteredUsers = filteredUsers.Where(u => u.TotalSpent <= criteria.MaxSpent.Value);

            if (criteria.MinLoyaltyPoints.HasValue)
                filteredUsers = filteredUsers.Where(u => u.LoyaltyPoints >= criteria.MinLoyaltyPoints.Value);

            if (criteria.MaxLoyaltyPoints.HasValue)
                filteredUsers = filteredUsers.Where(u => u.LoyaltyPoints <= criteria.MaxLoyaltyPoints.Value);

            if (criteria.MembershipTiers != null && criteria.MembershipTiers.Any())
            {
                var tierStrings = criteria.MembershipTiers.Select(t => t.ToString()).ToList();
                filteredUsers = filteredUsers.Where(u => tierStrings.Contains(u.MembershipTier));
            }

            if (criteria.MinOrders.HasValue)
                filteredUsers = filteredUsers.Where(u => u.TotalOrders >= criteria.MinOrders.Value);

            if (criteria.MaxOrders.HasValue)
                filteredUsers = filteredUsers.Where(u => u.TotalOrders <= criteria.MaxOrders.Value);

            if (criteria.CreatedAfter.HasValue)
                filteredUsers = filteredUsers.Where(u => u.CreatedAt >= criteria.CreatedAfter.Value);

            if (criteria.CreatedBefore.HasValue)
                filteredUsers = filteredUsers.Where(u => u.CreatedAt <= criteria.CreatedBefore.Value);

            if (criteria.IsEmailVerified.HasValue)
                filteredUsers = filteredUsers.Where(u => u.IsEmailVerified == criteria.IsEmailVerified.Value);

            var result = filteredUsers
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Segmentation complete: {result.Count} users matched",
                LogLevel.Info
            );

            return result;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Multi-criteria segmentation");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<List<string>> GetUserIdsByCriteriaAsync(UserSegmentationCriteria criteria)
    {
        try
        {
            var users = await GetUsersByMultipleCriteriaAsync(criteria);
            return users.Select(u => u.Id).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting user IDs by criteria");
            return new List<string>();
        }
    }
}
