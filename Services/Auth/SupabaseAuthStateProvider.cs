// Services/Auth/SupabaseAuthStateProvider.cs - UPDATED FOR BETTER STATE MANAGEMENT
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using SubashaVentures.Services.Auth;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Custom AuthenticationStateProvider for Supabase with role-based claims
/// Uses CustomSupabaseClaimsFactory to process user roles
/// UPDATED: Better state management and session persistence
/// </summary>
public class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly Client _supabaseClient;
    private readonly ILogger<SupabaseAuthStateProvider> _logger;
    private readonly CustomSupabaseClaimsFactory _claimsFactory;
    private AuthenticationState? _cachedAuthState;
    private bool _isInitialized = false;

    public SupabaseAuthStateProvider(
        Client supabaseClient,
        ILogger<SupabaseAuthStateProvider> logger,
        CustomSupabaseClaimsFactory claimsFactory)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
        _claimsFactory = claimsFactory;

        // ✅ NEW: Subscribe to auth state changes
        _supabaseClient.Auth.AddStateChangedListener(OnAuthStateChanged);
    }

    /// <summary>
    /// Handle Supabase auth state changes
    /// </summary>
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

                // Clear cached state to force reload
                _cachedAuthState = null;

                // ✅ FIXED: Notify on the synchronization context
                var authStateTask = GetAuthenticationStateAsync();
                NotifyAuthenticationStateChanged(Task.FromResult(await authStateTask));
                
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
            // ✅ Return cached state if available and not expired
            if (_cachedAuthState != null)
            {
                var cachedUser = _cachedAuthState.User;
                if (cachedUser?.Identity?.IsAuthenticated ?? false)
                {
                    // Check if session is still valid (not expired)
                    var session = _supabaseClient.Auth.CurrentSession;
                    if (session != null && session.ExpiresAt() > DateTime.UtcNow)
                    {
                        await MID_HelperFunctions.DebugMessageAsync(
                            "Using cached authentication state",
                            LogLevel.Debug
                        );
                        return _cachedAuthState;
                    }
                }
            }

            // ✅ Initialize Supabase session on first call
            if (!_isInitialized)
            {
                await InitializeSessionAsync();
                _isInitialized = true;
            }

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

    /// <summary>
    /// Initialize Supabase session from stored tokens
    /// </summary>
    private async Task InitializeSessionAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Initializing Supabase session...",
                LogLevel.Info
            );

            // Supabase automatically restores session from localStorage
            // Just need to check if there's a current session
            var session = _supabaseClient.Auth.CurrentSession;
            
            if (session != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Session restored for: {session.User?.Email}",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No existing session found",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing session");
            _logger.LogError(ex, "Failed to initialize session");
        }
    }

    /// <summary>
    /// Notify that authentication state has changed
    /// Call this after login/logout
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        try
        {
            // Clear cached state
            _cachedAuthState = null;
            
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            
            _logger.LogInformation("✅ Authentication state change notified manually");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying authentication state change");
        }
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

    /// <summary>
    /// Force refresh of authentication state
    /// </summary>
    public async Task RefreshAuthenticationStateAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Forcing authentication state refresh",
                LogLevel.Info
            );

            // Clear cache
            _cachedAuthState = null;
            _isInitialized = false;

            // Refresh Supabase session
            await _supabaseClient.Auth.RefreshSession();

            // Notify state change
            NotifyAuthenticationStateChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Refreshing auth state");
            _logger.LogError(ex, "Failed to refresh authentication state");
        }
    }

    /// <summary>
    /// Dispose and clean up
    /// </summary>
    public void Dispose()
    {
        _supabaseClient.Auth.RemoveStateChangedListener(OnAuthStateChanged);
    }
}
