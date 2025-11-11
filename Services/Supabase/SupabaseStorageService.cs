// Services/Supabase/SupabaseStorageService.cs - FINAL FIX
using Microsoft.AspNetCore.Components.Forms;
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
    
    private const long MaxFileSizeBytes = 50L * 1024L * 1024L;
    
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

    /// <summary>
    /// Upload image with optional compression (RECOMMENDED METHOD)
    /// </summary>
    public async Task<StorageUploadResult> UploadImageAsync(
        IBrowserFile browserFile,
        string bucketName = "products",
        string? folder = null,
        bool enableCompression = true)
    {
        try
        {
            // Validate image
            var validation = await _compressionService.ValidateImageAsync(browserFile);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Image validation failed: {Error}", validation.ErrorMessage);
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = validation.ErrorMessage
                };
            }

            // Compress or use original
            var compression = await _compressionService.CompressImageAsync(
                browserFile,
                maxWidth: 2000,
                maxHeight: 2000,
                quality: 80,
                enableCompression: enableCompression
            );

            if (!compression.Success || compression.CompressedStream == null)
            {
                _logger.LogError("Compression failed: {Error}", compression.ErrorMessage);
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = compression.ErrorMessage ?? "Compression failed"
                };
            }

            // Generate filename: name_timestamp.ext
            var bucketId = GetBucketId(bucketName);
            var sanitizedName = SanitizeFileName(Path.GetFileNameWithoutExtension(browserFile.Name));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var extension = Path.GetExtension(browserFile.Name);
            var uniqueFileName = $"{sanitizedName}_{timestamp}{extension}";
            
            var filePath = string.IsNullOrEmpty(folder)
                ? uniqueFileName
                : $"{folder}/{uniqueFileName}";

            _logger.LogInformation("Uploading to: {BucketId}/{FilePath}", bucketId, filePath);

            // CRITICAL FIX: Don't set Position on BrowserFileStream - just read it
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                // BrowserFileStream doesn't support Position - just copy from current position
                await compression.CompressedStream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            _logger.LogDebug("File size: {Size}KB, Compression: {Status}", 
                fileBytes.Length / 1024, 
                enableCompression ? "Enabled" : "Disabled");

            // Upload to Supabase
            var uploadedPath = await _supabaseClient.Storage
                .From(bucketId)
                .Upload(
                    fileBytes,
                    filePath,
                    new FileOptions
                    {
                        ContentType = validation.Format ?? "image/jpeg",
                        Upsert = false
                    });

            if (string.IsNullOrEmpty(uploadedPath))
            {
                _logger.LogError("Supabase returned empty path");
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Upload failed - no path returned"
                };
            }

            var publicUrl = GetPublicUrl(filePath, bucketName);

            _logger.LogInformation(
                "✓ Upload successful: {FileName} ({Original}KB → {Final}KB, {Ratio}% reduction)",
                uniqueFileName,
                compression.OriginalSize / 1024,
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for: {FileName}", browserFile.Name);
            
            if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = $"Permission denied. Check bucket permissions in Supabase."
                };
            }
            
            if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = $"Bucket does not exist. Create it in Supabase Dashboard."
                };
            }
            
            return new StorageUploadResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Upload from stream (LEGACY - Less reliable)
    /// </summary>
    public async Task<StorageUploadResult> UploadImageAsync(
        Stream fileStream,
        string fileName,
        string bucketName = "products",
        string? folder = null)
    {
        try
        {
            _logger.LogWarning("Using legacy stream upload - IBrowserFile method is preferred");

            var bucketId = GetBucketId(bucketName);
            var sanitizedName = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var extension = Path.GetExtension(fileName);
            var uniqueFileName = $"{sanitizedName}_{timestamp}{extension}";
            
            var filePath = string.IsNullOrEmpty(folder)
                ? uniqueFileName
                : $"{folder}/{uniqueFileName}";

            // Read entire stream - DON'T use Position
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                // Just copy from current position
                await fileStream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            var uploadedPath = await _supabaseClient.Storage
                .From(bucketId)
                .Upload(
                    fileBytes,
                    filePath,
                    new FileOptions
                    {
                        ContentType = "image/jpeg",
                        Upsert = false
                    });

            if (string.IsNullOrEmpty(uploadedPath))
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Upload failed"
                };
            }

            return new StorageUploadResult
            {
                Success = true,
                FilePath = filePath,
                PublicUrl = GetPublicUrl(filePath, bucketName),
                FileSize = fileBytes.Length,
                ContentType = "image/jpeg"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stream upload failed");
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
            await Task.Delay(100);
        }
        
        return results;
    }

    /// <summary>
/// Delete a single image from storage
/// FIXED: Proper error handling and path formatting
/// </summary>
public async Task<bool> DeleteImageAsync(string filePath, string bucketName = "products")
{
    try
    {
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.LogWarning("Cannot delete: filePath is null or empty");
            return false;
        }

        var bucketId = GetBucketId(bucketName);

        _logger.LogInformation("Attempting to delete: {BucketId}/{FilePath}", bucketId, filePath);

        // Remove method expects a List<string> of file paths
        var pathsToDelete = new List<string> { filePath };

        var response = await _supabaseClient.Storage
            .From(bucketId)
            .Remove(pathsToDelete);

        if (response == null)
        {
            _logger.LogError("Delete failed: No response from Supabase");
            return false;
        }

        _logger.LogInformation("✓ Successfully deleted: {FilePath}", filePath);
        return true;
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "HTTP error deleting {FilePath}: {Message}", filePath, ex.Message);

        if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
        {
            _logger.LogError("Permission denied. Check RLS policies for DELETE operation on storage.objects");
        }
        else if (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            _logger.LogWarning("File not found: {FilePath}", filePath);
            return true; // Treat as success since file doesn't exist
        }

        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error deleting: {FilePath}", filePath);
        return false;
    }
}

/// <summary>
/// Delete multiple images from storage
/// FIXED: Batch deletion with proper error handling
/// </summary>
public async Task<bool> DeleteImagesAsync(List<string> filePaths, string bucketName = "products")
{
    try
    {
        if (filePaths == null || !filePaths.Any())
        {
            _logger.LogWarning("Cannot delete: filePaths is null or empty");
            return false;
        }

        var validPaths = filePaths.Where(p => !string.IsNullOrEmpty(p)).ToList();

        if (!validPaths.Any())
        {
            _logger.LogWarning("No valid paths to delete");
            return false;
        }

        var bucketId = GetBucketId(bucketName);

        _logger.LogInformation("Attempting to delete {Count} files from {BucketId}", validPaths.Count, bucketId);

        var response = await _supabaseClient.Storage
            .From(bucketId)
            .Remove(validPaths);

        if (response == null)
        {
            _logger.LogError("Batch delete failed: No response from Supabase");
            return false;
        }

        _logger.LogInformation("✓ Successfully deleted {Count} files", validPaths.Count);
        return true;
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "HTTP error during batch delete: {Message}", ex.Message);

        if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
        {
            _logger.LogError("Permission denied. Ensure DELETE policy exists for storage.objects");
        }

        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during batch delete");
        return false;
    }
}
    
public string GetPublicUrl(string filePath, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            var publicUrl = _supabaseClient.Storage.From(bucketId).GetPublicUrl(filePath);
            
            if (string.IsNullOrEmpty(publicUrl))
            {
                _logger.LogWarning("GetPublicUrl returned empty for: {FilePath}", filePath);
                return string.Empty;
            }

            return publicUrl;
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
            _logger.LogError(ex, "Error creating signed URL: {FilePath}", filePath);
            return string.Empty;
        }
    }

    // Also update ListFilesAsync to return proper file metadata
public async Task<List<StorageFile>> ListFilesAsync(string folder, string bucketName = "products")
{
    try
    {
        var bucketId = GetBucketId(bucketName);
        var files = await _supabaseClient.Storage.From(bucketId).List(folder);

        return files.Select(f => new StorageFile
        {
            Name = f.Name ?? string.Empty,
            Id = f.Id ?? Guid.NewGuid().ToString(),
            UpdatedAt = f.UpdatedAt ?? DateTime.UtcNow,
            Size = 0, // Supabase C# SDK doesn't return size in list
            ContentType = string.Empty
        }).ToList();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "List failed for folder: {Folder}", folder);
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
            
            if (file == null) return null;

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
            _logger.LogError(ex, "Metadata failed: {FilePath}", filePath);
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
                TotalCapacityBytes = 100L * 1024L * 1024L * 1024L,
                UsedCapacityBytes = usedBytes,
                MaxFileSizeBytes = MaxFileSizeBytes,
                BucketName = bucketName
            };
        }
        catch
        {
            return new StorageCapacityInfo
            {
                TotalCapacityBytes = 100L * 1024L * 1024L * 1024L,
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
                    ErrorMessage = $"File exceeds {capacityInfo.FormattedMaxFileSize} limit",
                    CapacityInfo = capacityInfo
                };
            }

            return new StorageCapacityCheckResult
            {
                CanUpload = true,
                CapacityInfo = capacityInfo
            };
        }
        catch
        {
            return new StorageCapacityCheckResult
            {
                CanUpload = false,
                ErrorMessage = "Unable to verify capacity"
            };
        }
    }

    public async Task<long> GetBucketSizeAsync(string bucketName = "products")
    {
        return 0;
    }

    private string GetBucketId(string bucketName)
    {
        if (!_buckets.TryGetValue(bucketName.ToLower(), out var bucketId))
        {
            throw new ArgumentException($"Unknown bucket: {bucketName}");
        }
        return bucketId;
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars));
        sanitized = sanitized.Replace(" ", "_");
        return sanitized;
    }
}
