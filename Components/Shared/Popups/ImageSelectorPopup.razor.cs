// Components/Shared/Popups/ImageSelectorPopup.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Components.Shared.Popups;

public partial class ImageSelectorPopup : ComponentBase, IAsyncDisposable
{
    [Inject] private ISupabaseStorageService StorageService { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
    [Inject] private ILogger<ImageSelectorPopup> Logger { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public bool AllowMultiple { get; set; } = true;
    [Parameter] public int MaxSelection { get; set; } = 10;
    [Parameter] public List<string> PreSelectedUrls { get; set; } = new();
    [Parameter] public string? DefaultFolder { get; set; }
    [Parameter] public EventCallback<List<string>> OnImagesSelected { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    // Object pool for image lists
    private MID_ComponentObjectPool<List<ImageInfo>>? _imageListPool;

    private bool isLoading = false;
    private bool isInitialized = false;
    private string searchQuery = "";
    private string selectedFolder = "";
    private string viewSize = "medium";

    private List<string> SelectedImages = new();
    private List<ImageInfo> allImages = new();
    private List<ImageInfo> filteredImages = new();

    // Cache key for storing loaded images
    private const string CACHE_KEY = "image_selector_cache";
    private const int CACHE_DURATION_MINUTES = 5;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Initialize object pool
            _imageListPool = new MID_ComponentObjectPool<List<ImageInfo>>(
                () => new List<ImageInfo>(),
                list => list.Clear(),
                maxPoolSize: 5
            );

            await MID_HelperFunctions.DebugMessageAsync("ImageSelectorPopup initialized", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ImageSelectorPopup initialization");
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && !isInitialized)
        {
            // Set pre-selected images
            if (PreSelectedUrls.Any())
            {
                SelectedImages = new List<string>(PreSelectedUrls);
            }

            // Set default folder if provided
            if (!string.IsNullOrEmpty(DefaultFolder))
            {
                selectedFolder = DefaultFolder;
            }

            // Load images
            await LoadImagesAsync();
            isInitialized = true;
        }
        else if (!IsOpen)
        {
            // Reset when closed
            isInitialized = false;
        }
    }

    private async Task LoadImagesAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            // Try to load from cache first
            var cachedData = await LoadFromCacheAsync();
            if (cachedData != null)
            {
                allImages = cachedData;
                ApplyFilters();
                isLoading = false;
                StateHasChanged();
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Loaded {allImages.Count} images from cache",
                    LogLevel.Info
                );
                return;
            }

            // Load from storage service
            using var pooledImages = _imageListPool?.GetPooled();
            var imageList = pooledImages?.Object ?? new List<ImageInfo>();

            // Define folders to load from
            var folders = new[]
            {
                "products",
                "products/mens",
                "products/womens",
                "products/children",
                "products/baby",
                "products/home",
                "banners",
                "categories"
            };

            var loadTasks = folders.Select(folder => 
                LoadImagesFromFolderAsync(folder, imageList)
            ).ToList();

            await Task.WhenAll(loadTasks);

            allImages = new List<ImageInfo>(imageList);

            // Cache the loaded images
            await SaveToCacheAsync(allImages);

            ApplyFilters();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {allImages.Count} images from storage",
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

    private async Task LoadImagesFromFolderAsync(string folder, List<ImageInfo> imageList)
    {
        try
        {
            var files = await StorageService.ListFilesAsync(folder);

            foreach (var file in files)
            {
                var publicUrl = StorageService.GetPublicUrl(file.Name, "products");
                
                var imageInfo = new ImageInfo
                {
                    Id = file.Id,
                    FileName = file.Name,
                    Url = publicUrl,
                    ThumbnailUrl = publicUrl, // TODO: Use actual thumbnail
                    Folder = folder,
                    FileSize = file.Size,
                    Width = 800, // TODO: Get actual dimensions
                    Height = 800,
                    UploadedAt = file.UpdatedAt
                };

                lock (imageList)
                {
                    imageList.Add(imageInfo);
                }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Loading from folder: {folder}");
        }
    }

    private async Task<List<ImageInfo>?> LoadFromCacheAsync()
    {
        try
        {
            var cacheJson = await LocalStorage.GetItemAsync<string>(CACHE_KEY);
            if (string.IsNullOrEmpty(cacheJson))
                return null;

            var cacheData = JsonHelper.Deserialize<ImageCacheData>(cacheJson);
            if (cacheData == null)
                return null;

            // Check if cache is expired
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

    private async Task SaveToCacheAsync(List<ImageInfo> images)
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

            await MID_HelperFunctions.DebugMessageAsync("Images cached successfully", LogLevel.Debug);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Caching images");
        }
    }

    private void ApplyFilters()
    {
        filteredImages = allImages
            .Where(img =>
                (string.IsNullOrEmpty(selectedFolder) || img.Folder == selectedFolder) &&
                (string.IsNullOrEmpty(searchQuery) ||
                 img.FileName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.UploadedAt)
            .ToList();

        StateHasChanged();
    }

    private void HandleFolderFilter(ChangeEventArgs e)
    {
        selectedFolder = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void HandleImageSelect(ImageInfo image)
    {
        try
        {
            if (!AllowMultiple)
            {
                // Single selection mode
                SelectedImages.Clear();
                SelectedImages.Add(image.Url);
            }
            else
            {
                // Multiple selection mode
                if (SelectedImages.Contains(image.Url))
                {
                    SelectedImages.Remove(image.Url);
                }
                else
                {
                    if (SelectedImages.Count >= MaxSelection)
                    {
                        MID_HelperFunctions.DebugMessage(
                            $"Maximum selection limit ({MaxSelection}) reached",
                            LogLevel.Warning
                        );
                        // TODO: Show toast notification
                        return;
                    }
                    SelectedImages.Add(image.Url);
                }
            }

            StateHasChanged();

            MID_HelperFunctions.DebugMessage(
                $"Selected {SelectedImages.Count} image(s)",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, "Handling image selection");
        }
    }

    private void ClearSelection()
    {
        SelectedImages.Clear();
        StateHasChanged();
        
        MID_HelperFunctions.DebugMessage("Selection cleared", LogLevel.Debug);
    }

    private async Task HandleConfirm()
    {
        try
        {
            if (!SelectedImages.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No images selected",
                    LogLevel.Warning
                );
                return;
            }

            if (OnImagesSelected.HasDelegate)
            {
                await OnImagesSelected.InvokeAsync(new List<string>(SelectedImages));
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Confirmed selection of {SelectedImages.Count} image(s)",
                    LogLevel.Info
                );
            }

            await Close();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Confirming selection");
        }
    }

    private async Task Close()
    {
        try
        {
            IsOpen = false;
            
            if (OnClose.HasDelegate)
            {
                await OnClose.InvokeAsync();
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Closing popup");
        }
    }

    private void HandleOverlayClick()
    {
        // Close on overlay click
        _ = Close();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _imageListPool?.Dispose();

            await MID_HelperFunctions.DebugMessageAsync(
                "ImageSelectorPopup disposed successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disposing ImageSelectorPopup");
        }
    }

    // Data models
    public class ImageInfo
    {
        public string Id { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Url { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string Folder { get; set; } = "";
        public long FileSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime UploadedAt { get; set; }

        public string Dimensions => $"{Width}x{Height}";

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
        public List<ImageInfo> Images { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }
}
