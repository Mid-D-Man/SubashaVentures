using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Components.Admin.Images;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Domain.Enums;
using Microsoft.JSInterop;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Components.Shared.Popups;

public partial class ImageSelectorPopup : ComponentBase, IAsyncDisposable
{
    [Inject] private ISupabaseStorageService StorageService { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
    [Inject] private IFirestoreService FirestoreService { get; set; } = default!;
    [Inject] private IImageCacheService ImageCacheService { get; set; } = default!; // ✅ NEW
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<ImageSelectorPopup> Logger { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public bool AllowMultiple { get; set; } = true;
    [Parameter] public int MaxSelection { get; set; } = 10;
    [Parameter] public List<string> PreSelectedUrls { get; set; } = new();
    [Parameter] public string? DefaultFolder { get; set; }
    [Parameter] public EventCallback<List<string>> OnImagesSelected { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>? _imageListPool;

    private bool isLoading = false;
    private bool isInitialized = false;
    
    private string _searchQuery = "";
    private string searchQuery 
    { 
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                ApplyFilters();
                StateHasChanged();
            }
        }
    }
    
    private string selectedFolder = "";
    private string viewSize = "medium";

    private List<string> SelectedImages = new();
    private List<AdminImageCard.ImageItem> allImages = new();
    private List<AdminImageCard.ImageItem> filteredImages = new();
    private List<CategoryModel> categories = new();

    private const string CACHE_KEY = "image_selector_cache";
    private const int CACHE_DURATION_MINUTES = 5;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _imageListPool = new MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>(
                () => new List<AdminImageCard.ImageItem>(),
                list => list.Clear(),
                maxPoolSize: 5
            );

            await MID_HelperFunctions.DebugMessageAsync("ImageSelectorPopup initialized", LogLevel.Info);
            
            await LoadCategoriesAsync();
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
            if (PreSelectedUrls.Any())
            {
                SelectedImages = new List<string>(PreSelectedUrls);
            }

            if (!string.IsNullOrEmpty(DefaultFolder))
            {
                selectedFolder = DefaultFolder;
            }

            await LoadImagesAsync();
            isInitialized = true;
        }
        else if (!IsOpen)
        {
            isInitialized = false;
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

            // ✅ Try cache first
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

            using var pooledImages = _imageListPool?.GetPooled();
            var imageList = pooledImages?.Object ?? new List<AdminImageCard.ImageItem>();
            var allImageUrls = new List<string>();

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

            // ✅ Preload into browser cache
            if (allImageUrls.Any())
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ImageCacheService.PreloadImagesAsync(allImageUrls);
                    }
                    catch (Exception ex)
                    {
                        await MID_HelperFunctions.LogExceptionAsync(ex, "Preloading images");
                    }
                });
            }

            await SaveToCacheAsync(allImages);
            ApplyFilters();

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

    private void ApplyFilters()
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

        StateHasChanged();
    }

    private void HandleFolderFilter(ChangeEventArgs e)
    {
        selectedFolder = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void HandleImageSelect(AdminImageCard.ImageItem image)
    {
        try
        {
            if (!AllowMultiple)
            {
                SelectedImages.Clear();
                SelectedImages.Add(image.PublicUrl);
            }
            else
            {
                if (SelectedImages.Contains(image.PublicUrl))
                {
                    SelectedImages.Remove(image.PublicUrl);
                }
                else
                {
                    if (SelectedImages.Count >= MaxSelection)
                    {
                        MID_HelperFunctions.DebugMessage(
                            $"Maximum selection limit ({MaxSelection}) reached",
                            LogLevel.Warning
                        );
                        return;
                    }
                    SelectedImages.Add(image.PublicUrl);
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
                    $"✓ Confirmed selection of {SelectedImages.Count} image(s)",
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

    private class ImageCacheData
    {
        public List<AdminImageCard.ImageItem> Images { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }
}