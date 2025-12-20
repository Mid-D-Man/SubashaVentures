// Services/Authorization/PermissionService.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Users;
using SubashaVentures.Utilities.HelperScripts;
using System.Security.Claims;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Authorization;

/// <summary>
/// Implementation of permission service for authentication and authorization checks
/// </summary>
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
                
                // Get current URL if returnUrl not provided
                if (string.IsNullOrEmpty(returnUrl))
                {
                    returnUrl = _navigationManager.Uri
                        .Replace(_navigationManager.BaseUri, "/");
                }
                
                NavigateToSignIn(returnUrl);
                return false;
            }
            
            // Check if account is active
            var isActive = await IsAccountActiveAsync();
            if (!isActive)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User account is not active",
                    LogLevel.Warning
                );
                
                ShowPermissionDeniedMessage("access this feature (account suspended)");
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
        
        return await IsAccountActiveAsync();
    }

    public async Task<bool> CanAddToCartAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("add items to cart");
            return false;
        }
        
        return await IsAccountActiveAsync();
    }

    public async Task<bool> CanCheckoutAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("proceed to checkout");
            return false;
        }
        
        return await IsAccountActiveAsync();
    }

    public async Task<bool> CanWriteReviewAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("write a review");
            return false;
        }
        
        return await IsAccountActiveAsync();
    }

    public async Task<bool> CanViewOrdersAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            ShowAuthRequiredMessage("view your orders");
            return false;
        }
        
        return await IsAccountActiveAsync();
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

    public async Task<bool> IsAccountActiveAsync()
    {
        try
        {
            var userId = await GetCurrentUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }
            
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return false;
            }
            
            var isActive = user.AccountStatus == "Active";
            
            if (!isActive)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Account not active: Status = {user.AccountStatus}",
                    LogLevel.Warning
                );
            }
            
            return isActive;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking account status");
            _logger.LogError(ex, "Error checking account status");
            return false;
        }
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
            var signInUrl = "/signin";
            
            if (!string.IsNullOrEmpty(returnUrl))
            {
                // Encode return URL
                var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
                signInUrl = $"/signin?returnUrl={encodedReturnUrl}";
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
            var url = "/access-denied";
            
            if (!string.IsNullOrEmpty(reason))
            {
                var encodedReason = Uri.EscapeDataString(reason);
                url = $"/access-denied?reason={encodedReason}";
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
        
        // TODO: Integrate with toast notification service
        // For now, we'll just navigate to sign-in
        NavigateToSignIn();
    }

    public void ShowPermissionDeniedMessage(string action)
    {
        _logger.LogWarning("Permission denied: {Action}", action);
        
        // TODO: Integrate with toast notification service
        // For now, we'll navigate to access denied page
        NavigateToAccessDenied(action);
    }
}
