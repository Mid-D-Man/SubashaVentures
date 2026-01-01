// Services/Shop/IShopFilterService.cs
using SubashaVentures.Domain.Shop;

namespace SubashaVentures.Services.Shop;

/// <summary>
/// Service for managing shop filter state with local storage persistence
/// </summary>
public interface IShopFilterService
{
    /// <summary>
    /// Get the current filter state (from memory or storage)
    /// </summary>
    Task<FilterState> GetCurrentFiltersAsync();
    
    /// <summary>
    /// Save filter state to local storage
    /// </summary>
    Task SaveFiltersAsync(FilterState filters);
    
    /// <summary>
    /// Update only the categories in the current filter state
    /// </summary>
    Task UpdateCategoriesAsync(List<string> categories);
    
    /// <summary>
    /// Update search query in the current filter state
    /// </summary>
    Task UpdateSearchQueryAsync(string query);
    
    /// <summary>
    /// Update sort option in the current filter state
    /// </summary>
    Task UpdateSortAsync(string sortBy);
    
    /// <summary>
    /// Reset filters to default values
    /// </summary>
    Task ResetFiltersAsync();
    
    /// <summary>
    /// Check if there are any active filters
    /// </summary>
    Task<bool> HasActiveFiltersAsync();
    
    /// <summary>
    /// Clear all saved filters from storage
    /// </summary>
    Task ClearStorageAsync();
    
    /// <summary>
    /// Event triggered when filters change
    /// </summary>
    event EventHandler<FilterState>? OnFiltersChanged;
}
