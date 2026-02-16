// Services/Users/IUserSegmentationService.cs
using SubashaVentures.Domain.User;

namespace SubashaVentures.Services.Users;

/// <summary>
/// Service for segmenting users based on various criteria
/// ADMIN ONLY - Uses permission service to enforce access control
/// </summary>
public interface IUserSegmentationService
{
    /// <summary>
    /// Get users by total spent range
    /// </summary>
    Task<List<UserProfileViewModel>> GetUsersBySpendingRangeAsync(decimal minSpent, decimal? maxSpent = null);
    
    /// <summary>
    /// Get users by loyalty points range
    /// </summary>
    Task<List<UserProfileViewModel>> GetUsersByLoyaltyPointsAsync(int minPoints, int? maxPoints = null);
    
    /// <summary>
    /// Get users by membership tier
    /// </summary>
    Task<List<UserProfileViewModel>> GetUsersByMembershipTierAsync(MembershipTier tier);
    
    /// <summary>
    /// Get users who have items in cart
    /// </summary>
    Task<List<UserProfileViewModel>> GetUsersWithItemsInCartAsync();
    
    /// <summary>
    /// Get users who have items in wishlist
    /// </summary>
    Task<List<UserProfileViewModel>> GetUsersWithItemsInWishlistAsync();
    
    /// <summary>
    /// Get users who haven't purchased in specified days
    /// </summary>
    Task<List<UserProfileViewModel>> GetInactiveUsersAsync(int inactiveDays);
    
    /// <summary>
    /// Get users who joined recently
    /// </summary>
    Task<List<UserProfileViewModel>> GetNewUsersAsync(int daysAgo);
    
    /// <summary>
    /// Get users by order count
    /// </summary>
    Task<List<UserProfileViewModel>> GetUsersByOrderCountAsync(int minOrders, int? maxOrders = null);
    
    /// <summary>
    /// Get users with specific product in cart or wishlist
    /// </summary>
    Task<List<UserProfileViewModel>> GetUsersInterestedInProductAsync(string productId);
    
    /// <summary>
    /// Get users who purchased specific category
    /// </summary>
    Task<List<UserProfileViewModel>> GetUsersByPurchasedCategoryAsync(string category);
    
    /// <summary>
    /// Advanced segmentation with multiple criteria
    /// </summary>
    Task<List<UserProfileViewModel>> GetUsersByMultipleCriteriaAsync(UserSegmentationCriteria criteria);
    
    /// <summary>
    /// Get user IDs only (for bulk messaging) based on criteria
    /// </summary>
    Task<List<string>> GetUserIdsByCriteriaAsync(UserSegmentationCriteria criteria);
}

/// <summary>
/// Criteria for advanced user segmentation
/// </summary>
public class UserSegmentationCriteria
{
    public decimal? MinSpent { get; set; }
    public decimal? MaxSpent { get; set; }
    public int? MinLoyaltyPoints { get; set; }
    public int? MaxLoyaltyPoints { get; set; }
    public List<MembershipTier>? MembershipTiers { get; set; }
    public bool? HasCartItems { get; set; }
    public bool? HasWishlistItems { get; set; }
    public int? InactiveDays { get; set; }
    public int? MinOrders { get; set; }
    public int? MaxOrders { get; set; }
    public List<string>? InterestedProductIds { get; set; }
    public List<string>? PurchasedCategories { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public bool? IsEmailVerified { get; set; }
}
