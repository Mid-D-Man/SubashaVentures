using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Storage;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using FileOptions = Supabase.Storage.FileOptions;
namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Supabase Storage Service Implementation
/// Handles image uploads, downloads, and management
/// </summary>
public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly Client _supabaseClient;
    private readonly IImageCompressionService _compressionService;
    private readonly ILogger<SupabaseStorageService> _logger;
    
    // Limits - Fixed overflow by using long literal suffix
    private const long MaxFileSizeBytes = 50L * 1024L * 1024L; // 50 MB
    private const long WarningThresholdBytes = 40L * 1024L * 1024L; // 40 MB
    private const long BucketStorageLimit = 100L * 1024L * 1024L * 1024L; // 100 GB per bucket
    
    // Storage buckets
    private readonly Dictionary<string, string> _buckets = new()
    {
        { "products", "product-images" },
        { "users", "user-avatars" },
        { "reviews", "review-images" }
    };

    public SupabaseStorageService(
        Client supabaseClient,
        IImageCompressionService compressionService,
        ILogger<SupabaseStorageService> logger)
    {
        _supabaseClient = supabaseClient;
        _compressionService = compressionService;
        _logger = logger;
    }

    public async Task<StorageUploadResult> UploadImageAsync(
        Stream fileStream,
        string fileName,
        string bucketName = "products",
        string? folder = null)
    {
        try
        {
            // Validate image
            var validation = _compressionService.ValidateImage(fileStream);
            if (!validation.IsValid)
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = validation.ErrorMessage
                };
            }

            // Compress image
            var compression = await _compressionService.CompressImageAsync(fileStream);
            if (!compression.Success || compression.CompressedStream == null)
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Image compression failed"
                };
            }

            // Prepare file path
            var bucketId = GetBucketId(bucketName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var uniqueFileName = $"{timestamp}-{Path.GetFileNameWithoutExtension(fileName)}{Path.GetExtension(fileName)}";
            
            var filePath = string.IsNullOrEmpty(folder)
                ? uniqueFileName
                : $"{folder}/{uniqueFileName}";

            // Convert stream to byte array for upload
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                compression.CompressedStream.Position = 0;
                await compression.CompressedStream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            // Upload to Supabase Storage - Correct syntax without .Storage
            var uploadedFile = await _supabaseClient
                .From(bucketId)
                .Upload(
                    fileBytes,
                    filePath,
                    new FileOptions 
                    { 
                        ContentType = validation.Format,
                        Upsert = false
                    });

            if (string.IsNullOrEmpty(uploadedFile))
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Upload to Supabase failed"
                };
            }

            var publicUrl = GetPublicUrl(filePath, bucketName);

            MID_HelperFunctions.DebugMessage(
                $"Image uploaded successfully: {filePath} (Compressed: {(compression.CompressionRatio * 100):F1}%)",
                LogLevel.Info);

            return new StorageUploadResult
            {
                Success = true,
                FilePath = filePath,
                PublicUrl = publicUrl,
                FileSize = compression.CompressedSize,
                ContentType = validation.Format
            };
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, $"Error uploading image: {fileName}");
            return new StorageUploadResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<List<StorageUploadResult>> UploadImagesAsync(
        List<(Stream stream, string fileName)> files,
        string bucketName = "products",
        string? folder = null)
    {
        var results = new List<StorageUploadResult>();

        foreach (var (stream, fileName) in files)
        {
            var result = await UploadImageAsync(stream, fileName, bucketName, folder);
            results.Add(result);
        }

        return results;
    }

    public async Task<bool> DeleteImageAsync(string filePath, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            
            await _supabaseClient
                .From(bucketId)
                .Remove(new List<string> { filePath });

            MID_HelperFunctions.DebugMessage($"Image deleted: {filePath}", LogLevel.Info);
            return true;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, $"Error deleting image: {filePath}");
            return false;
        }
    }

    public async Task<bool> DeleteImagesAsync(List<string> filePaths, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            
            await _supabaseClient
                .From(bucketId)
                .Remove(filePaths);

            MID_HelperFunctions.DebugMessage($"Deleted {filePaths.Count} images", LogLevel.Info);
            return true;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, "Error deleting multiple images");
            return false;
        }
    }

    public string GetPublicUrl(string filePath, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            
            var publicUrl = _supabaseClient
                .From(bucketId)
                .GetPublicUrl(filePath);

            return publicUrl;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, $"Error getting public URL for: {filePath}");
            return string.Empty;
        }
    }

    public async Task<string> GetSignedUrlAsync(string filePath, int expiresIn = 3600, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            
            var signedUrl = await _supabaseClient
                .From(bucketId)
                .CreateSignedUrl(filePath, expiresIn);

            return signedUrl ?? string.Empty;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, $"Error creating signed URL for: {filePath}");
            return string.Empty;
        }
    }

    public async Task<List<StorageFile>> ListFilesAsync(string folder, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            
            var files = await _supabaseClient
                .From(bucketId)
                .List(folder);

            return files.Select(f => new StorageFile
            {
                Name = f.Name ?? string.Empty,
                Id = f.Id ?? string.Empty,
                UpdatedAt = f.UpdatedAt ?? DateTime.UtcNow,
                Size = f.Metadata?.Size ?? 0,
                ContentType = f.Metadata?.MimeType ?? string.Empty
            }).ToList();
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, $"Error listing files in folder: {folder}");
            return new List<StorageFile>();
        }
    }

    public async Task<StorageFileMetadata?> GetFileMetadataAsync(string filePath, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            var directoryPath = Path.GetDirectoryName(filePath)?.Replace("\\", "/") ?? "";
            var files = await _supabaseClient
                .From(bucketId)
                .List(directoryPath);

            var fileName = Path.GetFileName(filePath);
            var file = files.FirstOrDefault(f => f.Name == fileName);
            
            if (file == null)
                return null;

            return new StorageFileMetadata
            {
                Name = file.Name ?? string.Empty,
                Size = file.Metadata?.Size ?? 0,
                CreatedAt = file.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = file.UpdatedAt ?? DateTime.UtcNow,
                ContentType = file.Metadata?.MimeType ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, $"Error getting metadata for: {filePath}");
            return null;
        }
    }

    public async Task<StorageCapacityInfo> GetStorageCapacityAsync(string bucketName = "products")
    {
        try
        {
            var usedBytes = await GetBucketSizeAsync(bucketName);
            
            return new StorageCapacityInfo
            {
                TotalCapacityBytes = BucketStorageLimit,
                UsedCapacityBytes = usedBytes,
                MaxFileSizeBytes = MaxFileSizeBytes,
                BucketName = bucketName
            };
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, $"Error getting storage capacity for bucket: {bucketName}");
            return new StorageCapacityInfo
            {
                TotalCapacityBytes = BucketStorageLimit,
                UsedCapacityBytes = 0,
                MaxFileSizeBytes = MaxFileSizeBytes,
                BucketName = bucketName
            };
        }
    }

    public async Task<StorageCapacityCheckResult> CanUploadFileAsync(
        long fileSizeBytes,
        string bucketName = "products")
    {
        try
        {
            var capacityInfo = await GetStorageCapacityAsync(bucketName);
            
            if (fileSizeBytes > MaxFileSizeBytes)
            {
                return new StorageCapacityCheckResult
                {
                    CanUpload = false,
                    ErrorMessage = $"File size exceeds maximum allowed size of {capacityInfo.FormattedMaxFileSize}",
                    CapacityInfo = capacityInfo
                };
            }

            var availableSpace = capacityInfo.AvailableCapacityBytes;
            
            if (fileSizeBytes > availableSpace)
            {
                return new StorageCapacityCheckResult
                {
                    CanUpload = false,
                    ErrorMessage = $"Insufficient storage space. Available: {capacityInfo.FormattedAvailableCapacity}",
                    CapacityInfo = capacityInfo
                };
            }

            var estimatedUsageAfterUpload = 
                ((capacityInfo.UsedCapacityBytes + fileSizeBytes) / (double)capacityInfo.TotalCapacityBytes) * 100;

            return new StorageCapacityCheckResult
            {
                CanUpload = true,
                CapacityInfo = capacityInfo,
                EstimatedUsagePercentageAfterUpload = estimatedUsageAfterUpload
            };
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, "Error checking upload capacity");
            return new StorageCapacityCheckResult
            {
                CanUpload = false,
                ErrorMessage = "Unable to verify storage capacity"
            };
        }
    }

    public async Task<long> GetBucketSizeAsync(string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            var files = await _supabaseClient
                .From(bucketId)
                .List("");

            long totalSize = 0;
            
            foreach (var file in files)
            {
                totalSize += file.Metadata?.Size ?? 0;
            }

            return totalSize;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, $"Error calculating bucket size for: {bucketName}");
            return 0;
        }
    }

    private string GetBucketId(string bucketName)
    {
        return _buckets.TryGetValue(bucketName.ToLower(), out var bucketId)
            ? bucketId
            : throw new ArgumentException($"Unknown bucket: {bucketName}");
    }
}