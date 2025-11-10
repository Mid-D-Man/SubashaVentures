// Services/Storage/ImageCompressionService.cs
using Microsoft.AspNetCore.Components.Forms;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Image compression service using native Blazor RequestImageFileAsync
/// This is Microsoft's recommended approach for Blazor WASM
/// </summary>
public class ImageCompressionService : IImageCompressionService
{
    private readonly ILogger<ImageCompressionService> _logger;
    
    private const long MaxFileSizeBytes = 50L * 1024L * 1024L;
    
    private static readonly HashSet<string> SupportedFormats = new()
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    public ImageCompressionService(ILogger<ImageCompressionService> logger)
    {
        _logger = logger;
    }

    public async Task<ImageCompressionResult> CompressImageAsync(
        IBrowserFile browserFile,
        int maxWidth = 2000,
        int maxHeight = 2000,
        int quality = 80,
        bool enableCompression = true)
    {
        try
        {
            var originalSize = browserFile.Size;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Starting compression for {browserFile.Name} ({originalSize / 1024}KB). Compression enabled: {enableCompression}",
                LogLevel.Info
            );

            if (!enableCompression)
            {
                // Skip compression - return original
                var originalStream = browserFile.OpenReadStream(MaxFileSizeBytes);
                
                return new ImageCompressionResult
                {
                    Success = true,
                    CompressedStream = originalStream,
                    OriginalSize = originalSize,
                    CompressedSize = originalSize,
                    CompressionRatio = 0,
                    ContentType = browserFile.ContentType,
                    ErrorMessage = "Compression disabled - using original file"
                };
            }

            // Use Blazor's built-in RequestImageFileAsync - this is the recommended way
            var resizedImageFile = await browserFile.RequestImageFileAsync(
                browserFile.ContentType == "image/png" ? "image/png" : "image/jpeg",
                maxWidth,
                maxHeight);

            var compressedSize = resizedImageFile.Size;
            var compressedStream = resizedImageFile.OpenReadStream(MaxFileSizeBytes);

            var result = new ImageCompressionResult
            {
                Success = true,
                CompressedStream = compressedStream,
                OriginalSize = originalSize,
                CompressedSize = compressedSize,
                CompressionRatio = originalSize > 0 
                    ? Math.Max(0, (float)(originalSize - compressedSize) / originalSize)
                    : 0,
                ContentType = resizedImageFile.ContentType,
                Width = maxWidth,
                Height = maxHeight
            };

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Compression successful: {originalSize / 1024}KB → {compressedSize / 1024}KB ({result.CompressionRatio * 100:F1}% reduction)",
                LogLevel.Info
            );

            return result;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Compressing image: {browserFile.Name}");
            
            // Fallback: return original file
            _logger.LogWarning("Compression failed, returning original file");
            
            try
            {
                var fallbackStream = browserFile.OpenReadStream(MaxFileSizeBytes);
                
                return new ImageCompressionResult
                {
                    Success = true,
                    CompressedStream = fallbackStream,
                    OriginalSize = browserFile.Size,
                    CompressedSize = browserFile.Size,
                    CompressionRatio = 0,
                    ContentType = browserFile.ContentType,
                    ErrorMessage = $"Compression failed, using original: {ex.Message}"
                };
            }
            catch (Exception fallbackEx)
            {
                await MID_HelperFunctions.LogExceptionAsync(fallbackEx, "Fallback also failed");
                
                return new ImageCompressionResult
                {
                    Success = false,
                    ErrorMessage = $"Complete failure: {fallbackEx.Message}"
                };
            }
        }
    }

    public async Task<ImageCompressionResult> CompressImageFromStreamAsync(
        Stream imageStream,
        string fileName,
        int maxWidth = 2000,
        int maxHeight = 2000,
        bool enableCompression = true)
    {
        try
        {
            var originalSize = imageStream.Length;
            
            if (!enableCompression)
            {
                return new ImageCompressionResult
                {
                    Success = true,
                    CompressedStream = imageStream,
                    OriginalSize = originalSize,
                    CompressedSize = originalSize,
                    CompressionRatio = 0,
                    ContentType = "image/jpeg",
                    ErrorMessage = "Compression disabled - using original stream"
                };
            }

            // Note: This method is less reliable than using IBrowserFile
            // It's provided as a fallback but may not work properly
            await MID_HelperFunctions.DebugMessageAsync(
                "WARNING: Using stream-based compression (less reliable). Consider using IBrowserFile instead.",
                LogLevel.Warning
            );

            return new ImageCompressionResult
            {
                Success = true,
                CompressedStream = imageStream,
                OriginalSize = originalSize,
                CompressedSize = originalSize,
                CompressionRatio = 0,
                ContentType = "image/jpeg",
                ErrorMessage = "Stream-based compression not fully implemented - using original"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Stream-based compression");
            
            return new ImageCompressionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ImageValidationResult> ValidateImageAsync(
        IBrowserFile browserFile,
        long maxSizeBytes = 50L * 1024L * 1024L)
    {
        try
        {
            var fileSize = browserFile.Size;
            
            if (fileSize > maxSizeBytes)
            {
                return new ImageValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File size {FormatBytes(fileSize)} exceeds limit of {FormatBytes(maxSizeBytes)}",
                    FileSize = fileSize
                };
            }

            var contentType = browserFile.ContentType.ToLower();
            
            if (!SupportedFormats.Contains(contentType))
            {
                return new ImageValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Unsupported format: {contentType}. Supported: JPG, PNG, WebP, GIF",
                    FileSize = fileSize,
                    Format = contentType
                };
            }

            return new ImageValidationResult
            {
                IsValid = true,
                Format = contentType,
                FileSize = fileSize
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Validating image");
            
            return new ImageValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ImageDimensions?> GetImageDimensionsAsync(IBrowserFile browserFile)
    {
        try
        {
            // Request image at original size to get dimensions
            var imageFile = await browserFile.RequestImageFileAsync(
                browserFile.ContentType,
                int.MaxValue,
                int.MaxValue);

            // Note: Blazor doesn't expose dimensions directly
            // This is a limitation of the current API
            await MID_HelperFunctions.DebugMessageAsync(
                "WARNING: Exact dimensions not available via Blazor API",
                LogLevel.Warning
            );

            return null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting image dimensions");
            return null;
        }
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
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