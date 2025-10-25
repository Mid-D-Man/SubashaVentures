// Services/Supabase/ISupabaseStorageService.cs - Updated Interface
namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Service for managing file uploads to Supabase Storage
/// Handles compression, optimization, and retrieval of images
/// </summary>
public interface ISupabaseStorageService
{
    // ===== UPLOAD/DOWNLOAD OPERATIONS =====
    
    Task<StorageUploadResult> UploadImageAsync(
        Stream fileStream, 
        string fileName, 
        string bucketName = "products",
        string? folder = null);
    
    Task<List<StorageUploadResult>> UploadImagesAsync(
        List<(Stream stream, string fileName)> files,
        string bucketName = "products",
        string? folder = null);
    
    Task<bool> DeleteImageAsync(string filePath, string bucketName = "products");
    Task<bool> DeleteImagesAsync(List<string> filePaths, string bucketName = "products");
    
    // ===== URL OPERATIONS =====
    
    string GetPublicUrl(string filePath, string bucketName = "products");
    Task<string> GetSignedUrlAsync(string filePath, int expiresIn = 3600, string bucketName = "products");
    
    // ===== FILE MANAGEMENT =====
    
    Task<List<StorageFile>> ListFilesAsync(string folder, string bucketName = "products");
    Task<StorageFileMetadata?> GetFileMetadataAsync(string filePath, string bucketName = "products");
    
    // ===== STORAGE CAPACITY & LIMITS =====
    
    /// <summary>
    /// Get storage capacity information for a bucket
    /// Supabase Free Plan: 1GB per bucket
    /// </summary>
    Task<StorageCapacityInfo> GetStorageCapacityAsync(string bucketName = "products");
    
    /// <summary>
    /// Check if file can be uploaded based on size limit and bucket capacity
    /// </summary>
    Task<StorageCapacityCheckResult> CanUploadFileAsync(
        long fileSizeBytes, 
        string bucketName = "products");
    
    /// <summary>
    /// Get total size of all files in a bucket
    /// </summary>
    Task<long> GetBucketSizeAsync(string bucketName = "products");
}

// ===== DATA MODELS =====

/// <summary>
/// Result of a storage upload operation
/// </summary>
public class StorageUploadResult
{
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? PublicUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
}

/// <summary>
/// Storage file information
/// </summary>
public class StorageFile
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public long Size { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

/// <summary>
/// File metadata
/// </summary>
public class StorageFileMetadata
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

/// <summary>
/// Storage capacity information
/// </summary>
public class StorageCapacityInfo
{
    /// <summary>
    /// Total capacity in bytes (1GB = 1,073,741,824 bytes for free plan)
    /// </summary>
    public long TotalCapacityBytes { get; set; }
    
    /// <summary>
    /// Used capacity in bytes
    /// </summary>
    public long UsedCapacityBytes { get; set; }
    
    /// <summary>
    /// Available capacity in bytes
    /// </summary>
    public long AvailableCapacityBytes => TotalCapacityBytes - UsedCapacityBytes;
    
    /// <summary>
    /// Usage percentage (0-100)
    /// </summary>
    public double UsagePercentage => TotalCapacityBytes > 0 
        ? (UsedCapacityBytes / (double)TotalCapacityBytes) * 100 
        : 0;
    
    /// <summary>
    /// Maximum file size in bytes (50MB for Supabase)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
    
    /// <summary>
    /// Bucket name
    /// </summary>
    public string BucketName { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable format strings
    /// </summary>
    public string FormattedTotalCapacity => FormatBytes(TotalCapacityBytes);
    public string FormattedUsedCapacity => FormatBytes(UsedCapacityBytes);
    public string FormattedAvailableCapacity => FormatBytes(AvailableCapacityBytes);
    public string FormattedMaxFileSize => FormatBytes(MaxFileSizeBytes);
    
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Result of checking if a file can be uploaded
/// </summary>
public class StorageCapacityCheckResult
{
    public bool CanUpload { get; set; }
    public string? ErrorMessage { get; set; }
    public StorageCapacityInfo? CapacityInfo { get; set; }
    public double? EstimatedUsagePercentageAfterUpload { get; set; }
}