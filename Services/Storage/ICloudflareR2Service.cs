using Microsoft.AspNetCore.Components.Forms;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Service for interacting with Cloudflare R2 object storage
/// via the R2 proxy Cloudflare Worker.
///
/// R2 has no native RLS. All access control is enforced by the
/// proxy Worker which validates the Supabase JWT before allowing
/// reads or writes. Uploads are done via presigned URLs generated
/// by the Worker — the browser uploads directly to R2, the Worker
/// never sees the file bytes, keeping Worker CPU costs minimal.
///
/// Bucket structure:
///   partners/{partner_id}/store/logo.{ext}
///   partners/{partner_id}/store/banner.{ext}
///   partners/{partner_id}/templates/{template_id}/{filename}
///   users/{user_id}/avatar.{ext}               (future migration)
/// </summary>
public interface ICloudflareR2Service
{
    // ── Presigned Upload ───────────────────────────────────────

    /// <summary>
    /// Request a presigned upload URL from the R2 proxy Worker.
    /// The caller uploads directly to R2 using the returned URL.
    /// Returns the object key (path) on success, null on failure.
    /// </summary>
    Task<R2PresignedUrlResult?> GetPresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        long maxFileSizeBytes = 5_242_880); // 5 MB default

    // ── Direct Upload (small files only, < 5 MB) ──────────────

    /// <summary>
    /// Upload a browser file directly via the proxy Worker.
    /// Use for files under 5 MB. For larger files use presigned URLs.
    /// Returns the public object key on success.
    /// </summary>
    Task<R2UploadResult> UploadFileAsync(
        IBrowserFile file,
        string objectKey,
        string contentType,
        long maxFileSizeBytes = 5_242_880);

    /// <summary>
    /// Upload raw bytes via the proxy Worker.
    /// </summary>
    Task<R2UploadResult> UploadBytesAsync(
        byte[] bytes,
        string objectKey,
        string contentType);

    // ── Delete ─────────────────────────────────────────────────

    /// <summary>
    /// Delete a single object from R2.
    /// </summary>
    Task<bool> DeleteFileAsync(string objectKey);

    /// <summary>
    /// Delete all objects under a given prefix.
    /// Use with caution — e.g. deleting a partner's template folder.
    /// </summary>
    Task<bool> DeleteFolderAsync(string prefix);

    // ── Public URL ─────────────────────────────────────────────

    /// <summary>
    /// Build the full public CDN URL for an R2 object key.
    /// Format: https://r2-proxy.mysubasha.com/{objectKey}
    /// Does NOT make a network call — purely string construction.
    /// </summary>
    string GetPublicUrl(string objectKey);

    /// <summary>
    /// Extract the object key from a full R2 public URL.
    /// Inverse of GetPublicUrl.
    /// </summary>
    string? ExtractObjectKey(string publicUrl);

    // ── Object Key Builders ────────────────────────────────────

    /// <summary>
    /// Build the object key for a partner store logo.
    /// Pattern: partners/{partnerId}/store/logo.{ext}
    /// </summary>
    string BuildStoreLogoKey(string partnerId, string fileExtension);

    /// <summary>
    /// Build the object key for a partner store banner.
    /// Pattern: partners/{partnerId}/store/banner.{ext}
    /// </summary>
    string BuildStoreBannerKey(string partnerId, string fileExtension);

    /// <summary>
    /// Build the object key for a template image.
    /// Pattern: partners/{partnerId}/templates/{templateId}/{fileName}
    /// </summary>
    string BuildTemplateImageKey(string partnerId, string templateId, string fileName);

    /// <summary>
    /// Build the object key for a user avatar.
    /// Pattern: users/{userId}/avatar.{ext}
    /// </summary>
    string BuildUserAvatarKey(string userId, string fileExtension);

    // ── Validation ─────────────────────────────────────────────

    /// <summary>
    /// Validate that a file's content type and size are acceptable
    /// before attempting an upload.
    /// </summary>
    R2ValidationResult ValidateImageFile(IBrowserFile file, long maxBytes = 5_242_880);

    // ── List ───────────────────────────────────────────────────

    /// <summary>
    /// List all object keys under a prefix.
    /// Used by the admin R2 browser panel.
    /// </summary>
    Task<List<R2ObjectInfo>> ListObjectsAsync(string prefix = "", int maxKeys = 1000);
}

// ── Result / Info models ───────────────────────────────────────

/// <summary>
/// Result of requesting a presigned upload URL.
/// </summary>
public class R2PresignedUrlResult
{
    public bool Success { get; set; }
    public string? PresignedUrl { get; set; }
    public string? ObjectKey { get; set; }

    /// <summary>Presigned URL expiry in seconds from now.</summary>
    public int ExpiresInSeconds { get; set; } = 300;

    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a direct file upload to R2.
/// </summary>
public class R2UploadResult
{
    public bool Success { get; set; }

    /// <summary>
    /// The R2 object key (path) if upload succeeded.
    /// Store this in the database — not the full URL.
    /// </summary>
    public string? ObjectKey { get; set; }

    /// <summary>
    /// Full public CDN URL. Convenience field — derived from ObjectKey.
    /// </summary>
    public string? PublicUrl { get; set; }

    public long FileSizeBytes { get; set; }
    public string? ContentType { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Metadata for a single R2 object returned by list operations.
/// </summary>
public class R2ObjectInfo
{
    public string Key { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string? ContentType { get; set; }
    public string PublicUrl { get; set; } = string.Empty;

    public string DisplaySize
    {
        get
        {
            if (Size < 1024)        return $"{Size} B";
            if (Size < 1_048_576)   return $"{Size / 1024.0:F1} KB";
            if (Size < 1_073_741_824) return $"{Size / 1_048_576.0:F1} MB";
            return $"{Size / 1_073_741_824.0:F2} GB";
        }
    }

    public string LastModifiedDisplay =>
        LastModified.ToString("MMM dd, yyyy HH:mm");

    /// <summary>
    /// Infer the folder/prefix from the key.
    /// e.g. "partners/abc/store/logo.png" → "partners/abc/store"
    /// </summary>
    public string Folder
    {
        get
        {
            var lastSlash = Key.LastIndexOf('/');
            return lastSlash >= 0 ? Key[..lastSlash] : string.Empty;
        }
    }

    /// <summary>
    /// File name portion of the key.
    /// e.g. "partners/abc/store/logo.png" → "logo.png"
    /// </summary>
    public string FileName
    {
        get
        {
            var lastSlash = Key.LastIndexOf('/');
            return lastSlash >= 0 ? Key[(lastSlash + 1)..] : Key;
        }
    }
}

/// <summary>
/// Result of pre-upload file validation.
/// </summary>
public class R2ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static R2ValidationResult Ok() =>
        new() { IsValid = true };

    public static R2ValidationResult Fail(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}

/// <summary>
/// Allowed image MIME types for R2 uploads.
/// </summary>
public static class R2AllowedContentTypes
{
    public static readonly IReadOnlyList<string> Images = new[]
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    public static bool IsAllowedImage(string contentType) =>
        Images.Contains(contentType.ToLowerInvariant());

    public static string GetExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/jpg"  => "jpg",
            "image/png"  => "png",
            "image/webp" => "webp",
            "image/gif"  => "gif",
            _            => "bin"
        };
}
