// Services/Supabase/SupabaseStorageService.cs

using SubashaVentures.Services.Storage;
using Supabase.Storage;
using SubashaVentures.Utilities.HelperScripts;

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
    
    // Limits
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private const long WarningThresholdBytes = 40 * 1024 * 1024; // 40 MB
    
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

            // Upload to Supabase Storage
            var uploadedFile = await _supabaseClient
                .Storage
                .From(bucketId)
                .Upload(
                    compression.CompressedStream,
                    filePath,
                    new FileOptions { ContentType = validation.Format });

            if (string.IsNullOrEmpty(uploadedFile))
            {
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Upload to Supabase failed"
                };
            }

            var publicUrl = GetPublicUrl(filePath, bucketName);

            _logger.LogInformation("Image uploaded successfully: {FilePath} (Compressed: {Ratio}%)",
                filePath,
                (compression.CompressionRatio * 100).ToString("F1"));

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
            _logger.LogError(ex, "Error uploading image: {FileName}", fileName);
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
                .Storage
                .From(bucketId)
                .Remove(new List<string> { filePath });

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
            
            await _supabaseClient
                .Storage
                .From(bucketId)
                .Remove(filePaths);

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
            
            var publicUrl = _supabaseClient
                .Storage
                .From(bucketId)
                .GetPublicUrl(filePath);

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
            
            var signedUrl = await _supabaseClient
                .Storage
                .From(bucketId)
                .CreateSignedUrl(filePath, expiresIn);

            return signedUrl;
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
            
            var files = await _supabaseClient
                .Storage
                .From(bucketId)
                .List(folder);

            return files.Select(f => new StorageFile
            {
                Name = f.Name,
                Id = f.Id,
                UpdatedAt = f.UpdatedAt,
                Size = f.Size,
                ContentType = f.ContentType
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files in folder: {Folder}", folder);
            return new List<StorageFile>();
        }
    }

    public async Task<StorageFileMetadata?> GetFileMetadataAsync(string filePath, string bucketName = "products")
    {
        try
        {
            var bucketId = GetBucketId(bucketName);
            var files = await _supabaseClient
                .Storage
                .From(bucketId)
                .List(Path.GetDirectoryName(filePath) ?? "");

            var file = files.FirstOrDefault(f => f.Name == Path.GetFileName(filePath));
            
            if (file == null)
                return null;

            return new StorageFileMetadata
            {
                Name = file.Name,
                Size = file.Size,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = file.UpdatedAt,
                ContentType = file.ContentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata for: {FilePath}", filePath);
            return null;
        }
    }

    private string GetBucketId(string bucketName)
    {
        return _buckets.TryGetValue(bucketName.ToLower(), out var bucketId)
            ? bucketId
            : throw new ArgumentException($"Unknown bucket: {bucketName}");
    }
}