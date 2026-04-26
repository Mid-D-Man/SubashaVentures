// Services/Storage/ICloudflareR2Service.cs
using Microsoft.AspNetCore.Components.Forms;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Service for interacting with Cloudflare R2 object storage
/// via the R2 proxy Cloudflare Worker.
///
/// ALL images are automatically compressed and converted to WebP before upload.
/// Key/folder structure:
///   partners/{partner_id}/store/logo.webp
///   partners/{partner_id}/store/banner.webp
///   partners/{partner_id}/templates/{template_id}/img_{n}_{token}.webp
///   users/{user_id}/avatar.webp
/// </summary>
public interface ICloudflareR2Service
{
    // ── Upload (all auto-convert to WebP) ─────────────────────

    /// <summary>
    /// Upload a browser file. Automatically compresses and converts to WebP.
    /// Returns the R2 object key (WebP path) and public URL on success.
    /// </summary>
    Task<R2UploadResult> UploadImageAsync(
        IBrowserFile file,
        string objectKey,
        long maxFileSizeBytes = 5_242_880);

    /// <summary>
    /// Upload raw bytes (already processed). Use when you have pre-compressed data.
    /// </summary>
    Task<R2UploadResult> UploadBytesAsync(
        byte[] bytes,
        string objectKey,
        string contentType = "image/webp");

    // ── Delete ─────────────────────────────────────────────────

    Task<bool> DeleteFileAsync(string objectKey);
    Task<bool> DeleteFolderAsync(string prefix);

    // ── Public URL ─────────────────────────────────────────────

    /// <summary>
    /// Build full public URL from an object key.
    /// Pure string operation — no network call.
    /// </summary>
    string GetPublicUrl(string objectKey);

    /// <summary>Inverse of GetPublicUrl.</summary>
    string? ExtractObjectKey(string publicUrl);

    // ── Key Builders ───────────────────────────────────────────

    /// <summary>partners/{partnerId}/store/logo.webp</summary>
    string BuildStoreLogoKey(string partnerId);

    /// <summary>partners/{partnerId}/store/banner.webp</summary>
    string BuildStoreBannerKey(string partnerId);

    /// <summary>partners/{partnerId}/templates/{templateId}/img_{n}_{token}.webp</summary>
    string BuildTemplateImageKey(string partnerId, string templateId, int imageIndex);

    /// <summary>users/{userId}/avatar.webp</summary>
    string BuildUserAvatarKey(string userId);

    // ── Validation ─────────────────────────────────────────────

    R2ValidationResult ValidateImageFile(IBrowserFile file, long maxBytes = 5_242_880);

    // ── List ───────────────────────────────────────────────────

    Task<List<R2ObjectInfo>> ListObjectsAsync(string prefix = "", int maxKeys = 1000);
}

// ── Result / Info types ────────────────────────────────────────

public class R2PresignedUrlResult
{
    public bool    Success          { get; set; }
    public string? PresignedUrl     { get; set; }
    public string? ObjectKey        { get; set; }
    public int     ExpiresInSeconds { get; set; } = 300;
    public string? ErrorMessage     { get; set; }
}

public class R2UploadResult
{
    public bool    Success        { get; set; }
    public string? ObjectKey      { get; set; }
    public string? PublicUrl      { get; set; }
    public long    FileSizeBytes  { get; set; }
    public string  ContentType    { get; set; } = "image/webp";
    public string? ErrorMessage   { get; set; }
}

public class R2ObjectInfo
{
    public string   Key          { get; set; } = string.Empty;
    public long     Size         { get; set; }
    public DateTime LastModified { get; set; }
    public string?  ContentType  { get; set; }
    public string   PublicUrl    { get; set; } = string.Empty;

    public string DisplaySize
    {
        get
        {
            if (Size < 1024)          return $"{Size} B";
            if (Size < 1_048_576)     return $"{Size / 1024.0:F1} KB";
            if (Size < 1_073_741_824) return $"{Size / 1_048_576.0:F1} MB";
            return $"{Size / 1_073_741_824.0:F2} GB";
        }
    }

    public string LastModifiedDisplay => LastModified.ToString("MMM dd, yyyy HH:mm");

    public string Folder
    {
        get
        {
            var i = Key.LastIndexOf('/');
            return i >= 0 ? Key[..i] : string.Empty;
        }
    }

    public string FileName
    {
        get
        {
            var i = Key.LastIndexOf('/');
            return i >= 0 ? Key[(i + 1)..] : Key;
        }
    }
}

public class R2ValidationResult
{
    public bool         IsValid { get; set; }
    public List<string> Errors  { get; set; } = new();

    public static R2ValidationResult Ok()                    => new() { IsValid = true };
    public static R2ValidationResult Fail(params string[] e) => new() { IsValid = false, Errors = e.ToList() };
}

public static class R2AllowedContentTypes
{
    public static readonly IReadOnlyList<string> Images = new[]
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    public static bool IsAllowedImage(string ct) =>
        Images.Contains(ct.ToLowerInvariant());

    public static string GetExtension(string ct) => ct.ToLowerInvariant() switch
    {
        "image/jpeg" => "jpg",
        "image/jpg"  => "jpg",
        "image/png"  => "png",
        "image/webp" => "webp",
        "image/gif"  => "gif",
        _            => "bin"
    };
}
