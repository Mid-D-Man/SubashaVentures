// Services/Shop/ShopStateService.cs - WITH LOCALSTORAGE PERSISTENCE
using SubashaVentures.Domain.Shop;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Shop;

/// <summary>
/// Centralized state management for shop WITH LocalStorage persistence
/// </summary>
public class ShopStateService
{
    private readonly IBlazorAppLocalStorageService _localStorage;
    private FilterState _currentFilters = FilterState.CreateDefault();
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    private const string FILTERS_STORAGE_KEY = "shop_filters";
    private bool _isInitialized = false;
    
    public event Func<string, Task>? OnSearchChanged;
    public event Func<FilterState, Task>? OnFiltersChanged;
    
    public ShopStateService(IBlazorAppLocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }
    
    /// <summary>
    /// Initialize from localStorage
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        
        await _lock.WaitAsync();
        try
        {
            if (_isInitialized) return;
            
            // Load from localStorage
            var stored = await _localStorage.GetItemAsync<FilterState>(FILTERS_STORAGE_KEY);
            
            if (stored != null)
            {
                _currentFilters = stored;
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Loaded filters from localStorage: {_currentFilters.Categories.Count} categories",
                    LogLevel.Info
                );
            }
            
            _isInitialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Get current filter state (with localStorage fallback)
    /// </summary>
    public async Task<FilterState> GetCurrentFiltersAsync()
    {
        await EnsureInitializedAsync();
        
        await _lock.WaitAsync();
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"📋 Current filters: {_currentFilters.Categories.Count} categories, search: '{_currentFilters.SearchQuery}'",
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
    /// Update filters, save to localStorage, and notify listeners
    /// </summary>
    public async Task UpdateFiltersAsync(FilterState filters)
    {
        await EnsureInitializedAsync();
        
        await _lock.WaitAsync();
        try
        {
            _currentFilters = filters.Clone();
            _currentFilters.LastUpdated = DateTime.UtcNow;
            
            // ✅ SAVE TO LOCALSTORAGE
            await _localStorage.SetItemAsync(FILTERS_STORAGE_KEY, _currentFilters);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Filters updated & saved: {_currentFilters.Categories.Count} categories, {_currentFilters.Brands.Count} brands",
                LogLevel.Info
            );
            
            // Notify all listeners
            if (OnFiltersChanged != null)
            {
                await OnFiltersChanged.Invoke(_currentFilters.Clone());
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Update only categories
    /// </summary>
    public async Task UpdateCategoriesAsync(List<string> categories)
    {
        await EnsureInitializedAsync();
        
        await _lock.WaitAsync();
        try
        {
            _currentFilters.Categories = new List<string>(categories ?? new List<string>());
            _currentFilters.LastUpdated = DateTime.UtcNow;
            
            // ✅ SAVE TO LOCALSTORAGE
            await _localStorage.SetItemAsync(FILTERS_STORAGE_KEY, _currentFilters);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Categories updated & saved: [{string.Join(", ", _currentFilters.Categories)}]",
                LogLevel.Info
            );
            
            // Notify listeners
            if (OnFiltersChanged != null)
            {
                await OnFiltersChanged.Invoke(_currentFilters.Clone());
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Update search query
    /// </summary>
    public async Task NotifySearchChanged(string query)
    {
        await EnsureInitializedAsync();
        
        await _lock.WaitAsync();
        try
        {
            _currentFilters.SearchQuery = query ?? "";
            _currentFilters.LastUpdated = DateTime.UtcNow;
            
            // ✅ SAVE TO LOCALSTORAGE
            await _localStorage.SetItemAsync(FILTERS_STORAGE_KEY, _currentFilters);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Search updated & saved: '{query}'",
                LogLevel.Info
            );
            
            // Notify search listeners
            if (OnSearchChanged != null)
            {
                await OnSearchChanged.Invoke(query ?? "");
            }
            
            // Also notify filter listeners
            if (OnFiltersChanged != null)
            {
                await OnFiltersChanged.Invoke(_currentFilters.Clone());
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Notify filters changed (for backward compatibility)
    /// </summary>
    public async Task NotifyFiltersChanged(FilterState filters)
    {
        await UpdateFiltersAsync(filters);
    }
    
    /// <summary>
    /// Reset filters to default and clear localStorage
    /// </summary>
    public async Task ResetFiltersAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _currentFilters = FilterState.CreateDefault();
            
            // ✅ CLEAR FROM LOCALSTORAGE
            await _localStorage.RemoveItemAsync(FILTERS_STORAGE_KEY);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "🔄 Filters reset & cleared from storage",
                LogLevel.Info
            );
            
            // Notify listeners
            if (OnFiltersChanged != null)
            {
                await OnFiltersChanged.Invoke(_currentFilters.Clone());
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Check if filters are active
    /// </summary>
    public bool HasActiveFilters()
    {
        return !_currentFilters.IsEmpty;
    }
}