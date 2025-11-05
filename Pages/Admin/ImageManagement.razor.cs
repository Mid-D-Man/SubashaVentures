// Pages/Admin/ImageManagement.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Components.Shared.Modals;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Microsoft.AspNetCore.Components.Web;
namespace SubashaVentures.Pages.Admin;

public partial class ImageManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private ISupabaseStorageService StorageService { get; set; } = default!;
    [Inject] private IImageCompressionService CompressionService { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<ImageManagement> Logger { get; set; } = default!;

    // Object pools
    private MID_ComponentObjectPool<List<ImageItem>>? _imageListPool;
    private MID_ComponentObjectPool<List<UploadQueueItem>>? _uploadQueuePool;

    // State
    private bool isLoading = true;
    private bool isUploadModalOpen = false;
    private bool isDetailModalOpen = false;
    private bool isUploading = false;

    private string searchQuery = "";
    private string selectedFolder = "";
    private string uploadFolder = "products";
    private string gridSize = "medium";
    private string sortBy = "newest";

    private int currentPage = 1;
    private int pageSize = 24;
    private double storagePercentage = 0;
    private string usedStorage = "0 MB";
    private string totalStorage = "1 GB"; // FIXED: Supabase free tier is 1GB
    private int totalImages = 0;
    private int totalFolders = 8;
    private int referencedImages = 0;

    private List<string> selectedImages = new();
    private List<ImageItem> allImages = new();
    private List<ImageItem> filteredImages = new();
    private List<ImageItem> paginatedImages = new();
    private List<UploadQueueItem> uploadQueue = new();
    private ImageItem? selectedImage = null;

    private DynamicModal? uploadModal;
    private DynamicModal? detailModal;

    private int totalPages => (int)Math.Ceiling(filteredImages.Count / (double)pageSize);

private bool isDragging = false;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _imageListPool = new MID_ComponentObjectPool<List<ImageItem>>(
                () => new List<ImageItem>(),
                list => list.Clear(),
                maxPoolSize: 10
            );

            _uploadQueuePool = new MID_ComponentObjectPool<List<UploadQueueItem>>(
                () => new List<UploadQueueItem>(),
                list => list.Clear(),
                maxPoolSize: 5
            );

            await MID_HelperFunctions.DebugMessageAsync("ImageManagement initialized", LogLevel.Info);

            await LoadImagesAsync();
            await LoadStorageInfoAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ImageManagement initialization");
            isLoading = false;
        }
    }

    private async Task LoadImagesAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var loadTasks = new List<Task<List<StorageFile>>>
            {
                StorageService.ListFilesAsync("products"),
                StorageService.ListFilesAsync("products/mens"),
                StorageService.ListFilesAsync("products/womens"),
                StorageService.ListFilesAsync("products/children"),
                StorageService.ListFilesAsync("products/baby"),
                StorageService.ListFilesAsync("products/home"),
                StorageService.ListFilesAsync("banners"),
                StorageService.ListFilesAsync("categories")
            };

            var results = await Task.WhenAll(loadTasks);

            using var pooledImages = _imageListPool?.GetPooled();
            var imageList = pooledImages?.Object ?? new List<ImageItem>();

            foreach (var fileList in results)
            {
                foreach (var file in fileList)
                {
                    var imageItem = new ImageItem
                    {
                        Id = file.Id,
                        FileName = file.Name,
                        PublicUrl = StorageService.GetPublicUrl(file.Name),
                        ThumbnailUrl = StorageService.GetPublicUrl(file.Name),
                        Folder = ExtractFolderFromPath(file.Name),
                        FileSize = file.Size,
                        Dimensions = "800x800",
                        UploadedAt = file.UpdatedAt,
                        IsReferenced = false,
                        ReferenceCount = 0
                    };

                    imageList.Add(imageItem);
                }
            }

            allImages = new List<ImageItem>(imageList);
            totalImages = allImages.Count;
            referencedImages = allImages.Count(img => img.IsReferenced);

            ApplyFiltersAndSort();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {totalImages} images successfully", 
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading images");
            Logger.LogError(ex, "Failed to load images");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadStorageInfoAsync()
    {
        try
        {
            var capacityInfo = await StorageService.GetStorageCapacityAsync("products");

            storagePercentage = capacityInfo.UsagePercentage;
            usedStorage = capacityInfo.FormattedUsedCapacity;
            totalStorage = "1 GB"; // FIXED: Supabase free tier

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

    private void ApplyFiltersAndSort()
    {
        filteredImages = allImages
            .Where(img =>
                (string.IsNullOrEmpty(selectedFolder) || img.Folder == selectedFolder) &&
                (string.IsNullOrEmpty(searchQuery) ||
                 img.FileName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        filteredImages = sortBy switch
        {
            "newest" => filteredImages.OrderByDescending(x => x.UploadedAt).ToList(),
            "oldest" => filteredImages.OrderBy(x => x.UploadedAt).ToList(),
            "name-az" => filteredImages.OrderBy(x => x.FileName).ToList(),
            "name-za" => filteredImages.OrderByDescending(x => x.FileName).ToList(),
            "size-largest" => filteredImages.OrderByDescending(x => x.FileSize).ToList(),
            "size-smallest" => filteredImages.OrderBy(x => x.FileSize).ToList(),
            _ => filteredImages
        };

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

    private void HandleFolderChange(ChangeEventArgs e)
    {
        selectedFolder = e.Value?.ToString() ?? "";
        currentPage = 1;
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

    private async Task HandleCopyUrl(ImageItem image)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", image.PublicUrl);
            await MID_HelperFunctions.DebugMessageAsync("URL copied to clipboard", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Copying URL");
        }
    }

    private async Task HandleDownload(ImageItem image)
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

            await MID_HelperFunctions.DebugMessageAsync($"Downloading {image.FileName}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Downloading image");
        }
    }

    private async Task HandleDelete(ImageItem image)
    {
        try
        {
            if (image.IsReferenced)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Cannot delete: Image is used in products",
                    LogLevel.Warning
                );
                return;
            }

            var success = await StorageService.DeleteImageAsync(image.PublicUrl);

            if (success)
            {
                allImages.Remove(image);
                ApplyFiltersAndSort();
                await MID_HelperFunctions.DebugMessageAsync($"Deleted {image.FileName}", LogLevel.Info);
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting image");
        }
    }

    private async Task HandleBulkDownload()
    {
        foreach (var imageId in selectedImages)
        {
            var image = allImages.FirstOrDefault(x => x.Id == imageId);
            if (image != null)
            {
                await HandleDownload(image);
            }
        }
        selectedImages.Clear();
    }

    private async Task HandleBulkDelete()
    {
        try
        {
            var imagesToDelete = allImages
                .Where(x => selectedImages.Contains(x.Id) && !x.IsReferenced)
                .ToList();

            if (!imagesToDelete.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No images to delete (some may be referenced)",
                    LogLevel.Warning
                );
                return;
            }

            var filePaths = imagesToDelete.Select(x => x.PublicUrl).ToList();
            var success = await StorageService.DeleteImagesAsync(filePaths);

            if (success)
            {
                allImages.RemoveAll(x => imagesToDelete.Contains(x));
                selectedImages.Clear();
                ApplyFiltersAndSort();
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Deleted {imagesToDelete.Count} images",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk delete");
        }
    }

    // FIXED: Proper InputFile handling
    private async Task HandleFileSelect(InputFileChangeEventArgs e)
    {
        try
        {
            const long maxFileSize = 50L * 1024L * 1024L;
            const int maxAllowedFiles = 10;

            var files = e.GetMultipleFiles(maxAllowedFiles);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Selected {files.Count} file(s)", 
                LogLevel.Info
            );

            foreach (var file in files)
            {
                if (file.Size > maxFileSize)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"File {file.Name} exceeds size limit",
                        LogLevel.Warning
                    );
                    continue;
                }

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"File {file.Name} has unsupported type: {file.ContentType}",
                        LogLevel.Warning
                    );
                    continue;
                }

                // FIXED: Don't set Position, read directly into buffer
                var buffer = new byte[Math.Min(file.Size, 512 * 1024)];
                using var stream = file.OpenReadStream(maxFileSize);
                await stream.ReadAsync(buffer, 0, buffer.Length);
                
                var base64 = Convert.ToBase64String(buffer);
                var previewUrl = $"data:{file.ContentType};base64,{base64}";

                var queueItem = new UploadQueueItem
                {
                    FileName = file.Name,
                    PreviewUrl = previewUrl,
                    FileSize = file.Size,
                    Status = "pending",
                    Progress = 0,
                    BrowserFile = file
                };

                uploadQueue.Add(queueItem);
            }

            StateHasChanged();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Added {uploadQueue.Count} file(s) to upload queue",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Handling file selection");
            Logger.LogError(ex, "Error selecting files");
        }
    }

    private void RemoveFromQueue(UploadQueueItem item)
    {
        try
        {
            uploadQueue.Remove(item);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, "Removing item from queue");
        }
    }

    private void ClearQueue()
    {
        try
        {
            uploadQueue.Clear();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, "Clearing upload queue");
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

            foreach (var item in uploadQueue.Where(x => x.Status == "pending"))
            {
                try
                {
                    item.Status = "uploading";
                    item.Progress = 0;
                    StateHasChanged();

                    for (int i = 0; i <= 90; i += 10)
                    {
                        item.Progress = i;
                        StateHasChanged();
                        await Task.Delay(100);
                    }

                    if (item.BrowserFile != null)
                    {
                        // FIXED: Open stream without setting Position
                        using var fileStream = item.BrowserFile.OpenReadStream(50L * 1024L * 1024L);
                        
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
                            
                            await MID_HelperFunctions.DebugMessageAsync(
                                $"Uploaded {item.FileName} successfully", 
                                LogLevel.Info
                            );
                        }
                        else
                        {
                            item.Status = "error";
                            item.ErrorMessage = result.ErrorMessage ?? "Upload failed";
                            
                            await MID_HelperFunctions.DebugMessageAsync(
                                $"Failed to upload {item.FileName}: {item.ErrorMessage}",
                                LogLevel.Error
                            );
                        }
                    }
                    else
                    {
                        item.Status = "error";
                        item.ErrorMessage = "No file available";
                    }
                }
                catch (Exception ex)
                {
                    item.Status = "error";
                    item.ErrorMessage = ex.Message;
                    await MID_HelperFunctions.LogExceptionAsync(ex, $"Uploading {item.FileName}");
                }
                finally
                {
                    StateHasChanged();
                }
            }

            await LoadImagesAsync();
            await LoadStorageInfoAsync();

            uploadQueue.RemoveAll(x => x.Status == "success");

            if (!uploadQueue.Any())
            {
                CloseUploadModal();
            }

            await MID_HelperFunctions.DebugMessageAsync("Upload completed", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Starting upload");
        }
        finally
        {
            isUploading = false;
            StateHasChanged();
        }
    }

    private string ExtractFolderFromPath(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash > 0 ? path.Substring(0, lastSlash) : "products";
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

    private void OpenDetailModal()
    {
        isDetailModalOpen = true;
        StateHasChanged();
    }

    private void CloseDetailModal()
    {
        isDetailModalOpen = false;
        selectedImage = null;
        StateHasChanged();
    }

    private void HandleImageClick(ImageItem image)
    {
        selectedImage = image;
        OpenDetailModal();
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

private async Task HandleDrop(DragEventArgs e)
{
    try
    {
        isDragging = false;
        StateHasChanged();
        
        // The files will be handled by the InputFile component automatically
        // This method just prevents default behavior and updates UI state
        
        await MID_HelperFunctions.DebugMessageAsync(
            "Files dropped",
            LogLevel.Info
        );
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Handling drop");
    }
}

    public async ValueTask DisposeAsync()
    {
        try
        {
            _imageListPool?.Dispose();
            _uploadQueuePool?.Dispose();

            await MID_HelperFunctions.DebugMessageAsync(
                "ImageManagement disposed successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disposing ImageManagement");
        }
    }

    // Data models
    public class ImageItem
    {
        public string Id { get; set; } = "";
        public string FileName { get; set; } = "";
        public string PublicUrl { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string Folder { get; set; } = "";
        public long FileSize { get; set; }
        public string Dimensions { get; set; } = "";
        public DateTime UploadedAt { get; set; }
        public bool IsReferenced { get; set; }
        public int ReferenceCount { get; set; }

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

    public class UploadQueueItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = "";
        public string PreviewUrl { get; set; } = "";
        public long FileSize { get; set; }
        public string Status { get; set; } = "pending";
        public int Progress { get; set; }
        public string? ErrorMessage { get; set; }
        public IBrowserFile? BrowserFile { get; set; } // FIXED: Store IBrowserFile instead of Stream

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
}
