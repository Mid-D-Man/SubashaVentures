// Services/Auth/CustomSupabaseClaimsFactory.cs - FINAL FIX

using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;

namespace SubashaVentures.Services.Auth;

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

            // ✅ FIX: Get role from JWT token directly (custom claims are at root level)
            string role = "user";

            try
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "🔍 Getting role from JWT token...",
                    LogLevel.Info
                );

                // Get current session to access the JWT access token
                var session = _supabaseClient.Auth.CurrentSession;
                
                if (session != null && !string.IsNullOrEmpty(session.AccessToken))
                {
                    // Parse JWT to get custom claims at root level
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(session.AccessToken);
                    
                    // Check for user_role claim (set by Custom Access Token Hook)
                    var userRoleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "user_role");
                    
                    if (userRoleClaim != null)
                    {
                        role = userRoleClaim.Value;
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"✅ Role found in JWT custom claim (from hook): {role}",
                            LogLevel.Info
                        );
                    }
                    else
                    {
                        await MID_HelperFunctions.DebugMessageAsync(
                            "⚠️ user_role claim not found in JWT, checking metadata...",
                            LogLevel.Warning
                        );
                        
                        // Fallback 1: Check user_metadata
                        if (user.UserMetadata != null && user.UserMetadata.ContainsKey("role"))
                        {
                            role = user.UserMetadata["role"]?.ToString() ?? "user";
                            await MID_HelperFunctions.DebugMessageAsync(
                                $"✅ Role found in user_metadata: {role}",
                                LogLevel.Info
                            );
                        }
                        else
                        {
                            await MID_HelperFunctions.DebugMessageAsync(
                                "⚠️ Role not found anywhere, using default 'user'",
                                LogLevel.Warning
                            );
                        }
                    }
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "⚠️ No session found, checking user_metadata fallback...",
                        LogLevel.Warning
                    );
                    
                    // Fallback: user_metadata
                    if (user.UserMetadata != null && user.UserMetadata.ContainsKey("role"))
                    {
                        role = user.UserMetadata["role"]?.ToString() ?? "user";
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"✅ Role found in user_metadata: {role}",
                            LogLevel.Info
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Getting role from JWT");
                _logger.LogWarning(ex, "Failed to get role from JWT, using default");
                
                // Last fallback: user_metadata
                if (user.UserMetadata != null && user.UserMetadata.ContainsKey("role"))
                {
                    role = user.UserMetadata["role"]?.ToString() ?? "user";
                }
            }
            
            // Add role claim
            claims.Add(new Claim(ClaimTypes.Role, role));
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"➕ Added role claim: {role}",
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
                
                if (user.UserMetadata.TryGetValue("picture", out var picture))
                    claims.Add(new Claim("picture", picture?.ToString() ?? ""));
                
                if (user.UserMetadata.TryGetValue("name", out var name))
                    claims.Add(new Claim(ClaimTypes.Name, name?.ToString() ?? ""));
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Claims principal created for {user.Email} with role: {role}",
                LogLevel.Info
            );

            var identity = new ClaimsIdentity(claims, "Supabase");
            return new ClaimsPrincipal(identity);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating claims principal");
            _logger.LogError(ex, "Failed to create claims principal for user: {UserId}", user.Id);
            
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
