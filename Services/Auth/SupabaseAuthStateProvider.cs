// Services/Auth/SupabaseAuthStateProvider.cs - UPDATED (2 roles only)
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Custom AuthenticationStateProvider for Supabase with RBAC
/// ONLY 2 ROLES: user, superior_admin
/// </summary>
public class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly Client _supabaseClient;
    private readonly ILogger<SupabaseAuthStateProvider> _logger;

    public SupabaseAuthStateProvider(
        Client supabaseClient,
        ILogger<SupabaseAuthStateProvider> logger)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var user = _supabaseClient.Auth.CurrentUser;
            
            if (user == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No authenticated user found",
                    LogLevel.Info
                );
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            // Get user roles from database
            var roles = await GetUserRolesAsync(user.Id);
            
            // Create claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("sub", user.Id), // Standard JWT claim
            };

            // Add role claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Added role: {role} for user: {user.Email}",
                    LogLevel.Info
                );
            }

            // Add metadata claims if available
            if (user.UserMetadata != null)
            {
                if (user.UserMetadata.TryGetValue("first_name", out var firstName))
                    claims.Add(new Claim(ClaimTypes.GivenName, firstName?.ToString() ?? ""));
                
                if (user.UserMetadata.TryGetValue("last_name", out var lastName))
                    claims.Add(new Claim(ClaimTypes.Surname, lastName?.ToString() ?? ""));
                
                if (user.UserMetadata.TryGetValue("avatar_url", out var avatar))
                    claims.Add(new Claim("avatar_url", avatar?.ToString() ?? ""));
            }

            var identity = new ClaimsIdentity(claims, "Supabase");
            var principal = new ClaimsPrincipal(identity);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User authenticated: {user.Email} with {roles.Count} role(s)",
                LogLevel.Info
            );

            return new AuthenticationState(principal);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting authentication state");
            _logger.LogError(ex, "Failed to get authentication state");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    /// <summary>
    /// Get user roles from database (returns 'user' by default)
    /// </summary>
    private async Task<List<string>> GetUserRolesAsync(string userId)
    {
        try
        {
            var userRoles = await _supabaseClient
                .From<UserRoleModel>()
                .Where(r => r.UserId == userId)
                .Get();

            if (userRoles?.Models == null || !userRoles.Models.Any())
            {
                // No roles found - return default "user" role
                await MID_HelperFunctions.DebugMessageAsync(
                    $"No roles found for user {userId}, assigning default 'user' role",
                    LogLevel.Warning
                );
                return new List<string> { "user" };
            }

            return userRoles.Models.Select(r => r.Role).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting roles for user: {userId}");
            _logger.LogError(ex, "Failed to get user roles for: {UserId}", userId);
            // Return default role on error
            return new List<string> { "user" };
        }
    }

    /// <summary>
    /// Notify that authentication state has changed
    /// Call this after login/logout
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Check if current user has a specific role
    /// </summary>
    public async Task<bool> HasRoleAsync(string role)
    {
        var authState = await GetAuthenticationStateAsync();
        return authState.User.IsInRole(role);
    }

    /// <summary>
    /// Check if current user is superior admin
    /// </summary>
    public async Task<bool> IsSuperiorAdminAsync()
    {
        return await HasRoleAsync("superior_admin");
    }

    /// <summary>
    /// Get all roles for current user
    /// </summary>
    public async Task<List<string>> GetCurrentUserRolesAsync()
    {
        var authState = await GetAuthenticationStateAsync();
        return authState.User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
    }
}
