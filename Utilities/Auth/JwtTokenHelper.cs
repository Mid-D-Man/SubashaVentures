// Utilities/Auth/JwtTokenHelper.cs - ENHANCED WITH PROPER VALIDATION
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SubashaVentures.Utilities.Auth;

/// <summary>
/// JWT token validation and claims extraction with proper signature verification
/// </summary>
public static class JwtTokenHelper
{
    private const string SupabaseUrl = "https://wbwmovtewytjibxutssk.supabase.co";
    private const string JwtSecret = "wsT7UJyDm34gAJLaFPf1Y1f74C6RMsTQqIo6K2aKZxjwO4cvx/XZyNSRi4JIVy4yhuG1/j7CnqUBkEnWsnAhDQ=="; // From appsettings.json

    /// <summary>
    /// Validate JWT token with signature verification and extract claims
    /// </summary>
    public static List<Claim>? ValidateAndExtractClaims(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("❌ Token is null or empty");
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Proper JWT validation with signature verification
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = $"{SupabaseUrl}/auth/v1",
                ValidateAudience = true,
                ValidAudience = "authenticated",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero // Strict expiration check
            };

            // Validate token and get claims
            tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            var jwtToken = (JwtSecurityToken)validatedToken;
            var claims = jwtToken.Claims.ToList();

            // Add standard claims if they don't exist
            if (!claims.Any(c => c.Type == ClaimTypes.Email))
            {
                var email = claims.FirstOrDefault(c => c.Type == "email")?.Value;
                if (!string.IsNullOrEmpty(email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, email));
                }
            }

            if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
            {
                var sub = claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                if (!string.IsNullOrEmpty(sub))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
                }
            }

            // Extract user_metadata (first_name, last_name, avatar_url)
            var userMetadata = claims.FirstOrDefault(c => c.Type == "user_metadata")?.Value;
            if (!string.IsNullOrEmpty(userMetadata))
            {
                try
                {
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(userMetadata);
                    if (metadata != null)
                    {
                        if (metadata.TryGetValue("first_name", out var firstName))
                        {
                            claims.Add(new Claim(ClaimTypes.GivenName, firstName?.ToString() ?? ""));
                        }
                        if (metadata.TryGetValue("last_name", out var lastName))
                        {
                            claims.Add(new Claim(ClaimTypes.Surname, lastName?.ToString() ?? ""));
                        }
                        if (metadata.TryGetValue("avatar_url", out var avatar))
                        {
                            claims.Add(new Claim("avatar_url", avatar?.ToString() ?? ""));
                        }
                    }
                }
                catch
                {
                    // Ignore metadata parsing errors
                }
            }

            // Extract app_metadata (roles, provider, etc.)
            var appMetadata = claims.FirstOrDefault(c => c.Type == "app_metadata")?.Value;
            if (!string.IsNullOrEmpty(appMetadata))
            {
                try
                {
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(appMetadata);
                    if (metadata != null && metadata.TryGetValue("roles", out var rolesObj))
                    {
                        // Parse roles array
                        if (rolesObj is System.Text.Json.JsonElement rolesElement && 
                            rolesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var role in rolesElement.EnumerateArray())
                            {
                                if (role.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var roleValue = role.GetString();
                                    if (!string.IsNullOrEmpty(roleValue))
                                    {
                                        claims.Add(new Claim(ClaimTypes.Role, roleValue));
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore metadata parsing errors
                }
            }

            // Add default role if none present
            if (!claims.Any(c => c.Type == ClaimTypes.Role))
            {
                var role = claims.FirstOrDefault(c => c.Type == "role")?.Value ?? "user";
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            Console.WriteLine($"✅ Token validated successfully for user: {GetEmailFromClaims(claims)}");
            return claims;
        }
        catch (SecurityTokenExpiredException)
        {
            Console.WriteLine("❌ Token expired");
            return null;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            Console.WriteLine("❌ Token signature validation failed");
            return null;
        }
        catch (SecurityTokenInvalidIssuerException)
        {
            Console.WriteLine("❌ Token issuer validation failed");
            return null;
        }
        catch (SecurityTokenInvalidAudienceException)
        {
            Console.WriteLine("❌ Token audience validation failed");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Token validation failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extract user ID from token (without full validation - use with caution)
    /// </summary>
    public static string? GetUserIdFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                return null;
            }

            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Subject;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract email from token (without full validation - use with caution)
    /// </summary>
    public static string? GetEmailFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                return null;
            }

            var jwtToken = handler.ReadJwtToken(token);
            var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "email");
            return emailClaim?.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if token is expired (without full validation)
    /// </summary>
    public static bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                return true;
            }

            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.ValidTo < DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Get token expiration time
    /// </summary>
    public static DateTime? GetTokenExpiration(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                return null;
            }

            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.ValidTo;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get time until token expires
    /// </summary>
    public static TimeSpan? GetTimeUntilExpiration(string token)
    {
        var expiration = GetTokenExpiration(token);
        if (expiration == null)
        {
            return null;
        }

        var timeRemaining = expiration.Value - DateTime.UtcNow;
        return timeRemaining.TotalSeconds > 0 ? timeRemaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Check if token needs refresh (less than 5 minutes remaining)
    /// </summary>
    public static bool ShouldRefreshToken(string token)
    {
        var timeRemaining = GetTimeUntilExpiration(token);
        if (timeRemaining == null)
        {
            return true; // Can't determine, better to refresh
        }

        return timeRemaining.Value.TotalMinutes < 5;
    }

    /// <summary>
    /// Extract email from claims list
    /// </summary>
    private static string GetEmailFromClaims(List<Claim> claims)
    {
        return claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value ?? "unknown";
    }

    /// <summary>
    /// Validate token and get user ID (combines validation and extraction)
    /// </summary>
    public static string? ValidateAndGetUserId(string token)
    {
        var claims = ValidateAndExtractClaims(token);
        if (claims == null)
        {
            return null;
        }

        return claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
    }

    /// <summary>
    /// Validate token and get email (combines validation and extraction)
    /// </summary>
    public static string? ValidateAndGetEmail(string token)
    {
        var claims = ValidateAndExtractClaims(token);
        if (claims == null)
        {
            return null;
        }

        return claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
    }
}
