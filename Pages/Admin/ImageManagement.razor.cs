// Pages/Admin/ImageManagement.razor.cs - COMPLETE WITH CACHING
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
using SubashaVentures.Domain.Enums;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class ImageManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private ISupabaseStorageService StorageService { get; set; } = default!;
    [Inject] private IImageCompressionService CompressionService { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
    [Inject] private IImageCacheService ImageCacheService { get; set; } = default!; // ✅ NEW
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<ImageManagement> Logger { get; set; } = default!;
    [Inject] private IFirestoreService FirestoreService { get; set; } = default!;

    private List<CategoryModel> categories = new();
    private NotificationComponent? notificationComponent;

    private MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>? _imageListPool;
    private MID_ComponentObjectPool<List<UploadQueueItem>>? _uploadQueuePool;

    private bool isLoading = true;
    private bool isUploadModalOpen = false;
    private bool isUploading = false;
    private bool isDragging = false;
    private bool enableCompression = true;
    
    private bool isConfirmationOpen = false;
    private bool isDeleting = false;
    private AdminImageCard.ImageItem? imageToDelete = null;
    private List<string> imagesToDelete = new();

    private string searchQuery = "";
    private string selectedFolder = "";
    private string uploadFolder = "products";
    private string gridSize = "medium";
    private string sortBy = "newest";

    private int currentPage = 1;
    private int pageSize = 24;
    private double storagePercentage = 0;
    private string usedStorage = "0 MB";
    private string totalStorage = "1 GB";
    private int totalImages = 0;
    private int totalFolders = 8;
    private int referencedImages = 0;

    private List<string> selectedImages = new();
    private List<AdminImageCard.ImageItem> allImages = new();
    private List<AdminImageCard.ImageItem> filteredImages = new();
    private List<AdminImageCard.ImageItem> paginatedImages = new();
    private List<UploadQueueItem> uploadQueue = new();

    private DynamicModal? uploadModal;
    private ConfirmationPopup? confirmationPopup;
    
    private int totalPages => (int)Math.Ceiling(filteredImages.Count / (double)pageSize);

    private const string CACHE_KEY = "image_selector_cache";
    private const int CACHE_DURATION_MINUTES = 5;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _imageListPool = new MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>(
                () => new List<AdminImageCard.ImageItem>(),
                list => list.Clear(),
                maxPoolSize: 10
            );

            _uploadQueuePool = new MID_ComponentObjectPool<List<UploadQueueItem>>(
                () => new List<UploadQueueItem>(),
                list => list.Clear(),
                maxPoolSize: 5
            );

            await MID_HelperFunctions.DebugMessageAsync("ImageManagement initialized", LogLevel.Info);

            await LoadCategoriesAsync();
            await LoadImagesAsync();
            await LoadStorageInfoAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ImageManagement initialization");
            ShowError("Failed to initialize image management");
            isLoading = false;
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var loadedCategories = await FirestoreService.GetCollectionAsync<CategoryModel>("categories");
            
            if (loadedCategories != null && loadedCategories.Any())
            {
                categories = loadedCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToList();
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Loaded {categories.Count} categories from Firebase",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No categories found in Firebase, using defaults",
                    LogLevel.Warning
                );
                
                categories = new List<CategoryModel>();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading categories from Firebase");
            categories = new List<CategoryModel>();
        }
    }

    private async Task LoadImagesAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            // ✅ Try loading from cache first
            var cachedData = await LoadFromCacheAsync();
            if (cachedData != null)
            {
                allImages = cachedData;
                ApplyFiltersAndSort();
                isLoading = false;
                StateHasChanged();
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Loaded {allImages.Count} images from cache",
                    LogLevel.Info
                );
                return;
            }

            using var pooledImages = _imageListPool?.GetPooled();
            var imageList = pooledImages?.Object ?? new List<AdminImageCard.ImageItem>();
            var allImageUrls = new List<string>(); // ✅ For preloading

            var folders = categories.Any() 
                ? categories.Select(c => c.Slug).ToArray()
                : new[] { "products", "banners", "categories" };

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading images from {folders.Length} folders",
                LogLevel.Info
            );

            foreach (var folder in folders)
            {
                await LoadImagesFromFolderAsync(folder, imageList, allImageUrls);
            }

            allImages = new List<AdminImageCard.ImageItem>(imageList);

            // ✅ Preload images into browser cache (non-blocking)
            if (allImageUrls.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Preloading {allImageUrls.Count} images into cache...",
                    LogLevel.Info
                );
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ImageCacheService.PreloadImagesAsync(allImageUrls);
                        
                        await MID_HelperFunctions.DebugMessageAsync(
                            "✓ Images preloaded successfully",
                            LogLevel.Info
                        );
                    }
                    catch (Exception ex)
                    {
                        await MID_HelperFunctions.LogExceptionAsync(ex, "Preloading images");
                    }
                });
            }

            await SaveToCacheAsync(allImages);
            ApplyFiltersAndSort();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {allImages.Count} images from {folders.Length} folders",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading images");
            Logger.LogError(ex, "Failed to load images for selector");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadImagesFromFolderAsync(
        string folder, 
        List<AdminImageCard.ImageItem> imageList,
        List<string> allImageUrls)
    {
        try
        {
            var files = await StorageService.ListFilesAsync(folder, "products");

            if (!files.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"No files found in folder: {folder}",
                    LogLevel.Debug
                );
                return;
            }

            foreach (var file in files)
            {
                var filePath = $"{folder}/{file.Name}";
                var publicUrl = StorageService.GetPublicUrl(filePath, "products");
                
                // ✅ Add to preload list
                allImageUrls.Add(publicUrl);
                
                var fileSize = await GetFileSizeAsync(publicUrl);
                var dimensions = await GetImageDimensionsAsync(publicUrl);
                
                var imageInfo = new AdminImageCard.ImageItem
                {
                    Id = file.Id,
                    FileName = file.Name,
                    PublicUrl = publicUrl,
                    ThumbnailUrl = publicUrl,
                    Folder = folder,
                    FileSize = fileSize,
                    Dimensions = dimensions,
                    UploadedAt = file.UpdatedAt,
                    IsReferenced = false,
                    ReferenceCount = 0
                };

                lock (imageList)
                {
                    imageList.Add(imageInfo);
                }
            }
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {files.Count} images from {folder}",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Loading from folder: {folder}");
        }
    }

    private async Task<long> GetFileSizeAsync(string imageUrl)
    {
        try
        {
            var size = await JSRuntime.InvokeAsync<long>("eval", $@"
                fetch('{imageUrl}', {{method: 'HEAD'}})
                    .then(response => {{
                        const length = response.headers.get('Content-Length');
                        return length ? parseInt(length) : 0;
                    }})
                    .catch(() => 0);
            ");
            return size;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<string> GetImageDimensionsAsync(string imageUrl)
    {
        try
        {
            var dimensions = await JSRuntime.InvokeAsync<string>("eval", $@"
                new Promise((resolve) => {{
                    const img = new Image();
                    img.onload = () => resolve(`${{img.width}}x${{img.height}}`);
                    img.onerror = () => resolve('Unknown');
                    img.src = '{imageUrl}';
                }});
            ");
            return dimensions ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    // ✅ CACHE METHODS
    private async Task<List<AdminImageCard.ImageItem>?> LoadFromCacheAsync()
    {
        try
        {
            var cacheJson = await LocalStorage.GetItemAsync<string>(CACHE_KEY);
            if (string.IsNullOrEmpty(cacheJson))
                return null;

            var cacheData = JsonHelper.Deserialize<ImageCacheData>(cacheJson);
            if (cacheData == null)
                return null;

            if ((DateTime.UtcNow - cacheData.CachedAt).TotalMinutes > CACHE_DURATION_MINUTES)
            {
                await LocalStorage.RemoveItemAsync(CACHE_KEY);
                return null;
            }

            return cacheData.Images;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading from cache");
            return null;
        }
    }

    private async Task SaveToCacheAsync(List<AdminImageCard.ImageItem> images)
    {
        try
        {
            var cacheData = new ImageCacheData
            {
                Images = images,
                CachedAt = DateTime.UtcNow
            };

            var cacheJson = JsonHelper.Serialize(cacheData);
            await LocalStorage.SetItemAsync(CACHE_KEY, cacheJson);

            await MID_HelperFunctions.DebugMessageAsync("✓ Images cached successfully", LogLevel.Debug);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Caching images");
        }
    }

    // ✅ FILTERING & SORTING
    private void ApplyFiltersAndSort()
    {
        filteredImages = allImages
            .Where(img =>
            {
                bool folderMatch = string.IsNullOrEmpty(selectedFolder) || 
                                 img.Folder.Equals(selectedFolder, StringComparison.OrdinalIgnoreCase);
                
                bool searchMatch = string.IsNullOrEmpty(searchQuery) ||
                                 img.FileName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                 img.Folder.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
                
                return folderMatch && searchMatch;
            })
            .OrderByDescending(x => x.UploadedAt)
            .ToList();

        UpdatePaginatedImages();
    }

    private async Task LoadStorageInfoAsync()
    {
        try
        {
            var totalSize = allImages.Sum(img => img.FileSize);
            var totalCapacity = 1L * 1024L * 1024L * 1024L;

            storagePercentage = totalCapacity > 0 
                ? Math.Min(100, (totalSize / (double)totalCapacity) * 100)
                : 0;

            usedStorage = FormatBytes(totalSize);
            totalStorage = "1 GB";

            await MID_HelperFunctions.DebugMessageAsync(
                $"Storage usage: {usedStorage} / {totalStorage} ({storagePercentage:F1}%)",
                LogLevel.Info
            );

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading storage info");
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

    private void HandleImageSelect(string id, ChangeEventArgs e)
    {
        if (e.Value is bool isChecked)
        {
            if (isChecked)
            {
                if (!selectedImages.Contains(id))
                    selectedImages.Add(id);
            }
            else
            {
                selectedImages.Remove(id);
            }
            StateHasChanged();
        }
    }

    private async Task HandleCopyUrl(AdminImageCard.ImageItem image)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", image.PublicUrl);
            await MID_HelperFunctions.DebugMessageAsync("URL copied to clipboard", LogLevel.Info);
            ShowSuccess("Image URL copied to clipboard");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Copying URL");
            ShowError("Failed to copy URL to clipboard");
        }
    }

    private async Task HandleDownload(AdminImageCard.ImageItem image)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("eval", $@"
                fetch('{image.PublicUrl}')
                    .then(response => response.blob())
                    .then(blob => {{
                        const url = window.URL.createObjectURL(blob);
                        const a = document.createElement('a');
                        a.href = url;
                        a.download = '{image.FileName}';
                        document.body.appendChild(a);
                        a.click();
                        window.URL.revokeObjectURL(url);
                        document.body.removeChild(a);
                    }});
            ");
            ShowSuccess($"Downloading {image.FileName}");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Downloading image");
            ShowError("Failed to download image");
        }
    }

    private async Task HandleDelete(AdminImageCard.ImageItem image)
    {
        try
        {
            if (image.IsReferenced)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Cannot delete: Image is used in products",
                    LogLevel.Warning
                );
                ShowWarning("Cannot delete: Image is used in products");
                return;
            }

            imageToDelete = image;
            imagesToDelete.Clear();
            isConfirmationOpen = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Preparing delete");
            ShowError("Failed to prepare delete operation");
        }
    }

    private async Task HandleBulkDownload()
    {
        try
        {
            var downloadCount = 0;
            foreach (var imageId in selectedImages)
            {
                var image = allImages.FirstOrDefault(x => x.Id == imageId);
                if (image != null)
                {
                    await HandleDownload(image);
                    downloadCount++;
                    await Task.Delay(500);
                }
            }
            selectedImages.Clear();
            ShowSuccess($"Downloaded {downloadCount} image(s)");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk download");
            ShowError("Failed to download some images");
        }
    }

    private async Task HandleBulkDelete()
    {
        try
        {
            var imagesToDeleteList = allImages
                .Where(x => selectedImages.Contains(x.Id) && !x.IsReferenced)
                .ToList();

            if (!imagesToDeleteList.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No images to delete (all selected images are referenced)",
                    LogLevel.Warning
                );
                ShowWarning("Cannot delete: All selected images are in use");
                return;
            }

            imageToDelete = null;
            imagesToDelete = imagesToDeleteList.Select(x => $"{x.Folder}/{x.FileName}").ToList();
            isConfirmationOpen = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Preparing bulk delete");
            ShowError("Failed to prepare bulk delete");
        }
    }

    private async Task ConfirmDelete()
    {
        try
        {
            isDeleting = true;
            StateHasChanged();

            bool success = false;

            if (imageToDelete != null)
            {
                var filePath = $"{imageToDelete.Folder}/{imageToDelete.FileName}";
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Deleting single image: {filePath}",
                    LogLevel.Info
                );

                success = await StorageService.DeleteImageAsync(filePath, "products");

                if (success)
                {
                    allImages.Remove(imageToDelete);
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ Image deleted successfully: {imageToDelete.FileName}",
                        LogLevel.Info
                    );
                    ShowSuccess($"Image '{imageToDelete.FileName}' deleted successfully");
                }
                else
                {
                    ShowError($"Failed to delete '{imageToDelete.FileName}'");
                }
            }
            else if (imagesToDelete.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Deleting {imagesToDelete.Count} images",
                    LogLevel.Info
                );

                success = await StorageService.DeleteImagesAsync(imagesToDelete, "products");

                if (success)
                {
                    var fileNames = imagesToDelete.Select(p => Path.GetFileName(p)).ToHashSet();
                    allImages.RemoveAll(x => fileNames.Contains(x.FileName));
                    
                    selectedImages.Clear();
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ {imagesToDelete.Count} images deleted successfully",
                        LogLevel.Info
                    );
                    ShowSuccess($"{imagesToDelete.Count} images deleted successfully");
                }
                else
                {
                    ShowError("Failed to delete some images");
                }
            }

            if (success)
            {
                await LoadStorageInfoAsync();
                ApplyFiltersAndSort();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Confirming delete");
            ShowError("An error occurred while deleting");
        }
        finally
        {
            isDeleting = false;
            isConfirmationOpen = false;
            imageToDelete = null;
            imagesToDelete.Clear();
            StateHasChanged();
        }
    }

    private void CancelDelete()
    {
        isConfirmationOpen = false;
        imageToDelete = null;
        imagesToDelete.Clear();
        StateHasChanged();
    }

    private async Task HandleFileSelect(InputFileChangeEventArgs e)
    {
        try
        {
            const int maxAllowedFiles = 10;
            var files = e.GetMultipleFiles(maxAllowedFiles);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Processing {files.Count} selected files",
                LogLevel.Info
            );

            var validFileCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var validation = await CompressionService.ValidateImageAsync(file);
                    
                    if (!validation.IsValid)
                    {
                        ShowWarning($"{file.Name}: {validation.ErrorMessage}");
                        continue;
                    }

                    var fileDataResult = await ReadFileDataAsync(file);
                    
                    if (fileDataResult == null)
                    {
                        ShowWarning($"{file.Name}: Failed to read file data");
                        continue;
                    }

                    var (fileBytes, previewUrl) = fileDataResult.Value;

                    uploadQueue.Add(new UploadQueueItem
                    {
                        FileName = file.Name,
                        PreviewUrl = previewUrl,
                        FileSize = file.Size,
                        Status = "pending",
                        FileData = fileBytes,
                        ContentType = file.ContentType
                    });
                    
                    validFileCount++;

                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ Queued: {file.Name} ({file.Size / 1024}KB)",
                        LogLevel.Debug
                    );
                }
                catch (Exception ex)
                {
                    await MID_HelperFunctions.LogExceptionAsync(ex, $"Processing file: {file.Name}");
                    ShowWarning($"{file.Name}: {ex.Message}");
                }
            }

            if (validFileCount > 0)
            {
                ShowInfo($"{validFileCount} file(s) added to upload queue");
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "File selection");
            ShowError("Failed to add files to upload queue");
        }
    }

    private async Task<(byte[] FileBytes, string PreviewUrl)?> ReadFileDataAsync(IBrowserFile file)
    {
        try
        {
            const long maxPreviewSize = 2L * 1024L * 1024L;
            const long maxFileSize = 50L * 1024L * 1024L;
            
            using var fileStream = file.OpenReadStream(maxFileSize);
            using var memoryStream = new MemoryStream();
            
            await fileStream.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            var previewBytes = fileBytes.Length > maxPreviewSize 
                ? fileBytes.Take((int)maxPreviewSize).ToArray()
                : fileBytes;
                
            var base64 = Convert.ToBase64String(previewBytes);
            var previewUrl = $"data:{file.ContentType};base64,{base64}";

            return (fileBytes, previewUrl);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Reading file data: {file.Name}");
            return null;
        }
    }

    private async Task StartUpload()
    {
        if (!uploadQueue.Any() || isUploading)
            return;

        try
        {
            isUploading = true;
            StateHasChanged();

            var successCount = 0;
            var failCount = 0;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Starting upload of {uploadQueue.Count} files to folder: {uploadFolder}",
                LogLevel.Info
            );

            foreach (var item in uploadQueue.Where(x => x.Status == "pending"))
            {
                try
                {
                    item.Status = "uploading";
                    item.Progress = 30;
                    StateHasChanged();

                    if (item.FileData != null)
                    {
                        using var fileStream = new MemoryStream(item.FileData);
                        
                        var result = await StorageService.UploadImageAsync(
                            fileStream,
                            item.FileName,
                            "products",
                            uploadFolder
                        );

                        if (result.Success)
                        {
                            item.Status = "success";
                            item.Progress = 100;
                            successCount++;
                            
                            await MID_HelperFunctions.DebugMessageAsync(
                                $"✓ Uploaded: {item.FileName}",
                                LogLevel.Info
                            );
                        }
                        else
                        {
                            item.Status = "error";
                            item.ErrorMessage = result.ErrorMessage ?? "Upload failed";
                            failCount++;
                            
                            await MID_HelperFunctions.DebugMessageAsync(
                                $"✗ Failed: {item.FileName} - {item.ErrorMessage}",
                                LogLevel.Warning
                            );
                        }
                    }
                    else
                    {
                        item.Status = "error";
                        item.ErrorMessage = "No file data available";
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    item.Status = "error";
                    item.ErrorMessage = ex.Message;
                    failCount++;
                    
                    await MID_HelperFunctions.LogExceptionAsync(ex, $"Uploading: {item.FileName}");
                }
                finally
                {
                    StateHasChanged();
                    await Task.Delay(100);
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Upload complete: {successCount} succeeded, {failCount} failed",
                LogLevel.Info
            );

            await LoadImagesAsync();
            await LoadStorageInfoAsync();

            uploadQueue.RemoveAll(x => x.Status == "success");

            if (successCount > 0 && failCount == 0)
            {
                ShowSuccess($"Successfully uploaded {successCount} image(s)");
            }
            else if (successCount > 0 && failCount > 0)
            {
                ShowWarning($"Uploaded {successCount} image(s), {failCount} failed");
            }
            else if (failCount > 0)
            {
                ShowError($"Failed to upload {failCount} image(s)");
            }

            if (!uploadQueue.Any())
            {
                CloseUploadModal();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Upload");
            ShowError("Upload operation failed");
        }
        finally
        {
            isUploading = false;
            StateHasChanged();
        }
    }

    private void RemoveFromQueue(UploadQueueItem item)
    {
        uploadQueue.Remove(item);
        StateHasChanged();
    }

    private void ClearQueue()
    {
        uploadQueue.Clear();
        StateHasChanged();
    }

    private void UpdatePaginatedImages()
    {
        paginatedImages = filteredImages
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        StateHasChanged();
    }

    private void PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            UpdatePaginatedImages();
        }
    }

    private void NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            UpdatePaginatedImages();
        }
    }

    private void GoToPage(int page)
    {
        if (page >= 1 && page <= totalPages)
        {
            currentPage = page;
            UpdatePaginatedImages();
        }
    }

    private void OpenUploadModal()
    {
        isUploadModalOpen = true;
        StateHasChanged();
    }

    private void CloseUploadModal()
    {
        isUploadModalOpen = false;
        uploadQueue.Clear();
        StateHasChanged();
    }

    private void HandleDragEnter()
    {
        isDragging = true;
        StateHasChanged();
    }

    private void HandleDragLeave()
    {
        isDragging = false;
        StateHasChanged();
    }

    private async Task HandleDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs e)
    {
        isDragging = false;
        StateHasChanged();
    }
    private void ShowSuccess(string message)
    {
        notificationComponent?.ShowNotification(message, NotificationType.Success);
    }
    private void ShowError(string message)
    {
        notificationComponent?.ShowNotification(message, NotificationType.Error);
    }
    private void ShowWarning(string message)
    {
        notificationComponent?.ShowNotification(message, NotificationType.Warning);
    }
    private void ShowInfo(string message)
    {
        notificationComponent?.ShowNotification(message, NotificationType.Info);
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            _imageListPool?.Dispose();
            _uploadQueuePool?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disposing");
        }
    }
    public class UploadQueueItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = "";
        public string PreviewUrl { get; set; } = "";
        public long FileSize { get; set; }
        public string Status { get; set; } = "pending";
        public int Progress { get; set; }
        public string? ErrorMessage { get; set; }
        public byte[]? FileData { get; set; }
        public string ContentType { get; set; } = "image/jpeg";
        public string FormattedSize
        {
            get
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = FileSize;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }
    }
    private class ImageCacheData
    {
        public List<AdminImageCard.ImageItem> Images { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }
}