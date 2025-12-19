// Services/Auth/SupabaseAuthStateProvider.cs - UPDATED with custom claims factory
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using SubashaVentures.Services.Auth;
using SubashaVentures.Utilities.HelperScripts;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Custom AuthenticationStateProvider for Supabase with role-based claims
/// Uses CustomSupabaseClaimsFactory to process user roles
/// </summary>
public class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly Client _supabaseClient;
    private readonly ILogger<SupabaseAuthStateProvider> _logger;
    private readonly CustomSupabaseClaimsFactory _claimsFactory;

    public SupabaseAuthStateProvider(
        Client supabaseClient,
        ILogger<SupabaseAuthStateProvider> logger,
        CustomSupabaseClaimsFactory claimsFactory)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
        _claimsFactory = claimsFactory;
    }

    public override async TaskAuthenticationState> GetAuthenticationStateAsync()
{
try
{
var user = _supabaseClient.Auth.CurrentUser;if (user == null)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "No authenticated user found",
                LogLevel.Info
            );
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Use claims factory to create principal with roles
        var principal = await _claimsFactory.CreateUserPrincipalAsync(user);

        await MID_HelperFunctions.DebugMessageAsync(
            $"✓ User authenticated: {user.Email}",
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
}}
