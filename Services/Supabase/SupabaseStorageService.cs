// Services/Supabase/SupabaseStorageService.cs -- CORRECTED WITH ASYNC STREAMS
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using Supabase;
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
    
    // IMPORTANT: These bucket names must match your Supabase Storage bucket names exactly!
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
        _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
        _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.LogInformation("SupabaseStorageService initialized with buckets: {Buckets}", 
            string.Join(", ", _buckets.Values));
    }

    public async Task<StorageUploadResult> UploadImageAsync(
        Stream fileStream,
        string fileName,
        string bucketName = "products",
        string? folder = null)
    {
        try
        {
            // FIXED: Use async validation
            var validation = await _compressionService.ValidateImageAsync(fileStream);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Image validation failed: {Error}", validation.ErrorMessage);
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = validation.ErrorMessage
                };
            }

            // Reset stream position after validation
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            // Compress image
            var compression = await _compressionService.CompressImageAsync(fileStream);
            if (!compression.Success || compression.CompressedStream == null)
            {
                _logger.LogWarning("Image compression failed: {Error}", compression.ErrorMessage);
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Image compression failed: " + compression.ErrorMessage
                };
            }

            // Get bucket ID
            var bucketId = GetBucketId(bucketName);
            _logger.LogDebug("Using bucket: {BucketId}", bucketId);

            // Generate unique filename
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var sanitizedFileName = SanitizeFileName(fileName);
            var uniqueFileName = $"{timestamp}-{Path.GetFileNameWithoutExtension(sanitizedFileName)}{Path.GetExtension(sanitizedFileName)}";
            
            var filePath = string.IsNullOrEmpty(folder)
                ? uniqueFileName
                : $"{folder}/{uniqueFileName}";

            _logger.LogDebug("Uploading to path: {FilePath}", filePath);

            // Convert stream to byte array
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                compression.CompressedStream.Position = 0;
                await compression.CompressedStream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            _logger.LogDebug("File size: {Size} bytes", fileBytes.Length);

            try
            {
                // Upload to Supabase Storage
                var uploadedFile = await _supabaseClient.Storage
                    .From(bucketId)
                    .Upload(
                        fileBytes,
                        filePath,
                        new FileOptions 
                        { 
                            ContentType = validation.Format ?? "image/jpeg",
                            Upsert = false
                        });

                if (string.IsNullOrEmpty(uploadedFile))
                {
                    _logger.LogError("Supabase returned empty upload path");
                    return new StorageUploadResult
                    {
                        Success = false,
                        ErrorMessage = "Upload to Supabase failed - no path returned"
                    };
                }

                var publicUrl = GetPublicUrl(filePath, bucketName);

                _logger.LogInformation(
                    "Image uploaded successfully: {FilePath} (Original: {Original}KB, Compressed: {Compressed}KB, Ratio: {Ratio}%)",
                    filePath,
                    validation.FileSize / 1024,
                    compression.CompressedSize / 1024,
                    (compression.CompressionRatio * 100).ToString("F1")
                );

                return new StorageUploadResult
                {
                    Success = true,
                    FilePath = filePath,
                    PublicUrl = publicUrl,
                    FileSize = compression.CompressedSize,
                    ContentType = validation.Format
                };
            }
            catch (Exception uploadEx)
            {
                _logger.LogError(uploadEx, "Supabase upload failed for file: {FileName}", fileName);
                
                // Check if it's a bucket permission issue
                if (uploadEx.Message.Contains("403") || uploadEx.Message.Contains("Forbidden"))
                {
                    return new StorageUploadResult
                    {
                        Success = false,
                        ErrorMessage = $"Permission denied. Please check bucket '{bucketId}' permissions in Supabase Dashboard."
                    };
                }
                
                // Check if bucket doesn't exist
                if (uploadEx.Message.Contains("404") || uploadEx.Message.Contains("Not Found"))
                {
                    return new StorageUploadResult
                    {
                        Success = false,
                        ErrorMessage = $"Bucket '{bucketId}' does not exist. Please create it in Supabase Dashboard."
                    };
                }
                
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = $"Upload error: {uploadEx.Message}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UploadImageAsync for file: {FileName}", fileName);
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
            
            // Small delay between uploads to avoid rate limiting
            await Task.Delay(100);
        }
        
        return results;
    }

    public async Task<bool> DeleteImageAsync(string filePath, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            await _supabaseClient.Storage.From(bucketId).Remove(new List<string> { filePath });
            
            _logger.LogInformation("Image deleted: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> DeleteImagesAsync(List<string> filePaths, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            await _supabaseClient.Storage.From(bucketId).Remove(filePaths);
            
            _logger.LogInformation("Deleted {Count} images", filePaths.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting multiple images");
            return false;
        }
    }

    public string GetPublicUrl(string filePath, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            return _supabaseClient.Storage.From(bucketId).GetPublicUrl(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting public URL for: {FilePath}", filePath);
            return string.Empty;
        }
    }

    public async Task<string> GetSignedUrlAsync(string filePath, int expiresIn = 3600, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            var signedUrl = await _supabaseClient.Storage.From(bucketId).CreateSignedUrl(filePath, expiresIn);
            return signedUrl ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating signed URL for: {FilePath}", filePath);
            return string.Empty;
        }
    }

    public async Task<List<StorageFile>> ListFilesAsync(string folder, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            _logger.LogDebug("Listing files in bucket: {BucketId}, folder: {Folder}", bucketId, folder);
            
            var files = await _supabaseClient.Storage.From(bucketId).List(folder);

            return files.Select(f => new StorageFile
            {
                Name = f.Name ?? string.Empty,
                Id = f.Id ?? string.Empty,
                UpdatedAt = f.UpdatedAt ?? DateTime.UtcNow,
                Size = 0, // Supabase doesn't expose size in list
                ContentType = string.Empty
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files in folder: {Folder}, bucket: {Bucket}", folder, bucketName);
            
            // Provide helpful error message
            if (ex.Message.Contains("500") || ex.Message.Contains("Internal Server Error"))
            {
                _logger.LogError("Bucket '{BucketId}' may not exist or has permission issues. Please check Supabase Dashboard.", GetBucketId(bucketName));
            }
            
            return new List<StorageFile>();
        }
    }

    public async Task<StorageFileMetadata?> GetFileMetadataAsync(string filePath, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            var directoryPath = Path.GetDirectoryName(filePath)?.Replace("\\", "/") ?? "";
            var files = await _supabaseClient.Storage.From(bucketId).List(directoryPath);

            var fileName = Path.GetFileName(filePath);
            var file = files.FirstOrDefault(f => f.Name == fileName);
            
            if (file == null)
                return null;

            return new StorageFileMetadata
            {
                Name = file.Name ?? string.Empty,
                Size = 0,
                CreatedAt = file.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = file.UpdatedAt ?? DateTime.UtcNow,
                ContentType = string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata for: {FilePath}", filePath);
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
            _logger.LogError(ex, "Error getting storage capacity for bucket: {BucketName}", bucketName);
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
            _logger.LogError(ex, "Error checking upload capacity");
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
            _logger.LogWarning("GetBucketSizeAsync not fully implemented - Supabase doesn't expose file sizes in list");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating bucket size for: {BucketName}", bucketName);
            return 0;
        }
    }

    private string GetBucketId(string bucketName)
    {
        if (!_buckets.TryGetValue(bucketName.ToLower(), out var bucketId))
        {
            _logger.LogError("Unknown bucket requested: {BucketName}. Available buckets: {Available}", 
                bucketName, string.Join(", ", _buckets.Keys));
            throw new ArgumentException($"Unknown bucket: {bucketName}. Available: {string.Join(", ", _buckets.Keys)}");
        }
        
        return bucketId;
    }

    private string SanitizeFileName(string fileName)
    {
        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars));
        
        // Replace spaces with hyphens
        sanitized = sanitized.Replace(" ", "-");
        
        return sanitized;
    }
}
