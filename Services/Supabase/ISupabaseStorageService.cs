// Services/Supabase/ISupabaseStorageService.cs - Updated Interface
// Services/Supabase/ISupabaseStorageService.cs
using Microsoft.AspNetCore.Components.Forms;

namespace SubashaVentures.Services.Supabase;

public interface ISupabaseStorageService
{
    // RECOMMENDED: Upload using IBrowserFile with optional compression
    Task<StorageUploadResult> UploadImageAsync(
        IBrowserFile browserFile,
        string bucketName = "products",
        string? folder = null,
        bool enableCompression = true);
    
    // LEGACY: Upload from stream (less reliable)
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
    
    string GetPublicUrl(string filePath, string bucketName = "products");
    Task<string> GetSignedUrlAsync(string filePath, int expiresIn = 3600, string bucketName = "products");
    
    Task<List<StorageFile>> ListFilesAsync(string folder, string bucketName = "products");
    Task<StorageFileMetadata?> GetFileMetadataAsync(string filePath, string bucketName = "products");
    
    Task<StorageCapacityInfo> GetStorageCapacityAsync(string bucketName = "products");
    Task<StorageCapacityCheckResult> CanUploadFileAsync(long fileSizeBytes, string bucketName = "products");
    Task<long> GetBucketSizeAsync(string bucketName = "products");
}

// Data models remain the same...
public class StorageUploadResult
{
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? PublicUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
}

public class StorageFile
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public long Size { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

public class StorageFileMetadata
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

public class StorageCapacityInfo
{
    public long TotalCapacityBytes { get; set; }
    public long UsedCapacityBytes { get; set; }
    public long AvailableCapacityBytes => TotalCapacityBytes - UsedCapacityBytes;
    public double UsagePercentage => TotalCapacityBytes > 0 
        ? (UsedCapacityBytes / (double)TotalCapacityBytes) * 100 
        : 0;
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
    public string BucketName { get; set; } = string.Empty;
    
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

public class StorageCapacityCheckResult
{
    public bool CanUpload { get; set; }
    public string? ErrorMessage { get; set; }
    public StorageCapacityInfo? CapacityInfo { get; set; }
    public double? EstimatedUsagePercentageAfterUpload { get; set; }
}