// Pages/Admin/ImageManagement.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Components.Shared.Modals;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class ImageManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private ISupabaseStorageService StorageService { get; set; } = default!;
    [Inject] private IImageCompressionService CompressionService { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<ImageManagement> Logger { get; set; } = default!;

    // Object pools for performance
    private MID_ComponentObjectPool<List<ImageItem>>? _imageListPool;
    private MID_ComponentObjectPool<List<UploadQueueItem>>? _uploadQueuePool;

    // Component state
    private bool isLoading = true;
    private bool isUploadModalOpen = false;
    private bool isDetailModalOpen = false;
    private bool isDragging = false;
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
    private string totalStorage = "1 GB";
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

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Initialize object pools
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

            // Load images and storage info
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

            // Load from all folders
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

            // Use pooled list
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
                        ThumbnailUrl = StorageService.GetPublicUrl(file.Name), // TODO: Generate actual thumbnails
                        Folder = ExtractFolderFromPath(file.Name),
                        FileSize = file.Size,
                        Dimensions = "800x800", // TODO: Get actual dimensions
                        UploadedAt = file.UpdatedAt,
                        IsReferenced = false, // TODO: Check if used in products
                        ReferenceCount = 0
                    };

                    imageList.Add(imageItem);
                }
            }

            allImages = new List<ImageItem>(imageList);
            totalImages = allImages.Count;

            // Calculate referenced images count
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
            totalStorage = capacityInfo.FormattedTotalCapacity;

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

        // Apply sorting
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
            // TODO: Show success toast
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
                // TODO: Show warning modal
                await MID_HelperFunctions.DebugMessageAsync(
                    "Cannot delete: Image is used in products",
                    LogLevel.Warning
                );
                return;
            }

            // TODO: Show confirmation modal
            var success = await StorageService.DeleteImageAsync(image.PublicUrl);

            if (success)
            {
                allImages.Remove(image);
                ApplyFiltersAndSort();
                await MID_HelperFunctions.DebugMessageAsync($"Deleted {image.FileName}", LogLevel.Info);
                // TODO: Show success toast
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

            // TODO: Show confirmation modal

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
                // TODO: Show success toast
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk delete");
        }
    }

    private async Task HandleFileSelect(ChangeEventArgs e)
    {
        try
        {
            var files = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "eval", 
                "document.querySelector('input[type=\"file\"]')"
            );

            // TODO: Process file input
            await MID_HelperFunctions.DebugMessageAsync("Files selected", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Handling file selection");
        }
    }

    private async Task HandleDrop(DragEventArgs e)
    {
        isDragging = false;
        
        try
        {
            // TODO: Process dropped files
            await MID_HelperFunctions.DebugMessageAsync("Files dropped", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Handling file drop");
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

            var uploadTasks = new List<Task>();

            foreach (var item in uploadQueue.Where(x => x.Status == "pending"))
            {
                uploadTasks.Add(UploadSingleFile(item));
            }

            await Task.WhenAll(uploadTasks);

            // Reload images after upload
            await LoadImagesAsync();
            await LoadStorageInfoAsync();

            // Clear successful uploads
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

    private async Task UploadSingleFile(UploadQueueItem item)
    {
        try
        {
            item.Status = "uploading";
            item.Progress = 0;
            StateHasChanged();

            // Simulate progress (in real implementation, track actual upload progress)
            for (int i = 0; i <= 100; i += 10)
            {
                item.Progress = i;
                StateHasChanged();
                await Task.Delay(100);
            }

            // TODO: Actually upload file to Supabase Storage
            // var result = await StorageService.UploadImageAsync(stream, item.FileName, "products", uploadFolder);

            item.Status = "success";
            item.Progress = 100;
            
            await MID_HelperFunctions.DebugMessageAsync($"Uploaded {item.FileName}", LogLevel.Info);
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
}
