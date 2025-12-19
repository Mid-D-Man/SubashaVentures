// Services/Auth/CustomSupabaseClaimsFactory.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;

namespace SubashaVentures.Services.Auth;


/// <summary>
/// Custom claims factory to process user roles from Supabase for authorization
/// Similar to AirCode's CustomAccountFactory for Auth0
/// </summary>
public class CustomSupabaseClaimsFactory
{
    private readonly Client _supabaseClient;
    private readonly ILogger<CustomSupabaseClaimsFactory> _logger;

    public CustomSupabaseClaimsFactory(
        Client supabaseClient,
        ILogger<CustomSupabaseClaimsFactory> logger)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    /// <summary>
    /// Create ClaimsPrincipal with user roles from database
    /// </summary>
    public async Task<ClaimsPrincipal> CreateUserPrincipalAsync(Supabase.Gotrue.User user)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating claims principal for user: {user.Email}",
                LogLevel.Info
            );

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("sub", user.Id), // Standard JWT claim
            };

            // Get user roles from database
            var roles = await GetUserRolesAsync(user.Id);
            
            if (roles.Any())
            {
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Added role claim: {role} for user: {user.Email}",
                        LogLevel.Info
                    );
                }
            }
            else
            {
                // No roles found - assign default "user" role
                claims.Add(new Claim(ClaimTypes.Role, "user"));
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"No roles found for user {user.Email}, assigned default 'user' role",
                    LogLevel.Warning
                );
            }

            // Add metadata from user profile
            if (user.UserMetadata != null)
            {
                if (user.UserMetadata.TryGetValue("first_name", out var firstName))
                    claims.Add(new Claim(ClaimTypes.GivenName, firstName?.ToString() ?? ""));
                
                if (user.UserMetadata.TryGetValue("last_name", out var lastName))
                    claims.Add(new Claim(ClaimTypes.Surname, lastName?.ToString() ?? ""));
                
                if (user.UserMetadata.TryGetValue("avatar_url", out var avatar))
                    claims.Add(new Claim("avatar_url", avatar?.ToString() ?? ""));
            }

            // Debug: Log all claims
            await MID_HelperFunctions.DebugMessageAsync(
                $"Claims created for {user.Email}:",
                LogLevel.Info
            );
            
            foreach (var claim in claims)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"  - {claim.Type}: {claim.Value}",
                    LogLevel.Debug
                );
            }

            var identity = new ClaimsIdentity(claims, "Supabase");
            var principal = new ClaimsPrincipal(identity);

            return principal;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating claims principal");
            _logger.LogError(ex, "Failed to create claims principal for user: {UserId}", user.Id);
            
            // Return empty principal on error
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    /// <summary>
    /// Get user roles from database
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
                await MID_HelperFunctions.DebugMessageAsync(
                    $"No roles found in database for user: {userId}",
                    LogLevel.Warning
                );
                return new List<string>();
            }

            var roles = userRoles.Models.Select(r => r.Role).ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Retrieved {roles.Count} role(s) for user {userId}: {string.Join(", ", roles)}",
                LogLevel.Info
            );

            return roles;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting roles for user: {userId}");
            _logger.LogError(ex, "Failed to get user roles for: {UserId}", userId);
            return new List<string>();
        }
    }
}
