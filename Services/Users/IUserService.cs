// Services/Users/IUserService.cs - COMPLETE INTERFACE
using SubashaVentures.Domain.User;
using SubashaVentures.Models.Supabase;
using Supabase.Gotrue;

namespace SubashaVentures.Services.Users;

public interface IUserService
{
    // ==================== USER RETRIEVAL ====================
    
    /// <summary>
    /// Get all users with pagination
    /// </summary>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="take">Number of records to retrieve</param>
    /// <returns>List of user profile view models</returns>
    Task<List<UserProfileViewModel>> GetUsersAsync(int skip = 0, int take = 100);
    
    /// <summary>
    /// Get user by ID (from Supabase Auth + Profile)
    /// </summary>
    /// <param name="userId">User's unique identifier (UUID)</param>
    /// <returns>User profile view model or null if not found</returns>
    Task<UserProfileViewModel?> GetUserByIdAsync(string userId);
    
    /// <summary>
    /// Get user by email address
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <returns>User profile view model or null if not found</returns>
    Task<UserProfileViewModel?> GetUserByEmailAsync(string email);
    
    /// <summary>
    /// Search users by query (searches name and email)
    /// </summary>
    /// <param name="query">Search query string</param>
    /// <returns>List of matching user profiles</returns>
    Task<List<UserProfileViewModel>> SearchUsersAsync(string query);
    
    // ==================== USER MANAGEMENT ====================
    
    /// <summary>
    /// Create new user (Admin only - creates in Supabase Auth + Profile)
    /// </summary>
    /// <param name="request">User creation request data</param>
    /// <returns>Created user profile or null if failed</returns>
    Task<UserProfileViewModel?> CreateUserAsync(CreateUserRequest request);
    
    /// <summary>
    /// Update user profile information
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <param name="request">Update request with new data</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> UpdateUserProfileAsync(string userId, UpdateUserRequest request);
    
    /// <summary>
    /// Suspend or unsuspend user account
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <param name="suspend">True to suspend, false to unsuspend</param>
    /// <param name="reason">Reason for suspension (optional)</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> ToggleSuspendUserAsync(string userId, bool suspend, string? reason = null);
    
    /// <summary>
    /// Ban user (sets banned_until in Supabase Auth)
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <param name="bannedUntil">Ban expiration date (null for permanent ban)</param>
    /// <param name="reason">Reason for ban</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> BanUserAsync(string userId, DateTime? bannedUntil, string reason);
    
    /// <summary>
    /// Soft delete user (marks as deleted but keeps data)
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteUserAsync(string userId);
    
    /// <summary>
    /// Hard delete user (permanent deletion - cannot be undone)
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> PermanentDeleteUserAsync(string userId);
    
    // ==================== USER STATISTICS ====================
    
    /// <summary>
    /// Get user statistics (total, active, suspended, etc.)
    /// </summary>
    /// <returns>User statistics object</returns>
    Task<UserStatistics> GetUserStatisticsAsync();
    
    /// <summary>
    /// Update user order statistics (called after order placement)
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <param name="orderAmount">Order amount to add to total spent</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> UpdateUserOrderStatsAsync(string userId, decimal orderAmount);
    
    /// <summary>
    /// Update user loyalty points
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <param name="points">Points to add (positive) or subtract (negative)</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> UpdateLoyaltyPointsAsync(string userId, int points);
    
    /// <summary>
    /// Upgrade or downgrade membership tier
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <param name="tier">New membership tier</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> UpdateMembershipTierAsync(string userId, MembershipTier tier);
    
    // ==================== USER VERIFICATION ====================
    
    /// <summary>
    /// Verify user email (admin override)
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> VerifyUserEmailAsync(string userId);
    
    /// <summary>
    /// Verify user phone (admin override)
    /// </summary>
    /// <param name="userId">User's unique identifier</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> VerifyUserPhoneAsync(string userId);
    
    // ==================== BULK OPERATIONS ====================
    
    /// <summary>
    /// Bulk suspend multiple users
    /// </summary>
    /// <param name="userIds">List of user IDs to suspend</param>
    /// <param name="reason">Reason for suspension</param>
    /// <returns>True if all successful, false otherwise</returns>
    Task<bool> BulkSuspendUsersAsync(List<string> userIds, string reason);
    
    /// <summary>
    /// Bulk activate multiple users
    /// </summary>
    /// <param name="userIds">List of user IDs to activate</param>
    /// <returns>True if all successful, false otherwise</returns>
    Task<bool> BulkActivateUsersAsync(List<string> userIds);
    
    /// <summary>
    /// Export users to CSV format
    /// </summary>
    /// <param name="userIds">Optional list of specific user IDs to export (null for all)</param>
    /// <returns>CSV string</returns>
    Task<string> ExportUsersAsync(List<string>? userIds = null);
    /// <summary>
    /// Ensure user profile exists (create if missing) - for OAuth users
    /// </summary>
    /// <param name="userId">User's unique identifier from auth</param>
    /// <returns>True if profile exists or was created successfully</returns>
    Task<bool> EnsureUserProfileExistsAsync(string userId);
}

// ==================== DTOs (Data Transfer Objects) ====================

/// <summary>
/// Request model for creating a new user
/// </summary>
public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public bool SendWelcomeEmail { get; set; } = true;
}

/// <summary>
/// Request model for updating user profile
/// All fields are optional - only provided fields will be updated
/// </summary>
public class UpdateUserRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? AvatarUrl { get; set; }
    public bool? EmailNotifications { get; set; }
    public bool? SmsNotifications { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? Currency { get; set; }
}

/// <summary>
/// User statistics summary
/// </summary>
public class UserStatistics
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int SuspendedUsers { get; set; }
    public int DeletedUsers { get; set; }
    public int VerifiedUsers { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int NewUsersToday { get; set; }
    public Dictionary<MembershipTier, int> UsersByTier { get; set; } = new();
    
    // Computed properties
    public double ActivePercentage => TotalUsers > 0 
        ? (ActiveUsers / (double)TotalUsers) * 100 
        : 0;
    
    public double VerificationRate => TotalUsers > 0 
        ? (VerifiedUsers / (double)TotalUsers) * 100 
        : 0;
    
    public string FormattedStats => 
        $"Total: {TotalUsers}, Active: {ActiveUsers} ({ActivePercentage:F1}%), " +
        $"Verified: {VerifiedUsers} ({VerificationRate:F1}%)";
}
