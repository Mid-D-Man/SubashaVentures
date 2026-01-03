// Services/Shop/ShopStateService.cs - CONSOLIDATED STATE MANAGEMENT
using SubashaVentures.Domain.Shop;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Shop;

/// <summary>
/// Centralized state management for shop with in-memory filters (no storage)
/// </summary>
public class ShopStateService
{
    private readonly IBlazorAppLocalStorageService _localStorage;
    private FilterState _currentFilters = FilterState.CreateDefault();
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public event Func<string, Task>? OnSearchChanged;
    public event Func<FilterState, Task>? OnFiltersChanged;
    
    public ShopStateService(IBlazorAppLocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }
    
    /// <summary>
    /// Get current filter state (in-memory only)
    /// </summary>
    public async Task<FilterState> GetCurrentFiltersAsync()
    {
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
    /// Update filters and notify listeners
    /// </summary>
    public async Task UpdateFiltersAsync(FilterState filters)
    {
        await _lock.WaitAsync();
        try
        {
            _currentFilters = filters.Clone();
            _currentFilters.LastUpdated = DateTime.UtcNow;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Filters updated: {_currentFilters.Categories.Count} categories, {_currentFilters.Brands.Count} brands",
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
        await _lock.WaitAsync();
        try
        {
            _currentFilters.Categories = new List<string>(categories ?? new List<string>());
            _currentFilters.LastUpdated = DateTime.UtcNow;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Categories updated: [{string.Join(", ", _currentFilters.Categories)}]",
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
        await _lock.WaitAsync();
        try
        {
            _currentFilters.SearchQuery = query ?? "";
            _currentFilters.LastUpdated = DateTime.UtcNow;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Search updated: '{query}'",
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
    /// Reset filters to default
    /// </summary>
    public async Task ResetFiltersAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _currentFilters = FilterState.CreateDefault();
            
            await MID_HelperFunctions.DebugMessageAsync(
                "🔄 Filters reset to default",
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