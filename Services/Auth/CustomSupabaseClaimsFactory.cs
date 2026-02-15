// Services/Auth/CustomSupabaseClaimsFactory.cs - UPDATED FOR JWT CLAIMS
using System.Security.Claims;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;

namespace SubashaVentures.Services.Auth;

/// <summary>
/// Custom claims factory - UPDATED to use JWT claims from Custom Access Token Hook
/// NO database queries during authentication - prevents infinite recursion
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
    /// Create ClaimsPrincipal with user role from JWT claims (set by Custom Access Token Hook)
    /// CRITICAL: This method does NOT query the database - all info comes from JWT
    /// </summary>
    public async Task<ClaimsPrincipal> CreateUserPrincipalAsync(User user)
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
            new Claim("sub", user.Id),
        };

        // ✅ PRIORITY 1: Get role from JWT custom claim
        string role = "user";

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🔍 Checking app_metadata for role...",
                LogLevel.Info
            );

            // Try app_metadata first (set by Custom Access Token Hook)
            if (user.AppMetadata != null && user.AppMetadata.ContainsKey("user_role"))
            {
                role = user.AppMetadata["user_role"]?.ToString() ?? "user";
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Role found in app_metadata (from hook): {role}",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "⚠️ user_role not in app_metadata, checking user_metadata...",
                    LogLevel.Warning
                );

                // Fallback to user_metadata
                if (user.UserMetadata != null && user.UserMetadata.ContainsKey("role"))
                {
                    role = user.UserMetadata["role"]?.ToString() ?? "user";
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ Role found in user_metadata (legacy): {role}",
                        LogLevel.Info
                    );
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "⚠️ Role not found in JWT, using default 'user'",
                        LogLevel.Warning
                    );
                }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting role from JWT metadata");
            _logger.LogWarning(ex, "Failed to get role from JWT metadata, using default");
        }
        
        // Add role claim
        claims.Add(new Claim(ClaimTypes.Role, role));
        
        await MID_HelperFunctions.DebugMessageAsync(
            $"➕ Added role claim: {role} for user: {user.Email}",
            LogLevel.Info
        );

        // Add profile metadata
        if (user.UserMetadata != null)
        {
            if (user.UserMetadata.TryGetValue("first_name", out var firstName))
                claims.Add(new Claim(ClaimTypes.GivenName, firstName?.ToString() ?? ""));
            
            if (user.UserMetadata.TryGetValue("last_name", out var lastName))
                claims.Add(new Claim(ClaimTypes.Surname, lastName?.ToString() ?? ""));
            
            if (user.UserMetadata.TryGetValue("avatar_url", out var avatar))
                claims.Add(new Claim("avatar_url", avatar?.ToString() ?? ""));
        }

        // Also check app_metadata for names
        if (user.AppMetadata != null)
        {
            if (user.AppMetadata.TryGetValue("first_name", out var appFirstName))
                claims.Add(new Claim("app_first_name", appFirstName?.ToString() ?? ""));
            
            if (user.AppMetadata.TryGetValue("last_name", out var appLastName))
                claims.Add(new Claim("app_last_name", appLastName?.ToString() ?? ""));
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ Claims created for {user.Email} with role: {role}",
            LogLevel.Info
        );

        var identity = new ClaimsIdentity(claims, "Supabase");
        var principal = new ClaimsPrincipal(identity);

        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ ClaimsPrincipal created successfully",
            LogLevel.Info
        );

        return principal;
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Creating claims principal");
        _logger.LogError(ex, "Failed to create claims principal for user: {UserId}", user.Id);
        
        return new ClaimsPrincipal(new ClaimsIdentity());
    }
}
}
