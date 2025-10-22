namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Supabase configuration model
/// </summary>
public class SupabaseConfig
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
    public bool AutoRefreshToken { get; set; } = true;
    public bool PersistSession { get; set; } = true;
    public int SessionTimeout { get; set; } = 3600; // 1 hour in seconds
}

/// <summary>
/// Supabase session information
/// </summary>
public class SupabaseSessionInfo
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

/// <summary>
/// Supabase authentication result
/// </summary>
public class SupabaseAuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public SupabaseSessionInfo? Session { get; set; }
    public UserModel? User { get; set; }
}

/// <summary>
/// Supabase error response
/// </summary>
public class SupabaseError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}