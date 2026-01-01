// Services/Shop/ShopFilterService.cs
using SubashaVentures.Domain.Shop;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Shop;

/// <summary>
/// Service for managing shop filter state with local storage persistence
/// </summary>
public class ShopFilterService : IShopFilterService
{
    private readonly IBlazorAppLocalStorageService _localStorage;
    private const string FILTER_STORAGE_KEY = "shop_filters";
    
    private FilterState? _currentFilters;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public event EventHandler<FilterState>? OnFiltersChanged;
    
    public ShopFilterService(IBlazorAppLocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }
    
    /// <summary>
    /// Get the current filter state (from memory or storage)
    /// </summary>
    public async Task<FilterState> GetCurrentFiltersAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Return cached filters if available
            if (_currentFilters != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Returning cached filter state",
                    LogLevel.Debug
                );
                return _currentFilters.Clone();
            }
            
            // Try to load from storage
            var stored = await _localStorage.GetItemAsync<FilterState>(FILTER_STORAGE_KEY);
            
            if (stored != null && !stored.IsEmpty)
            {
                _currentFilters = stored;
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Loaded filters from storage: {stored.Categories.Count} categories, {stored.Brands.Count} brands",
                    LogLevel.Info
                );
                return stored.Clone();
            }
            
            // Return default empty filters
            _currentFilters = FilterState.CreateDefault();
            await MID_HelperFunctions.DebugMessageAsync(
                "No stored filters found, returning default",
                LogLevel.Debug
            );
            
            return _currentFilters.Clone();
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Save filter state to local storage
    /// </summary>
    public async Task SaveFiltersAsync(FilterState filters)
    {
        await _lock.WaitAsync();
        try
        {
            if (filters == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Cannot save null filters",
                    LogLevel.Warning
                );
                return;
            }
            
            filters.LastUpdated = DateTime.UtcNow;
            _currentFilters = filters.Clone();
            
            await _localStorage.SetItemAsync(FILTER_STORAGE_KEY, filters);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Saved filters: {filters.Categories.Count} categories, {filters.Brands.Count} brands",
                LogLevel.Info
            );
            
            // Trigger event
            OnFiltersChanged?.Invoke(this, filters.Clone());
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Update only the categories in the current filter state
    /// </summary>
    public async Task UpdateCategoriesAsync(List<string> categories)
    {
        await _lock.WaitAsync();
        try
        {
            var current = await GetCurrentFiltersAsync();
            current.Categories = new List<string>(categories ?? new List<string>());
            current.LastUpdated = DateTime.UtcNow;
            
            await SaveFiltersAsync(current);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Updated categories: [{string.Join(", ", current.Categories)}]",
                LogLevel.Info
            );
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Update search query in the current filter state
    /// </summary>
    public async Task UpdateSearchQueryAsync(string query)
    {
        await _lock.WaitAsync();
        try
        {
            var current = await GetCurrentFiltersAsync();
            current.SearchQuery = query ?? "";
            current.LastUpdated = DateTime.UtcNow;
            
            await SaveFiltersAsync(current);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Updated search query: '{current.SearchQuery}'",
                LogLevel.Info
            );
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Update sort option in the current filter state
    /// </summary>
    public async Task UpdateSortAsync(string sortBy)
    {
        await _lock.WaitAsync();
        try
        {
            var current = await GetCurrentFiltersAsync();
            current.SortBy = sortBy ?? "default";
            current.LastUpdated = DateTime.UtcNow;
            
            await SaveFiltersAsync(current);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Updated sort: {current.SortBy}",
                LogLevel.Info
            );
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Reset filters to default values
    /// </summary>
    public async Task ResetFiltersAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var defaultFilters = FilterState.CreateDefault();
            await SaveFiltersAsync(defaultFilters);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "Filters reset to default",
                LogLevel.Info
            );
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Check if there are any active filters
    /// </summary>
    public async Task<bool> HasActiveFiltersAsync()
    {
        var current = await GetCurrentFiltersAsync();
        return !current.IsEmpty;
    }
    
    /// <summary>
    /// Clear all saved filters from storage
    /// </summary>
    public async Task ClearStorageAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await _localStorage.RemoveItemAsync(FILTER_STORAGE_KEY);
            _currentFilters = null;
            
            await MID_HelperFunctions.DebugMessageAsync(
                "Filter storage cleared",
                LogLevel.Info
            );
        }
        finally
        {
            _lock.Release();
        }
    }
}
