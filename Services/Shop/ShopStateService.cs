// Services/Shop/ShopStateService.cs - CONFIRMED CORRECT
using SubashaVentures.Domain.Shop;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Shop;

public class ShopStateService
{
    private readonly IBlazorAppLocalStorageService _localStorage;
    private FilterState _currentFilters = FilterState.CreateDefault();
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    private const string FILTERS_STORAGE_KEY = "SubashaVentures_shop_filters";
    private bool _isInitialized = false;
    
    public event Func<string, Task>? OnSearchChanged;
    public event Func<FilterState, Task>? OnFiltersChanged;
    
    public ShopStateService(IBlazorAppLocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }
    
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        
        await _lock.WaitAsync();
        try
        {
            if (_isInitialized) return;
            
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
    
    public async Task UpdateFiltersAsync(FilterState filters)
    {
        await EnsureInitializedAsync();
        
        await _lock.WaitAsync();
        try
        {
            _currentFilters = filters.Clone();
            _currentFilters.LastUpdated = DateTime.UtcNow;
            
            await _localStorage.SetItemAsync(FILTERS_STORAGE_KEY, _currentFilters);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Filters updated & saved: [{string.Join(", ", _currentFilters.Categories)}]",
                LogLevel.Info
            );
            
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
    
    public async Task UpdateCategoriesAsync(List<string> categories)
    {
        await EnsureInitializedAsync();
        
        await _lock.WaitAsync();
        try
        {
            _currentFilters.Categories = new List<string>(categories ?? new List<string>());
            _currentFilters.LastUpdated = DateTime.UtcNow;
            
            await _localStorage.SetItemAsync(FILTERS_STORAGE_KEY, _currentFilters);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Categories updated & saved: [{string.Join(", ", _currentFilters.Categories)}]",
                LogLevel.Info
            );
            
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
    
    public async Task NotifySearchChanged(string query)
    {
        await EnsureInitializedAsync();
        
        await _lock.WaitAsync();
        try
        {
            _currentFilters.SearchQuery = query ?? "";
            _currentFilters.LastUpdated = DateTime.UtcNow;
            
            await _localStorage.SetItemAsync(FILTERS_STORAGE_KEY, _currentFilters);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Search updated & saved: '{query}'",
                LogLevel.Info
            );
            
            if (OnSearchChanged != null)
            {
                await OnSearchChanged.Invoke(query ?? "");
            }
            
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
    
    public async Task NotifyFiltersChanged(FilterState filters)
    {
        await UpdateFiltersAsync(filters);
    }
    
    public async Task ResetFiltersAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _currentFilters = FilterState.CreateDefault();
            
            await _localStorage.RemoveItemAsync(FILTERS_STORAGE_KEY);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "🔄 Filters reset & cleared from storage",
                LogLevel.Info
            );
            
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
    
    public bool HasActiveFilters()
    {
        return !_currentFilters.IsEmpty;
    }
}
