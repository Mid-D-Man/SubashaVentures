// Services/Authorization/IPermissionService.cs
using SubashaVentures.Domain.User;

namespace SubashaVentures.Services.Authorization;

/// <summary>
/// Service for handling user permissions and authentication checks
/// Used before actions like: wishlist, cart, checkout, reviews, orders
/// </summary>
public interface IPermissionService
{
    // ==================== AUTHENTICATION CHECKS ====================
    
    /// <summary>
    /// Check if user is currently authenticated
    /// </summary>
    /// <returns>True if authenticated, false otherwise</returns>
    Task<bool> IsAuthenticatedAsync();
    
    /// <summary>
    /// Ensure user is authenticated, redirect to sign-in if not
    /// </summary>
    /// <param name="returnUrl">URL to return to after sign-in (optional)</param>
    /// <returns>True if authenticated, false if redirected</returns>
    Task<bool> EnsureAuthenticatedAsync(string? returnUrl = null);
    
    /// <summary>
    /// Get current authenticated user ID
    /// </summary>
    /// <returns>User ID or null if not authenticated</returns>
    Task<string?> GetCurrentUserIdAsync();
    
    /// <summary>
    /// Get current authenticated user email
    /// </summary>
    /// <returns>User email or null if not authenticated</returns>
    Task<string?> GetCurrentUserEmailAsync();
    
    // ==================== ROLE-BASED CHECKS ====================
    
    /// <summary>
    /// Check if current user has a specific role
    /// </summary>
    /// <param name="role">Role to check (e.g., "user", "superior_admin")</param>
    /// <returns>True if user has role, false otherwise</returns>
    Task<bool> HasRoleAsync(string role);
    
    /// <summary>
    /// Check if current user has any of the specified roles
    /// </summary>
    /// <param name="roles">List of roles to check</param>
    /// <returns>True if user has any of the roles, false otherwise</returns>
    Task<bool> HasAnyRoleAsync(params string[] roles);
    
    /// <summary>
    /// Check if current user has all of the specified roles
    /// </summary>
    /// <param name="roles">List of roles to check</param>
    /// <returns>True if user has all roles, false otherwise</returns>
    Task<bool> HasAllRolesAsync(params string[] roles);
    
    /// <summary>
    /// Ensure user has required role, show error if not
    /// </summary>
    /// <param name="role">Required role</param>
    /// <param name="errorMessage">Custom error message (optional)</param>
    /// <returns>True if user has role, false otherwise</returns>
    Task<bool> RequireRoleAsync(string role, string? errorMessage = null);
    
    /// <summary>
    /// Check if current user is superior admin
    /// </summary>
    /// <returns>True if superior admin, false otherwise</returns>
    Task<bool> IsSuperiorAdminAsync();
    
    // ==================== PERMISSION CHECKS ====================
    
    /// <summary>
    /// Check if user can add items to wishlist
    /// </summary>
    /// <returns>True if allowed, false otherwise</returns>
    Task<bool> CanAddToWishlistAsync();
    
    /// <summary>
    /// Check if user can add items to cart
    /// </summary>
    /// <returns>True if allowed, false otherwise</returns>
    Task<bool> CanAddToCartAsync();
    
    /// <summary>
    /// Check if user can proceed to checkout
    /// </summary>
    /// <returns>True if allowed, false otherwise</returns>
    Task<bool> CanCheckoutAsync();
    
    /// <summary>
    /// Check if user can write reviews
    /// </summary>
    /// <returns>True if allowed, false otherwise</returns>
    Task<bool> CanWriteReviewAsync();
    
    /// <summary>
    /// Check if user can view orders
    /// </summary>
    /// <returns>True if allowed, false otherwise</returns>
    Task<bool> CanViewOrdersAsync();
    
    /// <summary>
    /// Check if user can access admin panel
    /// </summary>
    /// <returns>True if allowed, false otherwise</returns>
    Task<bool> CanAccessAdminAsync();
    
    // ==================== ACCOUNT STATUS CHECKS ====================
    
    /// <summary>
    /// Check if user account is active (not suspended/banned)
    /// </summary>
    /// <returns>True if active, false otherwise</returns>
    Task<bool> IsAccountActiveAsync();
    
    /// <summary>
    /// Get user's account status
    /// </summary>
    /// <returns>Account status string or null if not found</returns>
    Task<string?> GetAccountStatusAsync();
    
    /// <summary>
    /// Get user's membership tier
    /// </summary>
    /// <returns>Membership tier or null if not found</returns>
    Task<MembershipTier?> GetMembershipTierAsync();
    
    // ==================== NAVIGATION HELPERS ====================
    
    /// <summary>
    /// Navigate to sign-in page with return URL
    /// </summary>
    /// <param name="returnUrl">URL to return to after sign-in</param>
    void NavigateToSignIn(string? returnUrl = null);
    
    /// <summary>
    /// Navigate to access denied page
    /// </summary>
    /// <param name="reason">Reason for denial (optional)</param>
    void NavigateToAccessDenied(string? reason = null);
    
    /// <summary>
    /// Show authentication required message
    /// </summary>
    /// <param name="action">Action that requires authentication</param>
    void ShowAuthRequiredMessage(string action);
    
    /// <summary>
    /// Show permission denied message
    /// </summary>
    /// <param name="action">Action that was denied</param>
    void ShowPermissionDeniedMessage(string action);
}
