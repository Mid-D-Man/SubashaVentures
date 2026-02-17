// Services/Supabase/SupabaseStorageService.cs
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

    // FIX: Added "avatars" as an alias for "user-avatars" so callers can use either.
    private readonly Dictionary<string, string> _buckets = new()
    {
        { "products",  "product-images" },
        { "users",     "user-avatars" },
        { "avatars",   "user-avatars" },   // ← alias used by Settings page
        { "reviews",   "review-images" }
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

            var bucketId = GetBucketId(bucketName);
            var sanitizedName = SanitizeFileName(Path.GetFileNameWithoutExtension(browserFile.Name));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var extension = Path.GetExtension(browserFile.Name);
            var uniqueFileName = $"{sanitizedName}_{timestamp}{extension}";

            var filePath = string.IsNullOrEmpty(folder)
                ? uniqueFileName
                : $"{folder}/{uniqueFileName}";

            _logger.LogInformation("Uploading to: {BucketId}/{FilePath}", bucketId, filePath);

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                await compression.CompressedStream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            _logger.LogDebug("File size: {Size}KB, Compression: {Status}",
                fileBytes.Length / 1024,
                enableCompression ? "Enabled" : "Disabled");

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
                    ErrorMessage = "Permission denied. Check bucket permissions in Supabase."
                };
            }

            if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Bucket does not exist. Create it in Supabase Dashboard."
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
    /// Upload from stream (LEGACY)
    /// </summary>
    public async Task<StorageUploadResult> UploadImageAsync(
        Stream fileStream,
        string fileName,
        string bucketName = "products",
        string? folder = null)
    {
        try
        {
            _logger.LogWarning("Using legacy stream upload – IBrowserFile method is preferred");

            var bucketId = GetBucketId(bucketName);
            var sanitizedName = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var extension = Path.GetExtension(fileName);
            var uniqueFileName = $"{sanitizedName}_{timestamp}{extension}";

            var filePath = string.IsNullOrEmpty(folder)
                ? uniqueFileName
                : $"{folder}/{uniqueFileName}";

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
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
                return new StorageUploadResult { Success = false, ErrorMessage = "Upload failed" };
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
            return new StorageUploadResult { Success = false, ErrorMessage = ex.Message };
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
            results.Add(await UploadImageAsync(stream, fileName, bucketName, folder));
            await Task.Delay(100);
        }
        return results;
    }

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

            var response = await _supabaseClient.Storage
                .From(bucketId)
                .Remove(new List<string> { filePath });

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
            if (ex.Message.Contains("404") || ex.Message.Contains("not found"))
            {
                _logger.LogWarning("File not found (treating as success): {FilePath}", filePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting: {FilePath}", filePath);
            return false;
        }
    }

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
            if (!validPaths.Any()) return false;

            var bucketId = GetBucketId(bucketName);
            _logger.LogInformation("Deleting {Count} files from {BucketId}", validPaths.Count, bucketId);

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
                _logger.LogWarning("GetPublicUrl returned empty for: {FilePath}", filePath);
            return publicUrl ?? string.Empty;
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
                Size = 0,
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

    public async Task<StorageCapacityCheckResult> CanUploadFileAsync(long fileSizeBytes, string bucketName = "products")
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
            return new StorageCapacityCheckResult { CanUpload = true, CapacityInfo = capacityInfo };
        }
        catch
        {
            return new StorageCapacityCheckResult { CanUpload = false, ErrorMessage = "Unable to verify capacity" };
        }
    }

    public async Task<long> GetBucketSizeAsync(string bucketName = "products") => 0;

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private string GetBucketId(string bucketName)
    {
        if (!_buckets.TryGetValue(bucketName.ToLower(), out var bucketId))
            throw new ArgumentException($"Unknown bucket: {bucketName}");
        return bucketId;
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars)).Replace(" ", "_");
    }

    /// <summary>
    /// Extracts the storage file path from a Supabase public URL.
    /// URL format: https://[ref].supabase.co/storage/v1/object/public/[bucket-id]/[file-path]
    /// Returns null if the URL doesn't match the expected format.
    /// </summary>
    public string? ExtractFilePathFromUrl(string publicUrl, string bucketName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(publicUrl)) return null;
            var bucketId = GetBucketId(bucketName);
            var marker = $"/object/public/{bucketId}/";
            var idx = publicUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? Uri.UnescapeDataString(publicUrl[(idx + marker.Length)..]) : null;
        }
        catch
        {
            return null;
        }
    }
}
