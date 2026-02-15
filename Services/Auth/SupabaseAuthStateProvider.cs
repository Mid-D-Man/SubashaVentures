// Services/Auth/SupabaseAuthStateProvider.cs - UPDATED WITH JWT CLAIMS
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using SubashaVentures.Services.Auth;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.Auth;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Auth;

public class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly SupabaseAuthService _authService;
    private readonly CustomSupabaseClaimsFactory _claimsFactory;
    private readonly ILogger<SupabaseAuthStateProvider> _logger;

    public SupabaseAuthStateProvider(
        SupabaseAuthService authService,
        CustomSupabaseClaimsFactory claimsFactory,
        ILogger<SupabaseAuthStateProvider> logger)
    {
        _authService = authService;
        _claimsFactory = claimsFactory;
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

            // ✅ Use CustomSupabaseClaimsFactory to get roles from JWT claims
            // No database queries happen here!
            var principal = await _claimsFactory.CreateUserPrincipalAsync(session.User);

            if (principal?.Identity?.IsAuthenticated == true)
            {
                var rolesClaim = string.Join(", ", 
                    principal.Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value));

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ User authenticated: {session.User.Email} with roles: {rolesClaim}",
                    LogLevel.Info
                );

                return new AuthenticationState(principal);
            }

            // If claims factory failed, try token validation as fallback
            await MID_HelperFunctions.DebugMessageAsync(
                "Claims factory returned empty principal, attempting token validation fallback",
                LogLevel.Warning
            );

            var claims = JwtTokenHelper.ValidateAndExtractClaims(session.AccessToken);
            
            if (claims == null || !claims.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Token validation failed, attempting session refresh",
                    LogLevel.Warning
                );

                var refreshed = await _authService.RefreshSessionAsync();
                if (refreshed)
                {
                    session = await _authService.GetCurrentSessionAsync();
                    if (session != null)
                    {
                        // Retry with claims factory after refresh
                        principal = await _claimsFactory.CreateUserPrincipalAsync(session.User);
                        if (principal?.Identity?.IsAuthenticated == true)
                        {
                            return new AuthenticationState(principal);
                        }
                    }
                }

                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var identity = new ClaimsIdentity(claims, "Supabase");
            return new AuthenticationState(new ClaimsPrincipal(identity));
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
