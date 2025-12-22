// Services/Supabase/ISupabaseEdgeFunctionService.cs
using SubashaVentures.Services.Products;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Service for calling Supabase Edge Functions
/// </summary>
public interface ISupabaseEdgeFunctionService
{
    /// <summary>
    /// Update product analytics with batched interactions
    /// </summary>
    /// <param name="batch">Batch of product interactions</param>
    /// <returns>Edge function response</returns>
    Task<EdgeFunctionResponse<ProductAnalyticsUpdateResult>> UpdateProductAnalyticsAsync(ProductInteractionBatch batch);
    
    /// <summary>
    /// Get server time from edge function
    /// </summary>
    /// <param name="timeType">Type of time (utc, local, etc.)</param>
    /// <returns>Server time as DateTime</returns>
    Task<DateTime> GetServerTimeAsync(string timeType = "utc");
    
    /// <summary>
    /// Health check for edge functions
    /// </summary>
    /// <returns>True if edge functions are accessible</returns>
    Task<bool> HealthCheckAsync();
}

/// <summary>
/// Generic edge function response wrapper
/// </summary>
public class EdgeFunctionResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDetails { get; set; }
}

/// <summary>
/// Result from product analytics update
/// </summary>
public class ProductAnalyticsUpdateResult
{
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> UpdatedProducts { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// Server time response
/// </summary>
public class ServerTimeResponse
{
    public DateTime Time { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string FormattedTime { get; set; } = string.Empty;
}
