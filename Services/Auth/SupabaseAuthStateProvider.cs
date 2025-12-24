// Services/Auth/SupabaseAuthStateProvider.cs - COMPLETE REWRITE
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Microsoft.JSInterop;
using Newtonsoft.Json.Linq;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

public class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SupabaseAuthStateProvider> _logger;
    private AuthenticationState? _cachedAuthState;

    public SupabaseAuthStateProvider(
        IJSRuntime jsRuntime,
        ILogger<SupabaseAuthStateProvider> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // Get session from JavaScript
            var sessionJson = await _jsRuntime.InvokeAsync<string>("eval", 
                @"(async function() {
                    try {
                        const session = await window.supabaseOAuth.getSession();
                        return session ? JSON.stringify(session) : null;
                    } catch (error) {
                        console.error('Error getting session:', error);
                        return null;
                    }
                })()");

            if (string.IsNullOrEmpty(sessionJson))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No authenticated user found",
                    LogLevel.Info
                );
                
                _cachedAuthState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                return _cachedAuthState;
            }

            // Parse session
            var session = JObject.Parse(sessionJson);
            var user = session["user"];
            
            if (user == null)
            {
                _cachedAuthState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                return _cachedAuthState;
            }

            // Create claims from user data
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user["id"]?.ToString() ?? ""),
                new Claim(ClaimTypes.Email, user["email"]?.ToString() ?? ""),
                new Claim("sub", user["id"]?.ToString() ?? "")
            };

            // Add metadata claims
            var metadata = user["user_metadata"];
            if (metadata != null)
            {
                if (metadata["first_name"] != null)
                    claims.Add(new Claim(ClaimTypes.GivenName, metadata["first_name"].ToString()));
                
                if (metadata["last_name"] != null)
                    claims.Add(new Claim(ClaimTypes.Surname, metadata["last_name"].ToString()));
                
                if (metadata["avatar_url"] != null)
                    claims.Add(new Claim("avatar_url", metadata["avatar_url"].ToString()));
            }

            // Get roles from database via JavaScript
            var userId = user["id"]?.ToString();
            if (!string.IsNullOrEmpty(userId))
            {
                var roles = await GetUserRolesAsync(userId);
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            var identity = new ClaimsIdentity(claims, "Supabase");
            var principal = new ClaimsPrincipal(identity);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User authenticated: {user["email"]}",
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

    private async Task<List<string>> GetUserRolesAsync(string userId)
    {
        try
        {
            // Call JavaScript to query Supabase for roles
            var rolesJson = await _jsRuntime.InvokeAsync<string>("eval", 
                $@"(async function() {{
                    try {{
                        const {{ data, error }} = await window.supabaseOAuth.supabaseClient
                            .from('user_roles')
                            .select('role')
                            .eq('user_id', '{userId}');
                        
                        if (error) {{
                            console.error('Error getting roles:', error);
                            return null;
                        }}
                        
                        return JSON.stringify(data);
                    }} catch (error) {{
                        console.error('Exception getting roles:', error);
                        return null;
                    }}
                }})()");

            if (string.IsNullOrEmpty(rolesJson))
            {
                return new List<string> { "user" }; // Default role
            }

            var rolesArray = JArray.Parse(rolesJson);
            var roles = rolesArray.Select(r => r["role"]?.ToString() ?? "user").ToList();

            if (!roles.Any())
            {
                roles.Add("user");
            }

            return roles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user roles");
            return new List<string> { "user" };
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        try
        {
            _cachedAuthState = null;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            _logger.LogInformation("✅ Authentication state change notified");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying authentication state change");
        }
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
            NotifyAuthenticationStateChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Refreshing auth state");
            _logger.LogError(ex, "Failed to refresh authentication state");
        }
    }
}
