// Services/Authorization/PermissionService.cs - FIXED WITH BETTER ERROR HANDLING
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Users;
using SubashaVentures.Utilities.HelperScripts;
using System.Security.Claims;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Authorization;

public class PermissionService : IPermissionService
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly NavigationManager _navigationManager;
    private readonly IUserService _userService;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        AuthenticationStateProvider authStateProvider,
        NavigationManager navigationManager,
        IUserService userService,
        ILogger<PermissionService> logger)
    {
        _authStateProvider = authStateProvider;
        _navigationManager = navigationManager;
        _userService = userService;
        _logger = logger;
    }

    // ==================== AUTHENTICATION CHECKS ====================

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var isAuthenticated = authState.User?.Identity?.IsAuthenticated ?? false;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Authentication check: {(isAuthenticated ? "Authenticated" : "Not authenticated")}",
                LogLevel.Info
            );
            
            return isAuthenticated;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking authentication");
            _logger.LogError(ex, "Error checking authentication status");
            return false;
        }
    }

    public async Task<bool> EnsureAuthenticatedAsync(string? returnUrl = null)
    {
        try
        {
            var isAuthenticated = await IsAuthenticatedAsync();
            
            if (!isAuthenticated)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User not authenticated, redirecting to sign-in",
                    LogLevel.Warning
                );
                
                if (string.IsNullOrEmpty(returnUrl))
                {
                    returnUrl = _navigationManager.Uri.Replace(_navigationManager.BaseUri, "");
                }
                
                NavigateToSignIn(returnUrl);
                return false;
            }
            
            // ✅ FIXED: Better error handling for account status
            var accountCheckResult = await CheckAccountStatusAsync();
            if (!accountCheckResult.IsActive)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Account check failed: {accountCheckResult.Reason}",
                    LogLevel.Warning
                );
                
                ShowPermissionDeniedMessage(accountCheckResult.Reason);
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Ensuring authentication");
            _logger.LogError(ex, "Error ensuring authentication");
            return false;
        }
    }

    public async Task<string?> GetCurrentUserIdAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            return userId;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting current user ID");
            _logger.LogError(ex, "Error getting current user ID");
            return null;
        }
    }

    public async Task<string?> GetCurrentUserEmailAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var email = authState.User?.FindFirst(ClaimTypes.Email)?.Value;
            
            return email;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting current user email");
            _logger.LogError(ex, "Error getting current user email");
            return null;
        }
    }

    // ==================== ROLE-BASED CHECKS ====================

    public async Task<bool> HasRoleAsync(string role)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                _logger.LogWarning("HasRole called with empty role");
                return false;
            }
            
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var hasRole = authState.User?.IsInRole(role) ?? false;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Role check '{role}': {(hasRole ? "Has role" : "Does not have role")}",
                LogLevel.Info
            );
            
            return hasRole;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Checking role: {role}");
            _logger.LogError(ex, "Error checking role: {Role}", role);
            return false;
        }
    }

    public async Task<bool> HasAnyRoleAsync(params string[] roles)
    {
        try
        {
            if (roles == null || roles.Length == 0)
            {
                _logger.LogWarning("HasAnyRole called with no roles");
                return false;
            }
            
            foreach (var role in roles)
            {
                if (await HasRoleAsync(role))
                {
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking any role");
            _logger.LogError(ex, "Error checking any role");
            return false;
        }
    }

    public async Task<bool> HasAllRolesAsync(params string[] roles)
    {
        try
        {
            if (roles == null || roles.Length == 0)
            {
                _logger.LogWarning("HasAllRoles called with no roles");
                return false;
            }
            
            foreach (var role in roles)
            {
                if (!await HasRoleAsync(role))
                {
                    return false;
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking all roles");
            _logger.LogError(ex, "Error checking all roles");
            return false;
        }
    }

    public async Task<bool> RequireRoleAsync(string role, string? errorMessage = null)
    {
        try
        {
            var hasRole = await HasRoleAsync(role);
            
            if (!hasRole)
            {
                var message = errorMessage ?? $"You need '{role}' role to perform this action";
                ShowPermissionDeniedMessage(message);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Permission denied: User does not have '{role}' role",
                    LogLevel.Warning
                );
            }
            
            return hasRole;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Requiring role: {role}");
            _logger.LogError(ex, "Error requiring role: {Role}", role);
            return false;
        }
    }

    public async Task<bool> IsSuperiorAdminAsync()
    {
        return await HasRoleAsync("superior_admin");
    }

    // ==================== PERMISSION CHECKS ====================

    public async Task<bool> CanAddToWishlistAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("add items to wishlist");
            return false;
        }
        
        var accountCheck = await CheckAccountStatusAsync();
        if (!accountCheck.IsActive)
        {
            ShowPermissionDeniedMessage(accountCheck.Reason);
            return false;
        }
        
        return true;
    }

    public async Task<bool> CanAddToCartAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("add items to cart");
            return false;
        }
        
        var accountCheck = await CheckAccountStatusAsync();
        if (!accountCheck.IsActive)
        {
            ShowPermissionDeniedMessage(accountCheck.Reason);
            return false;
        }
        
        return true;
    }

    public async Task<bool> CanCheckoutAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("proceed to checkout");
            return false;
        }
        
        var accountCheck = await CheckAccountStatusAsync();
        if (!accountCheck.IsActive)
        {
            ShowPermissionDeniedMessage(accountCheck.Reason);
            return false;
        }
        
        return true;
    }

    public async Task<bool> CanWriteReviewAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("write a review");
            return false;
        }
        
        var accountCheck = await CheckAccountStatusAsync();
        if (!accountCheck.IsActive)
        {
            ShowPermissionDeniedMessage(accountCheck.Reason);
            return false;
        }
        
        return true;
    }

    public async Task<bool> CanViewOrdersAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("view your orders");
            return false;
        }
        
        var accountCheck = await CheckAccountStatusAsync();
        if (!accountCheck.IsActive)
        {
            ShowPermissionDeniedMessage(accountCheck.Reason);
            return false;
        }
        
        return true;
    }

    public async Task<bool> CanAccessAdminAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("access admin panel");
            return false;
        }
        
        var isSuperiorAdmin = await IsSuperiorAdminAsync();
        if (!isSuperiorAdmin)
        {
            ShowPermissionDeniedMessage("access admin panel (admin role required)");
            return false;
        }
        
        return true;
    }

    // ==================== ACCOUNT STATUS CHECKS ====================

    // ✅ NEW: Better account status checking
    private async Task<AccountStatusResult> CheckAccountStatusAsync()
    {
        try
        {
            var userId = await GetCurrentUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                return new AccountStatusResult 
                { 
                    IsActive = false, 
                    Reason = "User ID not found" 
                };
            }
            
            var user = await _userService.GetUserByIdAsync(userId);
            
            // ✅ CRITICAL FIX: Handle missing user profile
            if (user == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"⚠️ User profile not found for ID: {userId}. Attempting to create profile...",
                    LogLevel.Warning
                );
                
                // Try to create the profile
                var created = await _userService.EnsureUserProfileExistsAsync(userId);
                
                if (!created)
                {
                    return new AccountStatusResult 
                    { 
                        IsActive = false, 
                        Reason = "User profile could not be created. Please try signing out and signing in again." 
                    };
                }
                
                // Retry getting the user
                user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return new AccountStatusResult 
                    { 
                        IsActive = false, 
                        Reason = "User profile creation failed. Please contact support." 
                    };
                }
            }
            
            // Check account status
            if (user.AccountStatus != "Active")
            {
                var reason = user.AccountStatus switch
                {
                    "Suspended" => $"Your account has been suspended. Reason: {user.SuspensionReason ?? "Contact support for details"}",
                    "Deleted" => "Your account has been deleted. Please contact support if this was a mistake.",
                    _ => $"Your account status is '{user.AccountStatus}'. Please contact support."
                };
                
                return new AccountStatusResult 
                { 
                    IsActive = false, 
                    Reason = reason 
                };
            }
            
            return new AccountStatusResult 
            { 
                IsActive = true, 
                Reason = "Active" 
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking account status");
            _logger.LogError(ex, "Error checking account status");
            
            return new AccountStatusResult 
            { 
                IsActive = false, 
                Reason = "Error checking account status. Please try again." 
            };
        }
    }

    public async Task<bool> IsAccountActiveAsync()
    {
        var result = await CheckAccountStatusAsync();
        return result.IsActive;
    }

    public async Task<string?> GetAccountStatusAsync()
    {
        try
        {
            var userId = await GetCurrentUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }
            
            var user = await _userService.GetUserByIdAsync(userId);
            return user?.AccountStatus;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting account status");
            _logger.LogError(ex, "Error getting account status");
            return null;
        }
    }

    public async Task<MembershipTier?> GetMembershipTierAsync()
    {
        try
        {
            var userId = await GetCurrentUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }
            
            var user = await _userService.GetUserByIdAsync(userId);
            return user?.MembershipTier;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting membership tier");
            _logger.LogError(ex, "Error getting membership tier");
            return null;
        }
    }

    // ==================== NAVIGATION HELPERS ====================
    
    public void NavigateToSignIn(string? returnUrl = null)
    {
        try
        {
            var signInUrl = "signin";
            
            if (!string.IsNullOrEmpty(returnUrl))
            {
                var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
                signInUrl = $"signin?returnUrl={encodedReturnUrl}";
            }
            
            _logger.LogInformation("Navigating to sign-in: {Url}", signInUrl);
            _navigationManager.NavigateTo(signInUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to sign-in");
        }
    }

    public void NavigateToAccessDenied(string? reason = null)
    {
        try
        {
            var url = "access-denied";
            
            if (!string.IsNullOrEmpty(reason))
            {
                var encodedReason = Uri.EscapeDataString(reason);
                url = $"access-denied?reason={encodedReason}";
            }
            
            _logger.LogInformation("Navigating to access denied: {Url}", url);
            _navigationManager.NavigateTo(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to access denied");
        }
    }

    public void ShowAuthRequiredMessage(string action)
    {
        _logger.LogWarning("Authentication required: {Action}", action);
        NavigateToSignIn();
    }

    public void ShowPermissionDeniedMessage(string action)
    {
        _logger.LogWarning("Permission denied: {Action}", action);
        // TODO: Show toast notification instead of navigation
        // For now, just log it
        Console.WriteLine($"❌ Permission Denied: {action}");
    }
}

// ✅ NEW: Helper class for account status results
internal class AccountStatusResult
{
    public bool IsActive { get; set; }
    public string Reason { get; set; } = string.Empty;
}
