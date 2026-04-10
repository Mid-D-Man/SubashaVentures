// Services/Supabase/SupabaseStorageService.cs
// WebP-aware upload service. Derives the file extension from the compressed content type
// so uploaded files are named *.webp rather than keeping the original *.jpg / *.png.

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
        { "users",    "user-avatars"   },
        { "avatars",  "user-avatars"   },
        { "reviews",  "review-images"  }
    };

    public SupabaseStorageService(
        Client supabaseClient,
        IImageCompressionService compressionService,
        ILogger<SupabaseStorageService> logger)
    {
        _supabaseClient     = supabaseClient;
        _compressionService = compressionService;
        _logger             = logger;
    }

    // ─── Upload (IBrowserFile — recommended) ─────────────────────────────────

    public async Task<StorageUploadResult> UploadImageAsync(
        IBrowserFile browserFile,
        string bucketName        = "products",
        string? folder           = null,
        bool enableCompression   = true)
    {
        try
        {
            var validation = await _compressionService.ValidateImageAsync(browserFile);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Validation failed: {Err}", validation.ErrorMessage);
                return new StorageUploadResult { Success = false, ErrorMessage = validation.ErrorMessage };
            }

            // Compress + convert to WebP (default)
            var compression = await _compressionService.CompressImageAsync(
                browserFile,
                maxWidth          : 2000,
                maxHeight         : 2000,
                quality           : 85,
                enableCompression : enableCompression,
                outputFormat      : ImageOutputFormat.WebP);

            if (!compression.Success || compression.CompressedStream == null)
            {
                return new StorageUploadResult
                {
                    Success      = false,
                    ErrorMessage = compression.ErrorMessage ?? "Compression failed"
                };
            }

            // Derive extension from the actual output content type (may be .webp, .jpg, .png …)
            var outputExt    = ImageCompressionService.MimeToExtension(compression.ContentType);
            var baseName     = SanitizeFileName(Path.GetFileNameWithoutExtension(browserFile.Name));
            var timestamp    = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var uniqueFileName = $"{baseName}_{timestamp}{outputExt}";

            var filePath = string.IsNullOrEmpty(folder)
                ? uniqueFileName
                : $"{folder}/{uniqueFileName}";

            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await compression.CompressedStream.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"[Storage] Uploading {filePath} ({fileBytes.Length / 1024} KB) [{compression.ContentType}]",
                LogLevel.Info);

            var uploadedPath = await _supabaseClient.Storage
                .From(GetBucketId(bucketName))
                .Upload(fileBytes, filePath, new FileOptions
                {
                    ContentType = compression.ContentType,
                    Upsert      = false
                });

            if (string.IsNullOrEmpty(uploadedPath))
            {
                return new StorageUploadResult { Success = false, ErrorMessage = "Supabase returned empty path" };
            }

            var publicUrl = GetPublicUrl(filePath, bucketName);

            await MID_HelperFunctions.DebugMessageAsync(
                $"[Storage] ✓ Uploaded: {uniqueFileName} " +
                $"({compression.OriginalSize / 1024} KB → {compression.CompressedSize / 1024} KB, " +
                $"{compression.CompressionRatio * 100:F1}% saved) [{compression.ContentType}]",
                LogLevel.Info);

            return new StorageUploadResult
            {
                Success     = true,
                FilePath    = filePath,
                PublicUrl   = publicUrl,
                FileSize    = compression.CompressedSize,
                ContentType = compression.ContentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed: {Name}", browserFile.Name);
            return new StorageUploadResult { Success = false, ErrorMessage = BuildUploadErrorMessage(ex) };
        }
    }

    // ─── Upload (Stream — legacy) ─────────────────────────────────────────────

    public async Task<StorageUploadResult> UploadImageAsync(
        Stream fileStream,
        string fileName,
        string bucketName = "products",
        string? folder    = null)
    {
        try
        {
            _logger.LogWarning("[Storage] Legacy stream upload — IBrowserFile method preferred");

            var baseName       = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
            var timestamp      = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var ext            = Path.GetExtension(fileName).ToLowerInvariant();
            // Keep original extension for stream uploads (no conversion possible here)
            var uniqueFileName = $"{baseName}_{timestamp}{ext}";
            var filePath       = string.IsNullOrEmpty(folder) ? uniqueFileName : $"{folder}/{uniqueFileName}";
            var mimeType       = ExtToMime(ext);

            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await fileStream.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var uploadedPath = await _supabaseClient.Storage
                .From(GetBucketId(bucketName))
                .Upload(fileBytes, filePath, new FileOptions { ContentType = mimeType, Upsert = false });

            if (string.IsNullOrEmpty(uploadedPath))
                return new StorageUploadResult { Success = false, ErrorMessage = "Upload failed" };

            return new StorageUploadResult
            {
                Success     = true,
                FilePath    = filePath,
                PublicUrl   = GetPublicUrl(filePath, bucketName),
                FileSize    = fileBytes.Length,
                ContentType = mimeType
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
        string? folder    = null)
    {
        var results = new List<StorageUploadResult>();
        foreach (var (stream, fileName) in files)
        {
            results.Add(await UploadImageAsync(stream, fileName, bucketName, folder));
            await Task.Delay(100);
        }
        return results;
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    public async Task<bool> DeleteImageAsync(string filePath, string bucketName = "products")
    {
        try
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            var bucketId = GetBucketId(bucketName);
            var response = await _supabaseClient.Storage.From(bucketId).Remove(new List<string> { filePath });
            if (response == null) return false;
            _logger.LogInformation("[Storage] ✓ Deleted: {Path}", filePath);
            return true;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            _logger.LogWarning("[Storage] File not found (treating as success): {Path}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed: {Path}", filePath);
            return false;
        }
    }

    public async Task<bool> DeleteImagesAsync(List<string> filePaths, string bucketName = "products")
    {
        try
        {
            var valid = filePaths?.Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (valid == null || !valid.Any()) return false;
            var response = await _supabaseClient.Storage.From(GetBucketId(bucketName)).Remove(valid);
            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch delete failed");
            return false;
        }
    }

    // ─── URL helpers ──────────────────────────────────────────────────────────

    public string GetPublicUrl(string filePath, string bucketName = "products")
    {
        try
        {
            return _supabaseClient.Storage.From(GetBucketId(bucketName)).GetPublicUrl(filePath) ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPublicUrl failed: {Path}", filePath);
            return string.Empty;
        }
    }

    public async Task<string> GetSignedUrlAsync(string filePath, int expiresIn = 3600, string bucketName = "products")
    {
        try
        {
            return await _supabaseClient.Storage.From(GetBucketId(bucketName))
                       .CreateSignedUrl(filePath, expiresIn) ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSignedUrl failed: {Path}", filePath);
            return string.Empty;
        }
    }

    // ─── List / metadata ─────────────────────────────────────────────────────

    public async Task<List<StorageFile>> ListFilesAsync(string folder, string bucketName = "products")
    {
        try
        {
            var files = await _supabaseClient.Storage.From(GetBucketId(bucketName)).List(folder);
            return files.Select(f => new StorageFile
            {
                Name        = f.Name        ?? string.Empty,
                Id          = f.Id          ?? Guid.NewGuid().ToString(),
                UpdatedAt   = f.UpdatedAt   ?? DateTime.UtcNow,
                Size        = 0,
                ContentType = string.Empty
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListFiles failed: {Folder}", folder);
            return new List<StorageFile>();
        }
    }

    public async Task<StorageFileMetadata?> GetFileMetadataAsync(string filePath, string bucketName = "products")
    {
        try
        {
            var dir      = Path.GetDirectoryName(filePath)?.Replace("\\", "/") ?? "";
            var files    = await _supabaseClient.Storage.From(GetBucketId(bucketName)).List(dir);
            var fileName = Path.GetFileName(filePath);
            var file     = files.FirstOrDefault(f => f.Name == fileName);
            if (file == null) return null;

            return new StorageFileMetadata
            {
                Name        = file.Name ?? string.Empty,
                Size        = 0,
                CreatedAt   = file.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt   = file.UpdatedAt ?? DateTime.UtcNow,
                ContentType = string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFileMetadata failed: {Path}", filePath);
            return null;
        }
    }

    // ─── Capacity ─────────────────────────────────────────────────────────────

    public async Task<StorageCapacityInfo> GetStorageCapacityAsync(string bucketName = "products")
    {
        return new StorageCapacityInfo
        {
            TotalCapacityBytes = 100L * 1024L * 1024L * 1024L,
            UsedCapacityBytes  = await GetBucketSizeAsync(bucketName),
            MaxFileSizeBytes   = MaxFileSizeBytes,
            BucketName         = bucketName
        };
    }

    public async Task<StorageCapacityCheckResult> CanUploadFileAsync(long fileSizeBytes, string bucketName = "products")
    {
        if (fileSizeBytes > MaxFileSizeBytes)
        {
            var info = await GetStorageCapacityAsync(bucketName);
            return new StorageCapacityCheckResult
            {
                CanUpload    = false,
                ErrorMessage = $"File exceeds {info.FormattedMaxFileSize} limit",
                CapacityInfo = info
            };
        }
        return new StorageCapacityCheckResult { CanUpload = true };
    }

    public Task<long> GetBucketSizeAsync(string bucketName = "products") =>
        Task.FromResult(0L);

    // ─── Private helpers ──────────────────────────────────────────────────────

    private string GetBucketId(string bucketName)
    {
        if (!_buckets.TryGetValue(bucketName.ToLowerInvariant(), out var id))
            throw new ArgumentException($"Unknown bucket: {bucketName}");
        return id;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid)).Replace(" ", "_");
    }

    private static string ExtToMime(string ext) => ext.ToLowerInvariant() switch
    {
        ".webp" => "image/webp",
        ".jpg"  => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".png"  => "image/png",
        ".gif"  => "image/gif",
        _       => "image/jpeg"
    };

    private static string BuildUploadErrorMessage(Exception ex)
    {
        if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            return "Permission denied. Check bucket permissions in Supabase.";
        if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
            return "Bucket does not exist. Create it in Supabase Dashboard.";
        return ex.Message;
    }

    /// <summary>
    /// Extracts the storage file path from a Supabase public URL.
    /// URL format: https://[ref].supabase.co/storage/v1/object/public/[bucket-id]/[file-path]
    /// </summary>
    public string? ExtractFilePathFromUrl(string publicUrl, string bucketName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(publicUrl)) return null;
            var bucketId = GetBucketId(bucketName);
            var marker   = $"/object/public/{bucketId}/";
            var idx      = publicUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? Uri.UnescapeDataString(publicUrl[(idx + marker.Length)..]) : null;
        }
        catch
        {
            return null;
        }
    }
}
