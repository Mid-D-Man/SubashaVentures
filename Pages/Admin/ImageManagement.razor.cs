// Pages/Admin/ImageManagement.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Admin.Images;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Models.Firebase;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class ImageManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private ISupabaseStorageService      StorageService     { get; set; } = default!;
    [Inject] private IImageCompressionService     CompressionService { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage       { get; set; } = default!;
    [Inject] private IImageCacheService           ImageCacheService  { get; set; } = default!;
    [Inject] private IJSRuntime                   JSRuntime          { get; set; } = default!;
    [Inject] private ILogger<ImageManagement>     Logger             { get; set; } = default!;
    [Inject] private IFirestoreService            FirestoreService   { get; set; } = default!;

    // ─── UI refs ──────────────────────────────────────────────────────────────

    private List<CategoryModel>    categories             = new();
    private NotificationComponent? notificationComponent;
    private DynamicModal?          uploadModal;
    private DynamicModal?          convertModal;
    private DynamicModal?          moveModal;
    private ConfirmationPopup?     confirmationPopup;

    // ─── Object pools ─────────────────────────────────────────────────────────

    private MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>? _imageListPool;
    private MID_ComponentObjectPool<List<UploadQueueItem>>?          _uploadQueuePool;

    // ─── Loading / batch state ────────────────────────────────────────────────

    private bool isLoading         = true;
    private bool isLoadingMore     = false;
    private int  loadedFolderCount = 0;
    private int  totalFolderCount  = 0;
    private int  loadingProgress   =>
        totalFolderCount > 0
            ? (int)Math.Round(loadedFolderCount * 100.0 / totalFolderCount)
            : 0;

    private const int BATCH_SIZE = 2;

    // ─── Upload state ─────────────────────────────────────────────────────────

    private bool isUploadModalOpen = false;
    private bool isUploading       = false;
    private bool isDragging        = false;
    private bool enableCompression = true;

    // ─── Delete state ─────────────────────────────────────────────────────────

    private bool                       isConfirmationOpen = false;
    private bool                       isDeleting         = false;
    private AdminImageCard.ImageItem?  imageToDelete      = null;
    private List<string>               imagesToDelete     = new();

    // ─── Conversion state ─────────────────────────────────────────────────────

    private AdminImageCard.ImageItem? conversionImage;
    private bool   isConversionModalOpen = false;
    private string conversionFormat      = "image/webp";
    private int    conversionQuality     = 85;
    private bool   replaceOriginal       = false;
    private bool   isConverting          = false;

    // ─── Move state ───────────────────────────────────────────────────────────

    private AdminImageCard.ImageItem? moveImage;
    private bool   isMoveModalOpen   = false;
    private string moveTargetFolder  = "";
    private bool   isMoving          = false;

    // ─── Filter / sort / view ─────────────────────────────────────────────────

    private string searchQuery    = "";
    private string selectedFolder = "";
    private string uploadFolder   = "products";
    private string gridSize       = "medium";
    private string sortBy         = "newest";

    private bool HasActiveFilters =>
        !string.IsNullOrEmpty(searchQuery) || !string.IsNullOrEmpty(selectedFolder);

    // ─── Pagination ───────────────────────────────────────────────────────────

    private int currentPage = 1;
    private int pageSize    = 24;
    private int totalPages  => filteredImages.Count > 0
        ? (int)Math.Ceiling(filteredImages.Count / (double)pageSize)
        : 0;

    // ─── Stats ────────────────────────────────────────────────────────────────

    private double storagePercentage = 0;
    private string usedStorage       = "0 B";
    private string totalStorage      = "1 GB";
    private int    totalImages       = 0;
    private int    totalFolders      = 0;
    private int    referencedImages  = 0;

    // ─── Data ─────────────────────────────────────────────────────────────────

    private List<string>                   selectedImages  = new();
    private List<AdminImageCard.ImageItem> allImages       = new();
    private List<AdminImageCard.ImageItem> filteredImages  = new();
    private List<AdminImageCard.ImageItem> paginatedImages = new();
    private List<UploadQueueItem>          uploadQueue     = new();

    private const string CACHE_KEY              = "image_selector_cache";
    private const int    CACHE_DURATION_MINUTES = 5;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _imageListPool = new MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>(
                () => new List<AdminImageCard.ImageItem>(),
                l  => l.Clear(),
                maxPoolSize: 10);

            _uploadQueuePool = new MID_ComponentObjectPool<List<UploadQueueItem>>(
                () => new List<UploadQueueItem>(),
                l  => l.Clear(),
                maxPoolSize: 5);

            await LoadCategoriesAsync();
            await LoadImagesAsync();
            await LoadStorageInfoAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ImageManagement init");
            ShowError("Failed to initialise image management");
            isLoading = false;
            StateHasChanged();
        }
    }

    // ─── Category loading ─────────────────────────────────────────────────────

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var loaded = await FirestoreService.GetCollectionAsync<CategoryModel>("categories");
            categories = loaded?.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToList()
                         ?? new List<CategoryModel>();
            totalFolders = categories.Count > 0 ? categories.Count : 3;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "LoadCategories");
            categories = new List<CategoryModel>();
        }
    }

    // ─── Batch image loading ──────────────────────────────────────────────────

    private async Task LoadImagesAsync()
    {
        try
        {
            // Try cache first — instant render
            var cached = await LoadFromCacheAsync();
            if (cached != null)
            {
                allImages   = cached;
                totalImages = allImages.Count;
                ApplyFiltersAndSort();
                isLoading = false;
                StateHasChanged();
                await MID_HelperFunctions.DebugMessageAsync(
                    $"[ImageMgmt] {allImages.Count} images from cache", LogLevel.Info);
                return;
            }

            var folders = categories.Any()
                ? categories.Select(c => c.Slug).ToArray()
                : new[] { "products", "banners", "categories" };

            totalFolderCount  = folders.Length;
            loadedFolderCount = 0;
            isLoading         = true;
            allImages.Clear();
            StateHasChanged();

            var allImageUrls = new List<string>();

            for (int batchStart = 0; batchStart < folders.Length; batchStart += BATCH_SIZE)
            {
                var batch = folders.Skip(batchStart).Take(BATCH_SIZE).ToArray();

                if (batchStart > 0)
                {
                    isLoading     = false;
                    isLoadingMore = true;
                }

                var batchImages = new List<AdminImageCard.ImageItem>();
                var batchUrls   = new List<string>();

                var tasks = batch.Select(folder =>
                    LoadImagesFromFolderAsync(folder, batchImages, batchUrls)).ToArray();

                await Task.WhenAll(tasks);

                lock (allImages) { allImages.AddRange(batchImages); }
                allImageUrls.AddRange(batchUrls);
                loadedFolderCount += batch.Length;
                totalImages        = allImages.Count;

                ApplyFiltersAndSort();

                await MID_HelperFunctions.DebugMessageAsync(
                    $"[ImageMgmt] Batch done: {loadedFolderCount}/{totalFolderCount} folders, " +
                    $"{allImages.Count} total", LogLevel.Debug);
            }

            if (allImageUrls.Any())
            {
                _ = Task.Run(async () =>
                {
                    try { await ImageCacheService.PreloadImagesAsync(allImageUrls); }
                    catch (Exception ex) { await MID_HelperFunctions.LogExceptionAsync(ex, "Preload"); }
                });
            }

            await SaveToCacheAsync(allImages);
            await LoadStorageInfoAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"[ImageMgmt] ✓ {allImages.Count} images loaded", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "LoadImages");
            Logger.LogError(ex, "Failed to load images");
        }
        finally
        {
            isLoading     = false;
            isLoadingMore = false;
            StateHasChanged();
        }
    }

    private async Task LoadImagesFromFolderAsync(
        string folder,
        List<AdminImageCard.ImageItem> imageList,
        List<string> urlList)
    {
        try
        {
            var files = await StorageService.ListFilesAsync(folder, "products");
            if (!files.Any()) return;

            foreach (var file in files)
            {
                var filePath  = $"{folder}/{file.Name}";
                var publicUrl = StorageService.GetPublicUrl(filePath, "products");
                urlList.Add(publicUrl);

                var item = new AdminImageCard.ImageItem
                {
                    Id             = file.Id,
                    FileName       = file.Name,
                    PublicUrl      = publicUrl,
                    ThumbnailUrl   = publicUrl,
                    Folder         = folder,
                    FileSize       = file.Size,           // ← real size from metadata
                    Dimensions     = "",
                    UploadedAt     = file.UpdatedAt,
                    IsReferenced   = false,
                    ReferenceCount = 0
                };

                lock (imageList) { imageList.Add(item); }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"LoadFolder: {folder}");
        }
    }

    // ─── Storage stats ────────────────────────────────────────────────────────

    private async Task LoadStorageInfoAsync()
    {
        try
        {
            var totalSize     = allImages.Sum(img => img.FileSize);
            var totalCapacity = 1L * 1024L * 1024L * 1024L;

            storagePercentage = totalCapacity > 0
                ? Math.Min(100, totalSize / (double)totalCapacity * 100)
                : 0;

            usedStorage  = FormatBytes(totalSize);
            totalStorage = "1 GB";

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "LoadStorageInfo");
        }
    }

    // ─── Filtering / sorting / pagination ────────────────────────────────────

    private void ApplyFiltersAndSort()
    {
        IEnumerable<AdminImageCard.ImageItem> results = allImages;

        if (!string.IsNullOrEmpty(selectedFolder))
            results = results.Where(img =>
                img.Folder.Equals(selectedFolder, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(searchQuery))
            results = results.Where(img =>
                img.FileName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                img.Folder.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));

        results = sortBy switch
        {
            "oldest"        => results.OrderBy(x => x.UploadedAt),
            "name-az"       => results.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase),
            "name-za"       => results.OrderByDescending(x => x.FileName, StringComparer.OrdinalIgnoreCase),
            "size-largest"  => results.OrderByDescending(x => x.FileSize),
            "size-smallest" => results.OrderBy(x => x.FileSize),
            _               => results.OrderByDescending(x => x.UploadedAt)
        };

        filteredImages = results.ToList();
        currentPage    = 1;
        UpdatePaginatedImages();
    }

    private void UpdatePaginatedImages()
    {
        paginatedImages = filteredImages
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        StateHasChanged();
    }

    private void HandleSearchInput(ChangeEventArgs e)
    {
        searchQuery = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void ClearSearch()
    {
        searchQuery = "";
        ApplyFiltersAndSort();
    }

    private void ClearAllFilters()
    {
        searchQuery    = "";
        selectedFolder = "";
        sortBy         = "newest";
        ApplyFiltersAndSort();
    }

    private void HandleFolderFilterChange(ChangeEventArgs e)
    {
        selectedFolder = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "newest";
        ApplyFiltersAndSort();
    }

    private void SetGridSize(string size) { gridSize = size; StateHasChanged(); }

    // ─── Pagination ───────────────────────────────────────────────────────────

    private void PreviousPage()
    {
        if (currentPage > 1) { currentPage--; UpdatePaginatedImages(); }
    }

    private void NextPage()
    {
        if (currentPage < totalPages) { currentPage++; UpdatePaginatedImages(); }
    }

    private void GoToPage(int p)
    {
        if (p >= 1 && p <= totalPages) { currentPage = p; UpdatePaginatedImages(); }
    }

    private void HandlePageSizeChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var size))
        {
            pageSize    = size;
            currentPage = 1;
            UpdatePaginatedImages();
        }
    }

    // ─── Image selection ──────────────────────────────────────────────────────

    private void HandleImageSelect(string id, bool isChecked)
    {
        if (isChecked) { if (!selectedImages.Contains(id)) selectedImages.Add(id); }
        else           { selectedImages.Remove(id); }
        StateHasChanged();
    }

    // ─── Copy / Download / Delete ─────────────────────────────────────────────

    private async Task HandleCopyUrl(AdminImageCard.ImageItem image)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", image.PublicUrl);
            ShowSuccess("Image URL copied to clipboard");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "CopyUrl");
            ShowError("Failed to copy URL");
        }
    }

    private async Task HandleDownload(AdminImageCard.ImageItem image)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("eval", $@"
                fetch('{image.PublicUrl}')
                    .then(r => r.blob())
                    .then(b => {{
                        const url = URL.createObjectURL(b);
                        const a   = document.createElement('a');
                        a.href     = url;
                        a.download = '{image.FileName}';
                        document.body.appendChild(a);
                        a.click();
                        URL.revokeObjectURL(url);
                        document.body.removeChild(a);
                    }});
            ");
            ShowSuccess($"Downloading {image.FileName}");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Download");
            ShowError("Failed to download image");
        }
    }

    private async Task HandleDelete(AdminImageCard.ImageItem image)
    {
        if (image.IsReferenced) { ShowWarning("Cannot delete: image is used in products"); return; }
        imageToDelete      = image;
        imagesToDelete.Clear();
        isConfirmationOpen = true;
        StateHasChanged();
    }

    private async Task HandleBulkDownload()
    {
        int count = 0;
        foreach (var id in selectedImages)
        {
            var img = allImages.FirstOrDefault(x => x.Id == id);
            if (img != null) { await HandleDownload(img); count++; await Task.Delay(400); }
        }
        selectedImages.Clear();
        ShowSuccess($"Downloaded {count} image(s)");
    }

    private async Task HandleBulkDelete()
    {
        var toDelete = allImages
            .Where(x => selectedImages.Contains(x.Id) && !x.IsReferenced)
            .ToList();

        if (!toDelete.Any()) { ShowWarning("All selected images are in use and cannot be deleted"); return; }

        imageToDelete      = null;
        imagesToDelete     = toDelete.Select(x => $"{x.Folder}/{x.FileName}").ToList();
        isConfirmationOpen = true;
        StateHasChanged();
    }

    private async Task ConfirmDelete()
    {
        try
        {
            isDeleting = true;
            StateHasChanged();

            bool success;

            if (imageToDelete != null)
            {
                success = await StorageService.DeleteImageAsync(
                    $"{imageToDelete.Folder}/{imageToDelete.FileName}", "products");

                if (success)
                {
                    allImages.Remove(imageToDelete);
                    ShowSuccess($"'{imageToDelete.FileName}' deleted");
                }
                else ShowError($"Failed to delete '{imageToDelete.FileName}'");
            }
            else
            {
                success = await StorageService.DeleteImagesAsync(imagesToDelete, "products");

                if (success)
                {
                    var names = imagesToDelete.Select(Path.GetFileName).ToHashSet();
                    allImages.RemoveAll(x => names.Contains(x.FileName));
                    selectedImages.Clear();
                    ShowSuccess($"{imagesToDelete.Count} images deleted");
                }
                else ShowError("Failed to delete some images");
            }

            if (success)
            {
                totalImages = allImages.Count;
                await LoadStorageInfoAsync();
                ApplyFiltersAndSort();
                await InvalidateCacheAsync();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ConfirmDelete");
            ShowError("An error occurred while deleting");
        }
        finally
        {
            isDeleting         = false;
            isConfirmationOpen = false;
            imageToDelete      = null;
            imagesToDelete.Clear();
            StateHasChanged();
        }
    }

    private void CancelDelete()
    {
        isConfirmationOpen = false;
        imageToDelete      = null;
        imagesToDelete.Clear();
        StateHasChanged();
    }

    // ─── Conversion ───────────────────────────────────────────────────────────

    private Task HandleConvert(AdminImageCard.ImageItem image)
    {
        conversionImage      = image;
        conversionFormat     = "image/webp";
        conversionQuality    = 85;
        replaceOriginal      = false;
        isConversionModalOpen = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void CancelConvert()
    {
        isConversionModalOpen = false;
        conversionImage       = null;
        StateHasChanged();
    }

    private async Task ConfirmConvert()
    {
        if (conversionImage == null || isConverting) return;

        try
        {
            isConverting = true;
            StateHasChanged();

            // Call JS imageCompressor — fetches the public URL, canvas-encodes to target format
            var jsResult = await JSRuntime.InvokeAsync<JsConversionResult>(
                "imageCompressor.compressImageFromUrl",
                conversionImage.PublicUrl,
                conversionQuality,
                4096,
                4096,
                conversionFormat);

            if (!jsResult.Success || string.IsNullOrEmpty(jsResult.Base64Data))
            {
                ShowError($"Conversion failed: {jsResult.ErrorMessage ?? "Unknown JS error"}");
                return;
            }

            // Decode base64 → bytes
            byte[] bytes;
            try { bytes = Convert.FromBase64String(jsResult.Base64Data); }
            catch (Exception ex)
            {
                ShowError("Failed to decode converted image data");
                await MID_HelperFunctions.LogExceptionAsync(ex, "Base64Decode");
                return;
            }

            // Build a sensible new filename
            var baseName    = Path.GetFileNameWithoutExtension(conversionImage.FileName);
            var newExt      = GetFormatExtension(conversionFormat);
            var newFileName = $"{baseName}_converted{newExt}";
            var actualMime  = jsResult.Format ?? conversionFormat;

            // Upload to same folder
            var uploadResult = await StorageService.UploadImageBytesAsync(
                bytes,
                newFileName,
                actualMime,
                "products",
                conversionImage.Folder);

            if (!uploadResult.Success)
            {
                ShowError($"Upload failed: {uploadResult.ErrorMessage}");
                return;
            }

            // Optionally delete the original
            if (replaceOriginal)
            {
                var sourcePath = $"{conversionImage.Folder}/{conversionImage.FileName}";
                var deleted    = await StorageService.DeleteImageAsync(sourcePath, "products");

                if (deleted)
                {
                    allImages.Remove(conversionImage);
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"[ImageMgmt] Original deleted: {sourcePath}", LogLevel.Info);
                }
                else
                {
                    ShowWarning("Converted successfully, but original could not be deleted");
                }
            }

            // Refresh
            await InvalidateCacheAsync();
            await LoadImagesAsync();

            var savedPct = (int)(jsResult.CompressionRatio * 100);
            var label    = GetFormatLabel(conversionFormat);
            ShowSuccess($"Converted to {label} successfully — {savedPct}% size change");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ConfirmConvert");
            ShowError("Conversion failed unexpectedly");
        }
        finally
        {
            isConverting          = false;
            isConversionModalOpen = false;
            conversionImage       = null;
            StateHasChanged();
        }
    }

    // ─── Move ─────────────────────────────────────────────────────────────────

    private Task HandleMove(AdminImageCard.ImageItem image)
    {
        moveImage        = image;
        moveTargetFolder = image.Folder;
        isMoveModalOpen  = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void CancelMove()
    {
        isMoveModalOpen = false;
        moveImage       = null;
        StateHasChanged();
    }

    private async Task ConfirmMove()
    {
        if (moveImage == null || isMoving) return;

        if (moveTargetFolder == moveImage.Folder)
        {
            ShowWarning("Image is already in that category");
            isMoveModalOpen = false;
            return;
        }

        try
        {
            isMoving = true;
            StateHasChanged();

            var sourcePath = $"{moveImage.Folder}/{moveImage.FileName}";
            var destPath   = $"{moveTargetFolder}/{moveImage.FileName}";

            var success = await StorageService.MoveImageAsync(sourcePath, destPath, "products");

            if (success)
            {
                // Update local image list without full reload
                allImages.Remove(moveImage);
                moveImage.Folder       = moveTargetFolder;
                moveImage.PublicUrl    = StorageService.GetPublicUrl(destPath, "products");
                moveImage.ThumbnailUrl = moveImage.PublicUrl;
                allImages.Add(moveImage);

                await InvalidateCacheAsync();
                ApplyFiltersAndSort();
                await LoadStorageInfoAsync();

                ShowSuccess($"Image moved to {GetCategoryDisplayName(moveTargetFolder)}");
            }
            else
            {
                ShowError("Failed to move image — check storage permissions");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ConfirmMove");
            ShowError("Move operation failed unexpectedly");
        }
        finally
        {
            isMoving        = false;
            isMoveModalOpen = false;
            moveImage       = null;
            StateHasChanged();
        }
    }

    // ─── File select / upload ─────────────────────────────────────────────────

    private async Task HandleFileSelect(InputFileChangeEventArgs e)
    {
        const int maxAllowedFiles = 10;

        try
        {
            var files          = e.GetMultipleFiles(maxAllowedFiles);
            int validFileCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var validation = await CompressionService.ValidateImageAsync(file);
                    if (!validation.IsValid) { ShowWarning($"{file.Name}: {validation.ErrorMessage}"); continue; }

                    var previewUrl = await BuildPreviewUrlAsync(file);
                    if (previewUrl == null) { ShowWarning($"{file.Name}: failed to read preview"); continue; }

                    uploadQueue.Add(new UploadQueueItem
                    {
                        FileName    = file.Name,
                        PreviewUrl  = previewUrl,
                        FileSize    = file.Size,
                        Status      = "pending",
                        BrowserFile = file,
                        ContentType = file.ContentType
                    });

                    validFileCount++;
                }
                catch (Exception ex)
                {
                    await MID_HelperFunctions.LogExceptionAsync(ex, $"ProcessFile: {file.Name}");
                    ShowWarning($"{file.Name}: {ex.Message}");
                }
            }

            if (validFileCount > 0)
                ShowInfo($"{validFileCount} file(s) added to upload queue");

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "HandleFileSelect");
            ShowError("Failed to add files to upload queue");
        }
    }

    private async Task<string?> BuildPreviewUrlAsync(IBrowserFile file)
    {
        try
        {
            const long maxPreview = 2L * 1024L * 1024L;
            const long maxRead    = 50L * 1024L * 1024L;

            using var stream = file.OpenReadStream(maxRead);
            using var ms     = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var src   = bytes.Length > maxPreview ? bytes.Take((int)maxPreview).ToArray() : bytes;

            return $"data:{file.ContentType};base64,{Convert.ToBase64String(src)}";
        }
        catch { return null; }
    }

    private async Task StartUpload()
    {
        if (!uploadQueue.Any() || isUploading) return;

        try
        {
            isUploading = true;
            StateHasChanged();

            int successCount = 0, failCount = 0;

            foreach (var item in uploadQueue.Where(x => x.Status == "pending"))
            {
                try
                {
                    item.Status   = "uploading";
                    item.Progress = 20;
                    StateHasChanged();

                    StorageUploadResult result;

                    if (item.BrowserFile != null)
                    {
                        result = await StorageService.UploadImageAsync(
                            item.BrowserFile,
                            bucketName        : "products",
                            folder            : uploadFolder,
                            enableCompression : enableCompression);
                    }
                    else
                    {
                        if (item.FileData == null)
                        {
                            item.Status       = "error";
                            item.ErrorMessage = "No file data available";
                            failCount++;
                            continue;
                        }

                        using var ms = new MemoryStream(item.FileData);
                        result = await StorageService.UploadImageAsync(
                            ms, item.FileName, "products", uploadFolder);
                    }

                    item.Progress = 100;

                    if (result.Success)
                    {
                        item.Status = "success";
                        successCount++;
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"[ImageMgmt] ✓ Uploaded: {item.FileName} [{result.ContentType}]", LogLevel.Info);
                    }
                    else
                    {
                        item.Status       = "error";
                        item.ErrorMessage = result.ErrorMessage ?? "Upload failed";
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    item.Status       = "error";
                    item.ErrorMessage = ex.Message;
                    failCount++;
                    await MID_HelperFunctions.LogExceptionAsync(ex, $"Upload: {item.FileName}");
                }
                finally
                {
                    StateHasChanged();
                    await Task.Delay(80);
                }
            }

            await InvalidateCacheAsync();
            await LoadImagesAsync();

            uploadQueue.RemoveAll(x => x.Status == "success");

            if      (successCount > 0 && failCount == 0) ShowSuccess($"Uploaded {successCount} image(s)");
            else if (successCount > 0)                   ShowWarning($"Uploaded {successCount}, {failCount} failed");
            else                                         ShowError($"Failed to upload {failCount} image(s)");

            if (!uploadQueue.Any()) CloseUploadModal();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "StartUpload");
            ShowError("Upload operation failed");
        }
        finally
        {
            isUploading = false;
            StateHasChanged();
        }
    }

    private void RemoveFromQueue(UploadQueueItem item) { uploadQueue.Remove(item); StateHasChanged(); }
    private void ClearQueue()                          { uploadQueue.Clear();       StateHasChanged(); }

    // ─── Modal helpers ────────────────────────────────────────────────────────

    private void OpenUploadModal()  { isUploadModalOpen = true;  StateHasChanged(); }
    private void CloseUploadModal() { isUploadModalOpen = false; uploadQueue.Clear(); StateHasChanged(); }
    private void HandleDragEnter()  { isDragging = true;  StateHasChanged(); }
    private void HandleDragLeave()  { isDragging = false; StateHasChanged(); }
    private async Task HandleDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs _) { isDragging = false; StateHasChanged(); }

    // ─── Cache helpers ────────────────────────────────────────────────────────

    private async Task<List<AdminImageCard.ImageItem>?> LoadFromCacheAsync()
    {
        try
        {
            var json = await LocalStorage.GetItemAsync<string>(CACHE_KEY);
            if (string.IsNullOrEmpty(json)) return null;

            var data = JsonHelper.Deserialize<ImageCacheData>(json);
            if (data == null) return null;

            if ((DateTime.UtcNow - data.CachedAt).TotalMinutes > CACHE_DURATION_MINUTES)
            {
                await LocalStorage.RemoveItemAsync(CACHE_KEY);
                return null;
            }

            return data.Images;
        }
        catch { return null; }
    }

    private async Task SaveToCacheAsync(List<AdminImageCard.ImageItem> images)
    {
        try
        {
            var json = JsonHelper.Serialize(new ImageCacheData { Images = images, CachedAt = DateTime.UtcNow });
            await LocalStorage.SetItemAsync(CACHE_KEY, json);
        }
        catch (Exception ex) { await MID_HelperFunctions.LogExceptionAsync(ex, "SaveCache"); }
    }

    private async Task InvalidateCacheAsync()
    {
        try { await LocalStorage.RemoveItemAsync(CACHE_KEY); }
        catch { /* best-effort */ }
    }

    // ─── Notifications ────────────────────────────────────────────────────────

    private void ShowSuccess(string m) => notificationComponent?.ShowNotification(m, NotificationType.Success);
    private void ShowError(string m)   => notificationComponent?.ShowNotification(m, NotificationType.Error);
    private void ShowWarning(string m) => notificationComponent?.ShowNotification(m, NotificationType.Warning);
    private void ShowInfo(string m)    => notificationComponent?.ShowNotification(m, NotificationType.Info);

    // ─── Utility helpers ──────────────────────────────────────────────────────

    private string GetCategoryDisplayName(string slug) =>
        categories.FirstOrDefault(c => c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))?.Name ?? slug;

    private static string GetFormatLabel(string mime) => mime switch
    {
        "image/webp" => "WebP",
        "image/jpeg" => "JPEG",
        "image/png"  => "PNG",
        _            => "WebP"
    };

    private static string GetFormatExtension(string mime) => mime switch
    {
        "image/webp" => ".webp",
        "image/jpeg" => ".jpg",
        "image/png"  => ".png",
        _            => ".webp"
    };

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes; int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
        return $"{len:0.##} {sizes[order]}";
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        try
        {
            _imageListPool?.Dispose();
            _uploadQueuePool?.Dispose();
        }
        catch (Exception ex) { Logger.LogError(ex, "Dispose error"); }
    }

    // ─── Inner types ──────────────────────────────────────────────────────────

    /// <summary>
    /// Matches the JS object returned by imageCompressor.compressImageFromUrl().
    /// System.Text.Json (used by Blazor JS interop) deserialises camelCase automatically.
    /// </summary>
    private sealed class JsConversionResult
    {
        public bool    Success          { get; set; }
        public string? Base64Data       { get; set; }
        public long    CompressedSize   { get; set; }
        public long    OriginalSize     { get; set; }
        public float   CompressionRatio { get; set; }
        public string? Format           { get; set; }
        public string? ErrorMessage     { get; set; }
    }

    public class UploadQueueItem
    {
        public string        Id           { get; set; } = Guid.NewGuid().ToString();
        public string        FileName     { get; set; } = "";
        public string        PreviewUrl   { get; set; } = "";
        public long          FileSize     { get; set; }
        public string        Status       { get; set; } = "pending";
        public int           Progress     { get; set; }
        public string?       ErrorMessage { get; set; }
        public IBrowserFile? BrowserFile  { get; set; }
        public byte[]?       FileData     { get; set; }
        public string        ContentType  { get; set; } = "image/jpeg";

        public string FormattedSize
        {
            get
            {
                if (FileSize <= 0) return "—";
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = FileSize; int order = 0;
                while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
                return $"{len:0.##} {sizes[order]}";
            }
        }
    }

    private class ImageCacheData
    {
        public List<AdminImageCard.ImageItem> Images   { get; set; } = new();
        public DateTime                       CachedAt { get; set; }
    }
}
