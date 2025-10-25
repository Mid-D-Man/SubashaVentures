// Services/Storage/IImageCompressionService.cs & Implementation
using System.Text;
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using System.Linq;
namespace SubashaVentures.Services.Storage;

// ===== IMPLEMENTATION =====

/// <summary>
/// Image compression service implementation using native streams
/// NOTE: For WASM/.NET 7 without external image libraries, consider:
/// 1. Using Hosted API for image processing
/// 2. Using Sharp.js via JS interop
/// 3. Using Client-side compression before upload
/// </summary>
public class ImageCompressionService : IImageCompressionService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ImageCompressionService> _logger;
    
    // Maximum file sizes (Supabase Storage limits)
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB per file
    private const long BucketStorageLimit = 100 * 1024 * 1024 * 1024; // 100 GB per bucket (varies by plan)
    
    // Supported formats
    private static readonly HashSet<string> SupportedFormats = new()
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    public ImageCompressionService(IJSRuntime jsRuntime, ILogger<ImageCompressionService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<ImageCompressionResult> CompressImageAsync(
        Stream imageStream,
        int quality = 80,
        int maxWidth = 2000,
        int maxHeight = 2000,
        bool convertToWebP = false)
    {
        try
        {
            var originalSize = imageStream.Length;
            
            // For WASM, we'll use JS interop for compression
            // This requires sharp.js library
            var base64 = StreamToBase64(imageStream);
            var mimeType = DetectImageFormat(imageStream) ?? "image/jpeg";
            
            try
            {
                var result = await _jsRuntime.InvokeAsync<ImageCompressionResult>(
                    "imageCompressor.compressImage",
                    base64,
                    quality,
                    maxWidth,
                    maxHeight,
                    convertToWebP ? "image/webp" : mimeType);
                
                result.OriginalSize = originalSize;
                result.CompressionRatio = originalSize > 0 
                    ? (float)(originalSize - result.CompressedSize) / originalSize 
                    : 0;
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("JS image compression not available, returning original");
                
                return new ImageCompressionResult
                {
                    Success = true,
                    CompressedStream = new MemoryStream(imageStream.ToArray()),
                    OriginalSize = originalSize,
                    CompressedSize = originalSize,
                    CompressionRatio = 0,
                    ContentType = mimeType
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compressing image");
            return new ImageCompressionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<string> CompressToBase64Async(
        Stream imageStream,
        int quality = 80,
        int maxWidth = 2000,
        int maxHeight = 2000)
    {
        try
        {
            var result = await CompressImageAsync(imageStream, quality, maxWidth, maxHeight);
            
            if (!result.Success || result.CompressedStream == null)
            {
                throw new InvalidOperationException("Image compression failed");
            }

            return StreamToBase64(result.CompressedStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting compressed image to base64");
            throw;
        }
    }

    public async Task<ImageCompressionResult> CreateThumbnailAsync(
        Stream imageStream,
        int size = 300,
        int quality = 85)
    {
        return await CompressImageAsync(imageStream, quality, size, size);
    }

    public async Task<ImageDimensions?> GetImageDimensionsAsync(Stream imageStream)
    {
        try
        {
            var base64 = StreamToBase64(imageStream);
            
            try
            {
                return await _jsRuntime.InvokeAsync<ImageDimensions>(
                    "imageCompressor.getImageDimensions",
                    base64);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("JS image dimension detection not available");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting image dimensions");
            return null;
        }
    }

    public Stream Base64ToStream(string base64String)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64String);
            return new MemoryStream(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting base64 to stream");
            throw;
        }
    }

    public ImageValidationResult ValidateImage(
        Stream imageStream,
        long maxSizeBytes = 50 * 1024 * 1024)
    {
        try
        {
            var fileSize = imageStream.Length;
            
            // Check file size
            if (fileSize > maxSizeBytes)
            {
                return new ImageValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File size exceeds limit. Max: {maxSizeBytes / 1024 / 1024}MB",
                    FileSize = fileSize
                };
            }

            // Check format by reading header
            var format = DetectImageFormat(imageStream);
            
            if (format == null || !SupportedFormats.Contains(format))
            {
                return new ImageValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Unsupported image format",
                    FileSize = fileSize
                };
            }

            return new ImageValidationResult
            {
                IsValid = true,
                Format = format,
                FileSize = fileSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating image");
            return new ImageValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private string StreamToBase64(Stream stream)
    {
        stream.Position = 0;
        using (var memoryStream = new MemoryStream())
        {
            stream.CopyTo(memoryStream);
            return Convert.ToBase64String(memoryStream.ToArray());
        }
    }

    private string? DetectImageFormat(Stream stream)
    {
        try
        {
            stream.Position = 0;
            var buffer = new byte[8];
            stream.Read(buffer, 0, 8);
            stream.Position = 0;

            // JPEG
            if (buffer[0] == 0xFF && buffer[1] == 0xD8)
                return "image/jpeg";

            // PNG
            if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                return "image/png";

            // GIF
            if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46)
                return "image/gif";

            // WebP
            if (buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46)
                return "image/webp";

            return null;
        }
        catch
        {
            return null;
        }
    }
}