// Services/Auth/SupabaseAuthStateProvider.cs - C# AUTH STATE
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using SubashaVentures.Services.Auth;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.Auth;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Auth;

public class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly SupabaseAuthService _authService;
    private readonly ILogger<SupabaseAuthStateProvider> _logger;

    public SupabaseAuthStateProvider(
        SupabaseAuthService authService,
        ILogger<SupabaseAuthStateProvider> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var session = await _authService.GetCurrentSessionAsync();
            
            if (session == null || session.User == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No authenticated user found",
                    LogLevel.Info
                );
                
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            // Validate token
            var claims = JwtTokenHelper.ValidateAndExtractClaims(session.AccessToken);
            
            if (claims == null || !claims.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Token validation failed, attempting refresh",
                    LogLevel.Warning
                );

                // Try to refresh token
                var refreshed = await _authService.RefreshSessionAsync();
                if (refreshed)
                {
                    session = await _authService.GetCurrentSessionAsync();
                    if (session != null)
                    {
                        claims = JwtTokenHelper.ValidateAndExtractClaims(session.AccessToken);
                    }
                }

                if (claims == null || !claims.Any())
                {
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }
            }

            var identity = new ClaimsIdentity(claims, "Supabase");
            var principal = new ClaimsPrincipal(identity);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User authenticated: {session.User.Email}",
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

    public void NotifyAuthenticationStateChanged()
    {
        try
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            _logger.LogInformation("✅ Authentication state change notified");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying authentication state change");
        }
    }
}
