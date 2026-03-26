using SubashaVentures.Domain.Shop;

namespace SubashaVentures.Services.Shop;

/// <summary>
/// Lightweight pub/sub service that lets the ShopTop navigation bar
/// broadcast search-query changes to the Shop page without tight coupling.
/// </summary>
public class ShopStateService
{
    private readonly List<Func<string, Task>> _searchHandlers = new();

    // ==================== SEARCH ====================

    public event Func<string, Task>? OnSearchChanged
    {
        add    { if (value != null) _searchHandlers.Add(value); }
        remove { _searchHandlers.Remove(value!); }
    }

    public async Task NotifySearchChanged(string query)
    {
        foreach (var handler in _searchHandlers.ToList())
        {
            try   { await handler(query); }
            catch (Exception ex)
            {
                Console.WriteLine($"ShopStateService: search handler error — {ex.Message}");
            }
        }
    }

    // ==================== FILTER STATE CACHE ====================

    public FilterState? LastKnownFilters { get; private set; }

    public void SetLastKnownFilters(FilterState filters)
    {
        LastKnownFilters = filters.Clone();
    }
}
