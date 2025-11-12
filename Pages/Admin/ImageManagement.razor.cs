// Pages/Admin/ImageManagement.razor.cs - COMPLETE FIX
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Admin.Images;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class ImageManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private ISupabaseStorageService StorageService { get; set; } = default!;
    [Inject] private IImageCompressionService CompressionService { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<ImageManagement> Logger { get; set; } = default!;
[Inject] private IFirestoreService FirestoreService { get; set; } = default!;

private List<CategoryModel> categories = new();

    private MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>? _imageListPool;
    private MID_ComponentObjectPool<List<UploadQueueItem>>? _uploadQueuePool;

    private bool isLoading = true;
    private bool isUploadModalOpen = false;
    private bool isUploading = false;
    private bool isDragging = false;
    private bool enableCompression = true;
    
    // Confirmation popup state
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

        // Load categories from Firestore
        await LoadCategoriesAsync();
        
        // Load images
        await LoadImagesAsync();
        await LoadStorageInfoAsync();
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "ImageManagement initialization");
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
            categories = loadedCategories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToList();
            
            // Set default upload folder to first category
            if (categories.Any())
            {
                uploadFolder = categories.First().Slug;
            }
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {categories.Count} categories from Firestore",
                LogLevel.Info
            );
        }
        else
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "No categories found in Firestore",
                LogLevel.Warning
            );
        }
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Loading categories");
    }
}
    private async Task LoadImagesAsync()
{
    try
    {
        isLoading = true;
        StateHasChanged();

        // Use category slugs from database
        var folders = categories.Any() 
            ? categories.Select(c => c.Slug).ToArray()
            : new[] { "products" }; // Fallback

        using var pooledImages = _imageListPool?.GetPooled();
        var imageList = pooledImages?.Object ?? new List<AdminImageCard.ImageItem>();

        foreach (var folder in folders)
        {
            try
            {
                var files = await StorageService.ListFilesAsync(folder);

                foreach (var file in files)
                {
                    // Build proper public URL
                    var publicUrl = StorageService.GetPublicUrl($"{folder}/{file.Name}");
                    
                    var imageItem = new AdminImageCard.ImageItem
                    {
                        Id = file.Id,
                        FileName = file.Name,
                        PublicUrl = publicUrl,
                        ThumbnailUrl = publicUrl,
                        Folder = folder,
                        FileSize = await GetFileSizeAsync(publicUrl),
                        Dimensions = await GetImageDimensionsAsync(publicUrl),
                        UploadedAt = file.UpdatedAt,
                        IsReferenced = false,
                        ReferenceCount = 0
                    };

                    imageList.Add(imageItem);
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Loading from folder: {folder}");
            }
        }

        allImages = new List<AdminImageCard.ImageItem>(imageList);
        totalImages = allImages.Count;
        totalFolders = folders.Length;
        referencedImages = allImages.Count(img => img.IsReferenced);

        ApplyFiltersAndSort();

        await MID_HelperFunctions.DebugMessageAsync(
            $"Loaded {totalImages} images from {totalFolders} categories", 
            LogLevel.Info
        );
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Loading images");
    }
    finally
    {
        isLoading = false;
        StateHasChanged();
    }
}

    private async Task<long> GetFileSizeAsync(string imageUrl)
    {
        try
        {
            // Use fetch API to get Content-Length header
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

    private async Task LoadStorageInfoAsync()
    {
        try
        {
            // Calculate total size from all images
            var totalSize = allImages.Sum(img => img.FileSize);
            var totalCapacity = 1L * 1024L * 1024L * 1024L; // 1GB

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

    private void ApplyFiltersAndSort()
    {
        // Apply folder filter
        var query = allImages.AsEnumerable();
        
        if (!string.IsNullOrEmpty(selectedFolder))
        {
            query = query.Where(img => img.Folder == selectedFolder);
        }

        // Apply search filter
        if (!string.IsNullOrEmpty(searchQuery))
        {
            var search = searchQuery.ToLower();
            query = query.Where(img => 
                img.FileName.ToLower().Contains(search) ||
                img.Folder.ToLower().Contains(search));
        }

        // Apply sorting
        filteredImages = sortBy switch
        {
            "newest" => query.OrderByDescending(x => x.UploadedAt).ToList(),
            "oldest" => query.OrderBy(x => x.UploadedAt).ToList(),
            "name-az" => query.OrderBy(x => x.FileName).ToList(),
            "name-za" => query.OrderByDescending(x => x.FileName).ToList(),
            "size-largest" => query.OrderByDescending(x => x.FileSize).ToList(),
            "size-smallest" => query.OrderBy(x => x.FileSize).ToList(),
            _ => query.ToList()
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

    private async Task HandleCopyUrl(AdminImageCard.ImageItem image)
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
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Downloading image");
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
                // TODO: Show toast notification
                return;
            }

            // Store image for deletion and show confirmation
            imageToDelete = image;
            imagesToDelete.Clear();
            isConfirmationOpen = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Preparing delete");
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
                await Task.Delay(500);
            }
        }
        selectedImages.Clear();
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
                return;
            }

            // Store images for deletion and show confirmation
            imageToDelete = null;
            imagesToDelete = imagesToDeleteList.Select(x => $"{x.Folder}/{x.FileName}").ToList();
            isConfirmationOpen = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Preparing bulk delete");
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
                // Single image deletion
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
                }
            }
            else if (imagesToDelete.Any())
            {
                // Bulk deletion
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Deleting {imagesToDelete.Count} images",
                    LogLevel.Info
                );

                success = await StorageService.DeleteImagesAsync(imagesToDelete, "products");

                if (success)
                {
                    // Remove deleted images from collection
                    var fileNames = imagesToDelete.Select(p => Path.GetFileName(p)).ToHashSet();
                    allImages.RemoveAll(x => fileNames.Contains(x.FileName));
                    
                    selectedImages.Clear();
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ {imagesToDelete.Count} images deleted successfully",
                        LogLevel.Info
                    );
                }
            }

            if (success)
            {
                await LoadStorageInfoAsync();
                ApplyFiltersAndSort();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Delete operation failed",
                    LogLevel.Error
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Confirming delete");
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
            
            foreach (var file in files)
            {
                var validation = await CompressionService.ValidateImageAsync(file);
                
                if (!validation.IsValid)
                {
                    continue;
                }

                var previewStream = file.OpenReadStream(2 * 1024 * 1024);
                var buffer = new byte[Math.Min(file.Size, 2 * 1024 * 1024)];
                var bytesRead = await previewStream.ReadAsync(buffer, 0, buffer.Length);
                
                var actualBuffer = new byte[bytesRead];
                Array.Copy(buffer, actualBuffer, bytesRead);
                
                var base64 = Convert.ToBase64String(actualBuffer);
                var previewUrl = $"data:{file.ContentType};base64,{base64}";

                uploadQueue.Add(new UploadQueueItem
                {
                    FileName = file.Name,
                    PreviewUrl = previewUrl,
                    FileSize = file.Size,
                    Status = "pending",
                    BrowserFile = file
                });
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "File selection");
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
                    item.Progress = 30;
                    StateHasChanged();

                    if (item.BrowserFile != null)
                    {
                        var result = await StorageService.UploadImageAsync(
                            item.BrowserFile,
                            "products",
                            uploadFolder,
                            enableCompression
                        );

                        if (result.Success)
                        {
                            item.Status = "success";
                            item.Progress = 100;
                        }
                        else
                        {
                            item.Status = "error";
                            item.ErrorMessage = result.ErrorMessage ?? "Upload failed";
                        }
                    }
                }
                catch (Exception ex)
                {
                    item.Status = "error";
                    item.ErrorMessage = ex.Message;
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
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Upload");
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
        public IBrowserFile? BrowserFile { get; set; }

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
