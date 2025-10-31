using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Storage;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using FileOptions = Supabase.Storage.FileOptions;

namespace SubashaVentures.Services.Supabase;

public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly Client _supabaseClient;
    private readonly IImageCompressionService _compressionService;
    private readonly ILogger<SupabaseStorageService> _logger;
    
    // Limits
    private const long MaxFileSizeBytes = 50L * 1024L * 1024L; // 50 MB
    private const long WarningThresholdBytes = 40L * 1024L * 1024L; // 40 MB
    private const long BucketStorageLimit = 100L * 1024L * 1024L * 1024L; // 100 GB per bucket
    
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
            var validation = _compressionService.ValidateImage(fileStream);
            if (!validation.IsValid)
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = validation.ErrorMessage
                };
            }

            var compression = await _compressionService.CompressImageAsync(fileStream);
            if (!compression.Success || compression.CompressedStream == null)
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Image compression failed"
                };
            }

            var bucketId = GetBucketId(bucketName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var uniqueFileName = $"{timestamp}-{Path.GetFileNameWithoutExtension(fileName)}{Path.GetExtension(fileName)}";
            
            var filePath = string.IsNullOrEmpty(folder)
                ? uniqueFileName
                : $"{folder}/{uniqueFileName}";

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                compression.CompressedStream.Position = 0;
                await compression.CompressedStream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

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
            await _supabaseClient.From(bucketId).Remove(new List<string> { filePath });
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
            await _supabaseClient.From(bucketId).Remove(filePaths);
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
            return _supabaseClient.From(bucketId).GetPublicUrl(filePath);
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
            var signedUrl = await _supabaseClient.From(bucketId).CreateSignedUrl(filePath, expiresIn);
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
            var files = await _supabaseClient.From(bucketId).List(folder);

            return files.Select(f => new StorageFile
            {
                Name = f.Name ?? string.Empty,
                Id = f.Id ?? string.Empty,
                UpdatedAt = f.UpdatedAt ?? DateTime.UtcNow,
                // Note: Size is not available directly from FileObject
                // You would need to query the storage.objects table directly or download the file
                Size = 0, // FileObject doesn't expose size
                ContentType = string.Empty // FileObject doesn't expose content type
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
            var files = await _supabaseClient.From(bucketId).List(directoryPath);

            var fileName = Path.GetFileName(filePath);
            var file = files.FirstOrDefault(f => f.Name == fileName);
            
            if (file == null)
                return null;

            return new StorageFileMetadata
            {
                Name = file.Name ?? string.Empty,
                Size = 0, // FileObject doesn't expose size - would need database query
                CreatedAt = file.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = file.UpdatedAt ?? DateTime.UtcNow,
                ContentType = string.Empty // FileObject doesn't expose content type
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
            // Note: The C# client doesn't provide direct bucket size calculation
            // You would need to query the storage.objects table directly via Postgrest
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
            // WARNING: The C# FileObject class doesn't expose file size
            // To get accurate bucket size, you would need to:
            // 1. Query the storage.objects table directly via Postgrest
            // 2. Sum the metadata->'size' field from the JSONB column
            
            // For now, returning 0 as a placeholder
            // You'll need to implement a Postgrest query like:
            // SELECT SUM((metadata->>'size')::bigint) FROM storage.objects WHERE bucket_id = 'bucket-name'
            
            _logger.LogWarning("GetBucketSizeAsync not fully implemented - FileObject doesn't expose size");
            return 0;
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
