// Services/Users/UserSegmentationService.cs
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Users;

public class UserSegmentationService : IUserSegmentationService
{
    private readonly IPermissionService       _permissionService;
    private readonly ISupabaseDatabaseService _database;
    private readonly ILogger<UserSegmentationService> _logger;

    public UserSegmentationService(
        IPermissionService       permissionService,
        ISupabaseDatabaseService database,
        ILogger<UserSegmentationService> logger)
    {
        _permissionService = permissionService;
        _database          = database;
        _logger            = logger;
    }

    // ── Access guard ──────────────────────────────────────────────────────────

    private async Task<bool> EnsureAdminAccessAsync()
    {
        if (await _permissionService.IsSuperiorAdminAsync()) return true;
        _logger.LogWarning("Unauthorized access attempt to UserSegmentationService");
        return false;
    }

    // ── Base user fetch (all, including inactive — callers filter further) ────

    private async Task<List<UserModel>> FetchAllUsersAsync()
    {
        var result = await _database.GetAllAsync<UserModel>();
        return result.ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IEnumerable<UserModel> ActiveNonDeleted(IEnumerable<UserModel> src) =>
        src.Where(u => !u.IsDeleted && u.AccountStatus == "Active");

    // ── Public methods ────────────────────────────────────────────────────────

    public async Task<List<UserProfileViewModel>> GetUsersBySpendingRangeAsync(
        decimal minSpent, decimal? maxSpent = null)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var all      = await FetchAllUsersAsync();
            var filtered = ActiveNonDeleted(all).Where(u => u.TotalSpent >= minSpent);
            if (maxSpent.HasValue)
                filtered = filtered.Where(u => u.TotalSpent <= maxSpent.Value);

            return filtered.Select(UserProfileViewModel.FromCloudModel).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUsersBySpendingRange");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByLoyaltyPointsAsync(
        int minPoints, int? maxPoints = null)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var all      = await FetchAllUsersAsync();
            var filtered = ActiveNonDeleted(all).Where(u => u.LoyaltyPoints >= minPoints);
            if (maxPoints.HasValue)
                filtered = filtered.Where(u => u.LoyaltyPoints <= maxPoints.Value);

            return filtered.Select(UserProfileViewModel.FromCloudModel).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUsersByLoyaltyPoints");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByMembershipTierAsync(MembershipTier tier)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var all = await FetchAllUsersAsync();
            return ActiveNonDeleted(all)
                .Where(u => u.MembershipTier.Equals(tier.ToString(),
                    StringComparison.OrdinalIgnoreCase))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUsersByMembershipTier");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersWithItemsInCartAsync()
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var carts = await _database.GetAllAsync<CartModel>();
            var userIdsWithCart = carts
                .Where(c => c.Items != null && c.Items.Any())
                .Select(c => c.UserId)
                .ToHashSet();

            if (!userIdsWithCart.Any()) return new();

            var all = await FetchAllUsersAsync();
            return ActiveNonDeleted(all)
                .Where(u => userIdsWithCart.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUsersWithItemsInCart");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersWithItemsInWishlistAsync()
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var wishlists = await _database.GetAllAsync<WishlistModel>();
            var userIdsWithWishlist = wishlists
                .Where(w => w.Items != null && w.Items.Any())
                .Select(w => w.UserId)
                .ToHashSet();

            if (!userIdsWithWishlist.Any()) return new();

            var all = await FetchAllUsersAsync();
            return ActiveNonDeleted(all)
                .Where(u => userIdsWithWishlist.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUsersWithItemsInWishlist");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetInactiveUsersAsync(int inactiveDays)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var cutoff = DateTime.UtcNow.AddDays(-inactiveDays);
            var all    = await FetchAllUsersAsync();
            return ActiveNonDeleted(all)
                .Where(u => u.LastLoginAt.HasValue && u.LastLoginAt.Value < cutoff)
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetInactiveUsers");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetNewUsersAsync(int daysAgo)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var cutoff = DateTime.UtcNow.AddDays(-daysAgo);
            var all    = await FetchAllUsersAsync();
            return ActiveNonDeleted(all)
                .Where(u => u.CreatedAt >= cutoff)
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetNewUsers");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByOrderCountAsync(
        int minOrders, int? maxOrders = null)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var all      = await FetchAllUsersAsync();
            var filtered = ActiveNonDeleted(all).Where(u => u.TotalOrders >= minOrders);
            if (maxOrders.HasValue)
                filtered = filtered.Where(u => u.TotalOrders <= maxOrders.Value);

            return filtered.Select(UserProfileViewModel.FromCloudModel).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUsersByOrderCount");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersInterestedInProductAsync(string productId)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var carts     = await _database.GetAllAsync<CartModel>();
            var wishlists = await _database.GetAllAsync<WishlistModel>();

            var interested = new HashSet<string>();

            foreach (var cart in carts)
                if (cart.Items?.Any(i => i.product_id == productId) == true)
                    interested.Add(cart.UserId);

            foreach (var w in wishlists)
                if (w.Items?.Any(i => i.product_id == productId) == true)
                    interested.Add(w.UserId);

            if (!interested.Any()) return new();

            var all = await FetchAllUsersAsync();
            return ActiveNonDeleted(all)
                .Where(u => interested.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"GetUsersInterestedInProduct: {productId}");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByPurchasedCategoryAsync(string category)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            // Category-level filtering requires joining order items; use order model as proxy
            var orders = await _database.GetAllAsync<OrderModel>();
            var userIds = orders
                .Select(o => o.UserId.ToString())
                .Distinct()
                .ToHashSet();

            if (!userIds.Any()) return new();

            var all = await FetchAllUsersAsync();
            return ActiveNonDeleted(all)
                .Where(u => userIds.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"GetUsersByPurchasedCategory: {category}");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetPartnerUsersAsync()
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var all = await FetchAllUsersAsync();
            return all
                .Where(u => !u.IsDeleted && u.IsPartner)
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetPartnerUsers");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetNonPartnerUsersAsync()
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var all = await FetchAllUsersAsync();
            return ActiveNonDeleted(all)
                .Where(u => !u.IsPartner)
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetNonPartnerUsers");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByMultipleCriteriaAsync(
        UserSegmentationCriteria criteria)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            await MID_HelperFunctions.DebugMessageAsync(
                "Segmenting users with multiple criteria", LogLevel.Info);

            // Fetch everything once; filter entirely in memory to avoid
            // the IPostgrestTable ↔ ISupabaseTable type-mismatch compiler error
            // that occurs when chaining .Filter() calls on the raw Supabase client.
            var all = await FetchAllUsersAsync();

            IEnumerable<UserModel> filtered = all;

            // Deleted
            if (criteria.ExcludeDeleted != false)
                filtered = filtered.Where(u => !u.IsDeleted);

            // Active status
            if (criteria.ActiveOnly == true)
                filtered = filtered.Where(u => u.AccountStatus == "Active");

            // Email verified
            if (criteria.IsEmailVerified.HasValue)
                filtered = filtered.Where(u =>
                    u.IsEmailVerified == criteria.IsEmailVerified.Value);

            // Email notifications
            if (criteria.EmailNotificationsEnabled.HasValue)
                filtered = filtered.Where(u =>
                    u.EmailNotifications == criteria.EmailNotificationsEnabled.Value);

            // Partner flag
            if (criteria.IsPartner.HasValue)
                filtered = filtered.Where(u => u.IsPartner == criteria.IsPartner.Value);

            // Membership tiers
            if (criteria.MembershipTiers?.Any() == true)
            {
                var tierStrings = criteria.MembershipTiers
                    .Select(t => t.ToString())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                filtered = filtered.Where(u => tierStrings.Contains(u.MembershipTier));
            }

            // Spending
            if (criteria.MinSpent.HasValue)
                filtered = filtered.Where(u => u.TotalSpent >= criteria.MinSpent.Value);
            if (criteria.MaxSpent.HasValue)
                filtered = filtered.Where(u => u.TotalSpent <= criteria.MaxSpent.Value);

            // Loyalty points
            if (criteria.MinLoyaltyPoints.HasValue)
                filtered = filtered.Where(u => u.LoyaltyPoints >= criteria.MinLoyaltyPoints.Value);
            if (criteria.MaxLoyaltyPoints.HasValue)
                filtered = filtered.Where(u => u.LoyaltyPoints <= criteria.MaxLoyaltyPoints.Value);

            // Orders
            if (criteria.MinOrders.HasValue)
                filtered = filtered.Where(u => u.TotalOrders >= criteria.MinOrders.Value);
            if (criteria.MaxOrders.HasValue)
                filtered = filtered.Where(u => u.TotalOrders <= criteria.MaxOrders.Value);

            // Created date range
            if (criteria.CreatedAfter.HasValue)
                filtered = filtered.Where(u => u.CreatedAt >= criteria.CreatedAfter.Value);
            if (criteria.CreatedBefore.HasValue)
                filtered = filtered.Where(u => u.CreatedAt <= criteria.CreatedBefore.Value);

            // Inactivity
            if (criteria.InactiveDays.HasValue)
            {
                var cutoff = DateTime.UtcNow.AddDays(-criteria.InactiveDays.Value);
                filtered = filtered.Where(u =>
                    u.LastLoginAt.HasValue && u.LastLoginAt.Value < cutoff);
            }

            var finalList = filtered
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Segmentation complete: {finalList.Count} users matched", LogLevel.Info);

            return finalList;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUsersByMultipleCriteria");
            return new();
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUserIdsByCriteria");
            return new();
        }
    }
}
