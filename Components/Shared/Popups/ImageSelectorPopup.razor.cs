// Components/Shared/Popups/ImageSelectorPopup.razor.cs
// Added: pagination, reset-on-close, sort by type/size/name, proper state management.

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    [Inject] private ISupabaseStorageService      StorageService    { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage      { get; set; } = default!;
    [Inject] private IFirestoreService            FirestoreService  { get; set; } = default!;
    [Inject] private IImageCacheService           ImageCacheService { get; set; } = default!;
    [Inject] private IJSRuntime                   JSRuntime         { get; set; } = default!;
    [Inject] private ILogger<ImageSelectorPopup>  Logger            { get; set; } = default!;

    // ─── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public bool IsOpen                                   { get; set; }
    [Parameter] public bool AllowMultiple                            { get; set; } = true;
    [Parameter] public int  MaxSelection                             { get; set; } = 10;
    [Parameter] public List<string> PreSelectedUrls                  { get; set; } = new();
    [Parameter] public string? DefaultFolder                         { get; set; }
    [Parameter] public EventCallback<List<string>> OnImagesSelected  { get; set; }
    [Parameter] public EventCallback OnClose                         { get; set; }

    // ─── Loading ──────────────────────────────────────────────────────────────

    private bool isLoading      = false;
    private bool isLoadingMore  = false;
    private bool isInitialized  = false;

    private int loadedFolderCount = 0;
    private int totalFolderCount  = 0;
    private int loadingProgress   =>
        totalFolderCount > 0
            ? (int)Math.Round(loadedFolderCount * 100.0 / totalFolderCount)
            : 0;

    // ─── Filter / sort / view / pagination ───────────────────────────────────

    private string searchQuery    = "";
    private string selectedFolder = "";
    private string sortBy         = "newest";
    private string viewSize       = "medium";
    private int    currentPage    = 1;
    private int    pageSize       = 24;

    private int totalPages =>
        filteredImages.Count > 0
            ? (int)Math.Ceiling(filteredImages.Count / (double)pageSize)
            : 0;

    private bool HasActiveFilters =>
        !string.IsNullOrEmpty(searchQuery) || !string.IsNullOrEmpty(selectedFolder);

    // ─── Data ─────────────────────────────────────────────────────────────────

    private List<string>                   SelectedImages = new();
    private List<AdminImageCard.ImageItem> allImages      = new();
    private List<AdminImageCard.ImageItem> filteredImages = new();
    private List<AdminImageCard.ImageItem> pagedImages    = new();
    private List<CategoryModel>            categories     = new();

    private MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>? _pool;

    private const string CACHE_KEY              = "image_selector_cache";
    private const int    CACHE_DURATION_MINUTES = 5;
    private const int    BATCH_SIZE             = 2;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        _pool = new MID_ComponentObjectPool<List<AdminImageCard.ImageItem>>(
            () => new List<AdminImageCard.ImageItem>(),
            l  => l.Clear(),
            maxPoolSize: 5);

        await LoadCategoriesAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && !isInitialized)
        {
            // Apply pre-selections
            SelectedImages = PreSelectedUrls.Any()
                ? new List<string>(PreSelectedUrls)
                : new List<string>();

            if (!string.IsNullOrEmpty(DefaultFolder))
                selectedFolder = DefaultFolder;

            await LoadImagesAsync();
            isInitialized = true;
        }
        else if (!IsOpen && isInitialized)
        {
            // Reset everything when popup closes so it's clean next time
            ResetState();
            isInitialized = false;
        }
    }

    // ─── State reset ──────────────────────────────────────────────────────────

    private void ResetState()
    {
        searchQuery    = "";
        selectedFolder = "";
        sortBy         = "newest";
        currentPage    = 1;
        SelectedImages.Clear();
        filteredImages.Clear();
        pagedImages.Clear();
        // Note: allImages and categories are kept — avoids re-fetching on next open
        // (cache handles freshness). Clear them too if you want a full reset:
        // allImages.Clear();
    }

    // ─── Data loading ─────────────────────────────────────────────────────────

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var loaded = await FirestoreService.GetCollectionAsync<CategoryModel>("categories");
            categories = loaded?.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToList()
                         ?? new List<CategoryModel>();
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
            var cached = await LoadFromCacheAsync();
            if (cached != null)
            {
                allImages = cached;
                ApplyFilters();
                return;
            }

            var folders = categories.Any()
                ? categories.Select(c => c.Slug).ToArray()
                : new[] { "products", "banners", "categories" };

            totalFolderCount  = folders.Length;
            loadedFolderCount = 0;
            isLoading         = true;
            StateHasChanged();

            var allImageUrls = new List<string>();

            for (int batchStart = 0; batchStart < folders.Length; batchStart += BATCH_SIZE)
            {
                var batch = folders.Skip(batchStart).Take(BATCH_SIZE).ToArray();

                if (batchStart > 0) { isLoading = false; isLoadingMore = true; }

                var batchImages = new List<AdminImageCard.ImageItem>();
                var batchUrls   = new List<string>();

                await Task.WhenAll(batch.Select(f =>
                    LoadImagesFromFolderAsync(f, batchImages, batchUrls)));

                lock (allImages) { allImages.AddRange(batchImages); }
                allImageUrls.AddRange(batchUrls);
                loadedFolderCount += batch.Length;

                ApplyFilters();
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
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ImageSelector.LoadImages");
            Logger.LogError(ex, "Failed to load images for selector");
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
                    FileSize       = file.Size,   // real size from Supabase metadata
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
            await MID_HelperFunctions.LogExceptionAsync(ex, $"ISP LoadFolder: {folder}");
        }
    }

    // ─── Filtering / sorting / pagination ─────────────────────────────────────

    private void ApplyFilters()
    {
        IEnumerable<AdminImageCard.ImageItem> results = allImages;

        if (!string.IsNullOrEmpty(selectedFolder))
            results = results.Where(img =>
                img.Folder.Equals(selectedFolder, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(searchQuery))
        {
            var q = searchQuery.Trim();
            results = results.Where(img =>
                img.FileName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                img.Folder.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        results = sortBy switch
        {
            "oldest"       => results.OrderBy(x => x.UploadedAt),
            "name-az"      => results.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase),
            "name-za"      => results.OrderByDescending(x => x.FileName, StringComparer.OrdinalIgnoreCase),
            "size-largest" => results.OrderByDescending(x => x.FileSize),
            "size-smallest"=> results.OrderBy(x => x.FileSize),
            "type-az"      => results.OrderBy(x => Path.GetExtension(x.FileName).ToLowerInvariant())
                                     .ThenBy(x => x.FileName, StringComparer.OrdinalIgnoreCase),
            _              => results.OrderByDescending(x => x.UploadedAt)
        };

        filteredImages = results.ToList();
        currentPage    = 1;   // always reset to page 1 on filter change
        UpdatePagedImages();
    }

    private void UpdatePagedImages()
    {
        pagedImages = filteredImages
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        StateHasChanged();
    }

    // ─── Pagination event handlers ────────────────────────────────────────────

    private void PreviousPage()
    {
        if (currentPage > 1) { currentPage--; UpdatePagedImages(); }
    }

    private void NextPage()
    {
        if (currentPage < totalPages) { currentPage++; UpdatePagedImages(); }
    }

    private void GoToPage(int p)
    {
        if (p >= 1 && p <= totalPages) { currentPage = p; UpdatePagedImages(); }
    }

    private void HandlePageSizeChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var size))
        {
            pageSize    = size;
            currentPage = 1;
            UpdatePagedImages();
        }
    }

    // ─── UI event handlers ────────────────────────────────────────────────────

    private void HandleSearchInput(ChangeEventArgs e)
    {
        searchQuery = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void HandleSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") ClearSearch();
    }

    private void ClearSearch() { searchQuery = ""; ApplyFilters(); }

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

    private void SetViewSize(string size) { viewSize = size; StateHasChanged(); }

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

    private void ClearSelection() { SelectedImages.Clear(); StateHasChanged(); }

    private async Task HandleConfirm()
    {
        if (!SelectedImages.Any()) return;

        if (OnImagesSelected.HasDelegate)
            await OnImagesSelected.InvokeAsync(new List<string>(SelectedImages));

        await CloseInternal();
    }

    /// <summary>
    /// Called by Cancel button and the X close button.
    /// Fires OnClose callback — parent sets IsOpen=false which triggers
    /// OnParametersSetAsync → ResetState().
    /// </summary>
    private async Task HandleClose()
    {
        await CloseInternal();
    }

    private async Task CloseInternal()
    {
        if (OnClose.HasDelegate) await OnClose.InvokeAsync();
        // ResetState() is called inside OnParametersSetAsync when IsOpen becomes false
    }

    private void HandleOverlayClick() => _ = HandleClose();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private string GetCategoryName(string slug) =>
        categories.FirstOrDefault(c =>
            c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))?.Name ?? slug;

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
            var json = JsonHelper.Serialize(new ImageCacheData
            {
                Images   = images,
                CachedAt = DateTime.UtcNow
            });
            await LocalStorage.SetItemAsync(CACHE_KEY, json);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ISP SaveCache");
        }
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        try { _pool?.Dispose(); }
        catch (Exception ex) { Logger.LogError(ex, "ISP Dispose error"); }
    }

    // ─── Inner types ──────────────────────────────────────────────────────────

    private class ImageCacheData
    {
        public List<AdminImageCard.ImageItem> Images   { get; set; } = new();
        public DateTime                       CachedAt { get; set; }
    }
}
