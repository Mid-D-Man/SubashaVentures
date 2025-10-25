using System.Text;
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Image compression service implementation using native streams
/// For WASM/.NET 7, uses JS interop with Sharp.js for compression
/// Falls back to uncompressed if JS not available
/// </summary>
public class ImageCompressionService : IImageCompressionService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ImageCompressionService> _logger;
    
    // Maximum file sizes
    private const long MaxFileSizeBytes = 50L * 1024L * 1024L; // 50 MB
    
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
            
            // Convert stream to byte array then to base64
            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                imageStream.Position = 0;
                await imageStream.CopyToAsync(memoryStream);
                imageBytes = memoryStream.ToArray();
            }
            
            var base64 = Convert.ToBase64String(imageBytes);
            var mimeType = DetectImageFormat(imageBytes) ?? "image/jpeg";
            
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
                
                // Convert base64 back to stream
                if (!string.IsNullOrEmpty(result.Base64Data))
                {
                    var compressedBytes = Convert.FromBase64String(result.Base64Data);
                    result.CompressedStream = new MemoryStream(compressedBytes);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage(
                    "JS image compression not available, returning original",
                    LogLevel.Warning);
                
                return new ImageCompressionResult
                {
                    Success = true,
                    CompressedStream = new MemoryStream(imageBytes),
                    OriginalSize = originalSize,
                    CompressedSize = originalSize,
                    CompressionRatio = 0,
                    ContentType = mimeType
                };
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, "Error compressing image");
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
            MID_HelperFunctions.LogException(ex, "Error converting compressed image to base64");
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
                MID_HelperFunctions.DebugMessage(
                    "JS image dimension detection not available",
                    LogLevel.Warning);
                return null;
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, "Error getting image dimensions");
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
            MID_HelperFunctions.LogException(ex, "Error converting base64 to stream");
            throw;
        }
    }

    public ImageValidationResult ValidateImage(
        Stream imageStream,
        long maxSizeBytes = 50L * 1024L * 1024L)
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

            // Convert stream to byte array for format detection
            byte[] headerBytes;
            using (var memoryStream = new MemoryStream())
            {
                imageStream.Position = 0;
                imageStream.CopyTo(memoryStream);
                var allBytes = memoryStream.ToArray();
                headerBytes = allBytes.Take(8).ToArray();
                imageStream.Position = 0;
            }

            // Check format by reading header
            var format = DetectImageFormat(headerBytes);
            
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
            MID_HelperFunctions.LogException(ex, "Error validating image");
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

    private string? DetectImageFormat(byte[] buffer)
    {
        try
        {
            if (buffer.Length < 8)
                return null;

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