using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Components.Admin.Images;
using SubashaVentures.Models.Firebase;
using Microsoft.JSInterop;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Components.Shared.Popups;

public partial class ImageSelectorPopup : ComponentBase, IAsyncDisposable
{
    [Inject] private ISupabaseStorageService StorageService  { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
    [Inject] private IFirestoreService FirestoreService       { get; set; } = default!;
    [Inject] private IImageCacheService ImageCacheService     { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime                     { get; set; } = default!;
    [Inject] private ILogger<ImageSelectorPopup> Logger       { get; set; } = default!;

    // ─── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public bool IsOpen                                          { get; set; }
    [Parameter] public bool AllowMultiple                                   { get; set; } = true;
    [Parameter] public int MaxSelection                                     { get; set; } = 10;
    [Parameter] public List<string> PreSelectedUrls                         { get; set; } = new();
    [Parameter] public string? DefaultFolder                                { get; set; }
    [Parameter] public EventCallback<List<string>> OnImagesSelected         { get; set; }
    [Parameter] public EventCallback OnClose                                { get; set; }

    // ─── State ────────────────────────────────────────────────────────────────

    private MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>? _imageListPool;

    private bool isLoading      = false;
    private bool isInitialized  = false;

    private string searchQuery    = "";
    private string selectedFolder = "";
    private string sortBy         = "newest";
    private string viewSize       = "medium";

    private List<string>                   SelectedImages  = new();
    private List<AdminImageCard.ImageItem> allImages       = new();
    private List<AdminImageCard.ImageItem> filteredImages  = new();
    private List<CategoryModel>            categories      = new();

    // Computed: any filter active?
    private bool HasActiveFilters =>
        !string.IsNullOrEmpty(searchQuery) || !string.IsNullOrEmpty(selectedFolder);

    private const string CACHE_KEY            = "image_selector_cache";
    private const int    CACHE_DURATION_MINUTES = 5;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        _imageListPool = new MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>(
            () => new List<AdminImageCard.ImageItem>(),
            list => list.Clear(),
            maxPoolSize: 5);

        await LoadCategoriesAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && !isInitialized)
        {
            if (PreSelectedUrls.Any())
                SelectedImages = new List<string>(PreSelectedUrls);

            if (!string.IsNullOrEmpty(DefaultFolder))
                selectedFolder = DefaultFolder;

            await LoadImagesAsync();
            isInitialized = true;
        }
        else if (!IsOpen)
        {
            isInitialized = false;
        }
    }

    // ─── Data loading ─────────────────────────────────────────────────────────

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var loaded = await FirestoreService.GetCollectionAsync<CategoryModel>("categories");

            categories = loaded != null && loaded.Any()
                ? loaded.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToList()
                : new List<CategoryModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"[ImageSelector] Loaded {categories.Count} categories", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ImageSelector.LoadCategories");
            categories = new List<CategoryModel>();
        }
    }

    private async Task LoadImagesAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var cached = await LoadFromCacheAsync();
            if (cached != null)
            {
                allImages = cached;
                ApplyFilters();
                isLoading = false;
                StateHasChanged();
                await MID_HelperFunctions.DebugMessageAsync(
                    $"[ImageSelector] {allImages.Count} images from cache", LogLevel.Info);
                return;
            }

            using var pooled   = _imageListPool?.GetPooled();
            var imageList      = pooled?.Object ?? new List<AdminImageCard.ImageItem>();
            var allImageUrls   = new List<string>();

            var folders = categories.Any()
                ? categories.Select(c => c.Slug).ToArray()
                : new[] { "products", "banners", "categories" };

            foreach (var folder in folders)
                await LoadImagesFromFolderAsync(folder, imageList, allImageUrls);

            allImages = new List<AdminImageCard.ImageItem>(imageList);

            // Preload thumbnails in background
            if (allImageUrls.Any())
            {
                _ = Task.Run(async () =>
                {
                    try { await ImageCacheService.PreloadImagesAsync(allImageUrls); }
                    catch (Exception ex) { await MID_HelperFunctions.LogExceptionAsync(ex, "Preload"); }
                });
            }

            await SaveToCacheAsync(allImages);
            ApplyFilters();

            await MID_HelperFunctions.DebugMessageAsync(
                $"[ImageSelector] ✓ Loaded {allImages.Count} images", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ImageSelector.LoadImages");
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
            if (!files.Any()) return;

            foreach (var file in files)
            {
                var filePath  = $"{folder}/{file.Name}";
                var publicUrl = StorageService.GetPublicUrl(filePath, "products");

                allImageUrls.Add(publicUrl);

                var imageInfo = new AdminImageCard.ImageItem
                {
                    Id           = file.Id,
                    FileName     = file.Name,
                    PublicUrl    = publicUrl,
                    ThumbnailUrl = publicUrl,
                    Folder       = folder,
                    FileSize     = await GetFileSizeAsync(publicUrl),
                    Dimensions   = await GetImageDimensionsAsync(publicUrl),
                    UploadedAt   = file.UpdatedAt,
                    IsReferenced = false,
                    ReferenceCount = 0
                };

                lock (imageList) { imageList.Add(imageInfo); }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"LoadFolder: {folder}");
        }
    }

    private async Task<long> GetFileSizeAsync(string imageUrl)
    {
        try
        {
            return await JSRuntime.InvokeAsync<long>("eval", $@"
                fetch('{imageUrl}', {{method: 'HEAD'}})
                    .then(r => {{ const l = r.headers.get('Content-Length'); return l ? parseInt(l) : 0; }})
                    .catch(() => 0);
            ");
        }
        catch { return 0; }
    }

    private async Task<string> GetImageDimensionsAsync(string imageUrl)
    {
        try
        {
            var d = await JSRuntime.InvokeAsync<string>("eval", $@"
                new Promise(resolve => {{
                    const img = new Image();
                    img.onload  = () => resolve(`${{img.width}}x${{img.height}}`);
                    img.onerror = () => resolve('Unknown');
                    img.src = '{imageUrl}';
                }});
            ");
            return d ?? "Unknown";
        }
        catch { return "Unknown"; }
    }

    // ─── Filtering / sorting ──────────────────────────────────────────────────

    private void ApplyFilters()
    {
        var query = searchQuery.Trim();

        var results = allImages.AsEnumerable();

        // Category filter
        if (!string.IsNullOrEmpty(selectedFolder))
            results = results.Where(img =>
                img.Folder.Equals(selectedFolder, StringComparison.OrdinalIgnoreCase));

        // Search filter — matches filename or folder name
        if (!string.IsNullOrEmpty(query))
            results = results.Where(img =>
                img.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                img.Folder.Contains(query, StringComparison.OrdinalIgnoreCase));

        // Sort
        results = sortBy switch
        {
            "oldest"  => results.OrderBy(x => x.UploadedAt),
            "name-az" => results.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase),
            "name-za" => results.OrderByDescending(x => x.FileName, StringComparer.OrdinalIgnoreCase),
            _         => results.OrderByDescending(x => x.UploadedAt) // "newest"
        };

        filteredImages = results.ToList();
        StateHasChanged();
    }

    // ─── Event handlers ───────────────────────────────────────────────────────

    private void HandleSearchInput(ChangeEventArgs e)
    {
        searchQuery = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void HandleSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            ClearSearch();
    }

    private void ClearSearch()
    {
        searchQuery = "";
        ApplyFilters();
    }

    private void HandleFolderFilter(ChangeEventArgs e)
    {
        selectedFolder = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "newest";
        ApplyFilters();
    }

    private void ResetAllFilters()
    {
        searchQuery    = "";
        selectedFolder = "";
        sortBy         = "newest";
        ApplyFilters();
    }

    private void SetViewSize(string size)
    {
        viewSize = size;
        StateHasChanged();
    }

    private void HandleImageSelect(AdminImageCard.ImageItem image)
    {
        if (!AllowMultiple)
        {
            SelectedImages.Clear();
            SelectedImages.Add(image.PublicUrl);
        }
        else
        {
            if (SelectedImages.Contains(image.PublicUrl))
                SelectedImages.Remove(image.PublicUrl);
            else
            {
                if (SelectedImages.Count >= MaxSelection)
                {
                    MID_HelperFunctions.DebugMessage(
                        $"Max selection ({MaxSelection}) reached", LogLevel.Warning);
                    return;
                }
                SelectedImages.Add(image.PublicUrl);
            }
        }

        StateHasChanged();
    }

    private void ClearSelection()
    {
        SelectedImages.Clear();
        StateHasChanged();
    }

    private async Task HandleConfirm()
    {
        if (!SelectedImages.Any()) return;

        if (OnImagesSelected.HasDelegate)
            await OnImagesSelected.InvokeAsync(new List<string>(SelectedImages));

        await Close();
    }

    private async Task Close()
    {
        IsOpen = false;
        if (OnClose.HasDelegate) await OnClose.InvokeAsync();
        StateHasChanged();
    }

    private void HandleOverlayClick() => _ = Close();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private string GetCategoryName(string slug)
    {
        var cat = categories.FirstOrDefault(c =>
            c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        return cat?.Name ?? slug;
    }

    // ─── Cache ────────────────────────────────────────────────────────────────

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
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "SaveCache");
        }
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        try { _imageListPool?.Dispose(); }
        catch (Exception ex) { Logger.LogError(ex, "Dispose error"); }
    }

    // ─── Internal types ───────────────────────────────────────────────────────

    private class ImageCacheData
    {
        public List<AdminImageCard.ImageItem> Images { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }
}
