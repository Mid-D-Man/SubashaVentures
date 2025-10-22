using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Service for managing Supabase configuration
/// </summary>
public interface ISupabaseConfigService
{
    /// <summary>
    /// Get the current Supabase configuration
    /// </summary>
    Task<SupabaseConfig> GetConfigAsync();
    
    /// <summary>
    /// Update Supabase configuration
    /// </summary>
    Task UpdateConfigAsync(SupabaseConfig config);
    
    /// <summary>
    /// Validate Supabase configuration
    /// </summary>
    Task<bool> ValidateConfigAsync();
    
    /// <summary>
    /// Get Supabase URL
    /// </summary>
    string GetSupabaseUrl();
    
    /// <summary>
    /// Get Supabase anonymous key
    /// </summary>
    string GetAnonKey();
}