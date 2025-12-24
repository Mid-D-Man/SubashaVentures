// Services/Auth/SupabaseAuthStateProvider.cs - FIXED FOR OAUTH PERSISTENCE
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using SubashaVentures.Services.Auth;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

public class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly Client _supabaseClient;
    private readonly ILogger<SupabaseAuthStateProvider> _logger;
    private readonly CustomSupabaseClaimsFactory _claimsFactory;
    private AuthenticationState? _cachedAuthState;

    public SupabaseAuthStateProvider(
        Client supabaseClient,
        ILogger<SupabaseAuthStateProvider> logger,
        CustomSupabaseClaimsFactory claimsFactory)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
        _claimsFactory = claimsFactory;

        // Subscribe to auth state changes
        _supabaseClient.Auth.AddStateChangedListener(OnAuthStateChanged);
    }

    private void OnAuthStateChanged(object? sender, Constants.AuthState state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"🔔 Auth state changed: {state}",
                    LogLevel.Info
                );

                // Clear cached state
                _cachedAuthState = null;

                // Notify Blazor
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                
                _logger.LogInformation("✅ Notified authentication state change");
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Handling auth state change");
                _logger.LogError(ex, "Error handling auth state change");
            }
        });
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // ✅ Always get fresh session - don't rely on cache for auth checks
            var user = _supabaseClient.Auth.CurrentUser;

            if (user == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No authenticated user found",
                    LogLevel.Info
                );
                
                _cachedAuthState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                return _cachedAuthState;
            }

            // Verify session is still valid
            var session = _supabaseClient.Auth.CurrentSession;
            if (session == null || session.ExpiresAt() <= DateTime.UtcNow)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Session expired or invalid",
                    LogLevel.Warning
                );
                
                _cachedAuthState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                return _cachedAuthState;
            }

            // Use claims factory to create principal with roles
            var principal = await _claimsFactory.CreateUserPrincipalAsync(user);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User authenticated: {user.Email}",
                LogLevel.Info
            );

            _cachedAuthState = new AuthenticationState(principal);
            return _cachedAuthState;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting authentication state");
            _logger.LogError(ex, "Failed to get authentication state");
            
            _cachedAuthState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            return _cachedAuthState;
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        try
        {
            _cachedAuthState = null;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            _logger.LogInformation("✅ Authentication state change notified manually");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying authentication state change");
        }
    }

    public async Task<bool> HasRoleAsync(string role)
    {
        var authState = await GetAuthenticationStateAsync();
        return authState.User.IsInRole(role);
    }

    public async Task<bool> IsSuperiorAdminAsync()
    {
        return await HasRoleAsync("superior_admin");
    }

    public async Task<List<string>> GetCurrentUserRolesAsync()
    {
        var authState = await GetAuthenticationStateAsync();
        return authState.User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
    }

    public async Task RefreshAuthenticationStateAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Forcing authentication state refresh",
                LogLevel.Info
            );

            _cachedAuthState = null;
            await _supabaseClient.Auth.RefreshSession();
            NotifyAuthenticationStateChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Refreshing auth state");
            _logger.LogError(ex, "Failed to refresh authentication state");
        }
    }

    public void Dispose()
    {
        _supabaseClient.Auth.RemoveStateChangedListener(OnAuthStateChanged);
    }
}
