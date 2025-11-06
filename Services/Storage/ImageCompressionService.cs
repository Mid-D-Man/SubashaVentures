// Services/Storage/ImageCompressionService.cs  
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Image compression service for Blazor WASM
/// FIXED: No stream.Position calls on BrowserFileStream
/// </summary>
public class ImageCompressionService : IImageCompressionService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ImageCompressionService> _logger;
    
    private const long MaxFileSizeBytes = 50L * 1024L * 1024L;
    
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
            
            // FIXED: Read stream into memory first (don't use Position)
            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                await imageStream.CopyToAsync(memoryStream);
                imageBytes = memoryStream.ToArray();
            }
            
            var base64 = Convert.ToBase64String(imageBytes);
            
            try
            {
                // Use JS compression
                var result = await _jsRuntime.InvokeAsync<ImageCompressionResult>(
                    "imageCompressor.compressImage",
                    base64,
                    quality,
                    maxWidth,
                    maxHeight,
                    convertToWebP ? "image/webp" : "image/jpeg");
                
                if (result != null && result.Success)
                {
                    result.OriginalSize = originalSize;
                    result.CompressionRatio = originalSize > 0 
                        ? (float)(originalSize - result.CompressedSize) / originalSize 
                        : 0;
                    
                    if (!string.IsNullOrEmpty(result.Base64Data))
                    {
                        var compressedBytes = Convert.FromBase64String(result.Base64Data);
                        result.CompressedStream = new MemoryStream(compressedBytes);
                    }
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Image compressed: {originalSize / 1024}KB → {result.CompressedSize / 1024}KB ({result.CompressionRatio * 100:F1}%)",
                        LogLevel.Info
                    );
                    
                    return result;
                }
            }
            catch (Exception jsEx)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"JS compression not available: {jsEx.Message}",
                    LogLevel.Warning
                );
            }
            
            // Fallback: return original
            return new ImageCompressionResult
            {
                Success = true,
                CompressedStream = new MemoryStream(imageBytes),
                OriginalSize = originalSize,
                CompressedSize = originalSize,
                CompressionRatio = 0,
                ContentType = "image/jpeg"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Error compressing image");
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
        var result = await CompressImageAsync(imageStream, quality, maxWidth, maxHeight);
        
        if (!result.Success || result.CompressedStream == null)
        {
            throw new InvalidOperationException("Image compression failed");
        }

        return await StreamToBase64Async(result.CompressedStream);
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
            var base64 = await StreamToBase64Async(imageStream);
            
            return await _jsRuntime.InvokeAsync<ImageDimensions>(
                "imageCompressor.getImageDimensions",
                base64);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Error getting image dimensions");
            return null;
        }
    }

    public Stream Base64ToStream(string base64String)
    {
        var bytes = Convert.FromBase64String(base64String);
        return new MemoryStream(bytes);
    }

    public ImageValidationResult ValidateImage(
        Stream imageStream,
        long maxSizeBytes = 50L * 1024L * 1024L)
    {
        return ValidateImageAsync(imageStream, maxSizeBytes).GetAwaiter().GetResult();
    }

    public async Task<ImageValidationResult> ValidateImageAsync(
        Stream imageStream,
        long maxSizeBytes = 50L * 1024L * 1024L)
    {
        try
        {
            var fileSize = imageStream.Length;
            
            if (fileSize > maxSizeBytes)
            {
                return new ImageValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File size exceeds limit. Max: {maxSizeBytes / 1024 / 1024}MB",
                    FileSize = fileSize
                };
            }

            // FIXED: Read bytes without using Position
            byte[] headerBytes = new byte[8];
            await imageStream.ReadAsync(headerBytes, 0, 8);
            // Don't reset Position - BrowserFileStream doesn't support it!

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
            await MID_HelperFunctions.LogExceptionAsync(ex, "Error validating image");
            return new ImageValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // FIXED: StreamToBase64 without Position
    private async Task<string> StreamToBase64Async(Stream stream)
    {
        using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream);
            return Convert.ToBase64String(memoryStream.ToArray());
        }
    }

    private string? DetectImageFormat(byte[] buffer)
    {
        if (buffer.Length < 8) return null;

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
}
