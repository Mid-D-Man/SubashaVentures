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
        _database          = database;
        _supabaseClient    = supabaseClient;
        _logger            = logger;
    }

    private async Task<bool> EnsureAdminAccessAsync()
    {
        if (await _permissionService.IsSuperiorAdminAsync()) return true;
        _logger.LogWarning("Unauthorized access attempt to UserSegmentationService");
        return false;
    }

    // ── Fetch all active non-deleted users (base query used by most methods) ──

    private async Task<List<UserModel>> FetchBaseUsersAsync()
    {
        // Use .Filter() with string operators throughout — the Postgrest C# client
        // cannot parse bare boolean expressions or negations in .Where() lambdas.
        var result = await _supabaseClient
            .From<UserModel>()
            .Filter("is_deleted",      Constants.Operator.Equals, "false")
            .Filter("account_status",  Constants.Operator.Equals, "Active")
            .Get();

        return result?.Models ?? new List<UserModel>();
    }

    // ── Public methods ────────────────────────────────────────────────────────

    public async Task<List<UserProfileViewModel>> GetUsersBySpendingRangeAsync(
        decimal minSpent, decimal? maxSpent = null)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var users = await FetchBaseUsersAsync();

            var filtered = users.Where(u => u.TotalSpent >= minSpent);
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

            var users = await FetchBaseUsersAsync();

            var filtered = users.Where(u => u.LoyaltyPoints >= minPoints);
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

            var result = await _supabaseClient
                .From<UserModel>()
                .Filter("is_deleted",      Constants.Operator.Equals, "false")
                .Filter("account_status",  Constants.Operator.Equals, "Active")
                .Filter("membership_tier", Constants.Operator.Equals, tier.ToString())
                .Get();

            return result?.Models?
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList() ?? new();
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

            var carts = await _supabaseClient.From<CartModel>().Get();
            if (carts?.Models == null || !carts.Models.Any()) return new();

            var userIdsWithCart = carts.Models
                .Where(c => c.Items != null && c.Items.Any())
                .Select(c => c.UserId)
                .ToHashSet();

            if (!userIdsWithCart.Any()) return new();

            var users = await FetchBaseUsersAsync();
            return users
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

            var wishlists = await _supabaseClient.From<WishlistModel>().Get();
            if (wishlists?.Models == null || !wishlists.Models.Any()) return new();

            var userIdsWithWishlist = wishlists.Models
                .Where(w => w.Items != null && w.Items.Any())
                .Select(w => w.UserId)
                .ToHashSet();

            if (!userIdsWithWishlist.Any()) return new();

            var users = await FetchBaseUsersAsync();
            return users
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
            var users  = await FetchBaseUsersAsync();

            return users
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
            var users  = await FetchBaseUsersAsync();

            return users
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

            var users    = await FetchBaseUsersAsync();
            var filtered = users.Where(u => u.TotalOrders >= minOrders);
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

            var carts     = await _supabaseClient.From<CartModel>().Get();
            var wishlists = await _supabaseClient.From<WishlistModel>().Get();

            var interested = new HashSet<string>();

            if (carts?.Models != null)
                foreach (var cart in carts.Models)
                    if (cart.Items?.Any(i => i.product_id == productId) == true)
                        interested.Add(cart.UserId);

            if (wishlists?.Models != null)
                foreach (var w in wishlists.Models)
                    if (w.Items?.Any(i => i.product_id == productId) == true)
                        interested.Add(w.UserId);

            if (!interested.Any()) return new();

            var users = await FetchBaseUsersAsync();
            return users
                .Where(u => interested.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetUsersInterestedInProduct: {productId}");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetUsersByPurchasedCategoryAsync(string category)
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            // Fetch orders that contain items in this category and get their user IDs
            var orders = await _supabaseClient.From<OrderModel>().Get();
            if (orders?.Models == null || !orders.Models.Any()) return new();

            var userIds = orders.Models
                .Select(o => o.UserId.ToString())
                .Distinct()
                .ToHashSet();

            if (!userIds.Any()) return new();

            var users = await FetchBaseUsersAsync();
            return users
                .Where(u => userIds.Contains(u.Id))
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetUsersByPurchasedCategory: {category}");
            return new();
        }
    }

    public async Task<List<UserProfileViewModel>> GetPartnerUsersAsync()
    {
        try
        {
            if (!await EnsureAdminAccessAsync()) return new();

            var result = await _supabaseClient
                .From<UserModel>()
                .Filter("is_deleted",  Constants.Operator.Equals, "false")
                .Filter("is_partner",  Constants.Operator.Equals, "true")
                .Get();

            return result?.Models?
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList() ?? new();
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

            var result = await _supabaseClient
                .From<UserModel>()
                .Filter("is_deleted",     Constants.Operator.Equals, "false")
                .Filter("account_status", Constants.Operator.Equals, "Active")
                .Filter("is_partner",     Constants.Operator.Equals, "false")
                .Get();

            return result?.Models?
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList() ?? new();
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

            // Build server-side filters first to reduce payload
            var query = _supabaseClient.From<UserModel>();

            // Always exclude deleted unless explicitly asked not to
            if (criteria.ExcludeDeleted != false)
                query = query.Filter("is_deleted", Constants.Operator.Equals, "false");

            if (criteria.ActiveOnly == true)
                query = query.Filter("account_status", Constants.Operator.Equals, "Active");

            if (criteria.IsEmailVerified.HasValue)
                query = query.Filter("is_email_verified", Constants.Operator.Equals,
                    criteria.IsEmailVerified.Value ? "true" : "false");

            if (criteria.EmailNotificationsEnabled.HasValue)
                query = query.Filter("email_notifications", Constants.Operator.Equals,
                    criteria.EmailNotificationsEnabled.Value ? "true" : "false");

            if (criteria.IsPartner.HasValue)
                query = query.Filter("is_partner", Constants.Operator.Equals,
                    criteria.IsPartner.Value ? "true" : "false");

            if (criteria.MembershipTiers?.Count == 1)
                query = query.Filter("membership_tier", Constants.Operator.Equals,
                    criteria.MembershipTiers[0].ToString());

            var result = await query.Get();
            if (result?.Models == null || !result.Models.Any()) return new();

            // Client-side filters for things the Postgrest client can't do server-side
            var filtered = result.Models.AsEnumerable();

            if (criteria.MinSpent.HasValue)
                filtered = filtered.Where(u => u.TotalSpent >= criteria.MinSpent.Value);

            if (criteria.MaxSpent.HasValue)
                filtered = filtered.Where(u => u.TotalSpent <= criteria.MaxSpent.Value);

            if (criteria.MinLoyaltyPoints.HasValue)
                filtered = filtered.Where(u => u.LoyaltyPoints >= criteria.MinLoyaltyPoints.Value);

            if (criteria.MaxLoyaltyPoints.HasValue)
                filtered = filtered.Where(u => u.LoyaltyPoints <= criteria.MaxLoyaltyPoints.Value);

            // Multi-tier filter (server-side only handles single tier)
            if (criteria.MembershipTiers?.Count > 1)
            {
                var tierStrings = criteria.MembershipTiers.Select(t => t.ToString()).ToHashSet();
                filtered = filtered.Where(u => tierStrings.Contains(u.MembershipTier));
            }

            if (criteria.MinOrders.HasValue)
                filtered = filtered.Where(u => u.TotalOrders >= criteria.MinOrders.Value);

            if (criteria.MaxOrders.HasValue)
                filtered = filtered.Where(u => u.TotalOrders <= criteria.MaxOrders.Value);

            if (criteria.CreatedAfter.HasValue)
                filtered = filtered.Where(u => u.CreatedAt >= criteria.CreatedAfter.Value);

            if (criteria.CreatedBefore.HasValue)
                filtered = filtered.Where(u => u.CreatedAt <= criteria.CreatedBefore.Value);

            if (criteria.InactiveDays.HasValue)
            {
                var cutoff = DateTime.UtcNow.AddDays(-criteria.InactiveDays.Value);
                filtered = filtered.Where(u =>
                    u.LastLoginAt.HasValue && u.LastLoginAt.Value < cutoff);
            }

            var finalList = filtered.Select(UserProfileViewModel.FromCloudModel).ToList();

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
