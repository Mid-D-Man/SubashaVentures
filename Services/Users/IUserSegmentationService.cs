// Services/Users/IUserSegmentationService.cs
using SubashaVentures.Domain.User;

namespace SubashaVentures.Services.Users;

public interface IUserSegmentationService
{
    Task<List<UserProfileViewModel>> GetUsersBySpendingRangeAsync(decimal minSpent, decimal? maxSpent = null);
    Task<List<UserProfileViewModel>> GetUsersByLoyaltyPointsAsync(int minPoints, int? maxPoints = null);
    Task<List<UserProfileViewModel>> GetUsersByMembershipTierAsync(MembershipTier tier);
    Task<List<UserProfileViewModel>> GetUsersWithItemsInCartAsync();
    Task<List<UserProfileViewModel>> GetUsersWithItemsInWishlistAsync();
    Task<List<UserProfileViewModel>> GetInactiveUsersAsync(int inactiveDays);
    Task<List<UserProfileViewModel>> GetNewUsersAsync(int daysAgo);
    Task<List<UserProfileViewModel>> GetUsersByOrderCountAsync(int minOrders, int? maxOrders = null);
    Task<List<UserProfileViewModel>> GetUsersInterestedInProductAsync(string productId);
    Task<List<UserProfileViewModel>> GetUsersByPurchasedCategoryAsync(string category);
    Task<List<UserProfileViewModel>> GetUsersByMultipleCriteriaAsync(UserSegmentationCriteria criteria);
    Task<List<string>> GetUserIdsByCriteriaAsync(UserSegmentationCriteria criteria);

    // Partner-specific
    Task<List<UserProfileViewModel>> GetPartnerUsersAsync();
    Task<List<UserProfileViewModel>> GetNonPartnerUsersAsync();
}

public class UserSegmentationCriteria
{
    // Spending
    public decimal? MinSpent { get; set; }
    public decimal? MaxSpent { get; set; }

    // Loyalty
    public int? MinLoyaltyPoints { get; set; }
    public int? MaxLoyaltyPoints { get; set; }

    // Tiers
    public List<MembershipTier>? MembershipTiers { get; set; }

    // Cart / wishlist
    public bool? HasCartItems { get; set; }
    public bool? HasWishlistItems { get; set; }

    // Activity
    public int? InactiveDays { get; set; }
    public int? MinOrders { get; set; }
    public int? MaxOrders { get; set; }

    // Interests
    public List<string>? InterestedProductIds { get; set; }
    public List<string>? PurchasedCategories { get; set; }

    // Dates
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }

    // Verification
    public bool? IsEmailVerified { get; set; }

    // Partner
    public bool? IsPartner { get; set; }
    public bool? EmailNotificationsEnabled { get; set; }
    public bool? ExcludeDeleted { get; set; } = true;
    public bool? ActiveOnly { get; set; }
}
