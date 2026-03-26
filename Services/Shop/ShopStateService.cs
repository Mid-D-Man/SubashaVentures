namespace SubashaVentures.Services.Shop;

/// <summary>
/// Lightweight pub/sub service that lets the ShopTop navigation bar
/// broadcast search-query changes to the Shop page without tight coupling.
/// </summary>
public class ShopStateService
{
    // Supports multiple async subscribers (e.g. Shop page + future analytics)
    private readonly List<Func<string, Task>> _searchHandlers = new();

    // ==================== SEARCH ====================

    /// <summary>Subscribe to search-query changes.</summary>
    public event Func<string, Task>? OnSearchChanged
    {
        add    { if (value != null) _searchHandlers.Add(value); }
        remove { _searchHandlers.Remove(value!); }
    }

    /// <summary>
    /// Broadcast a new search query to all subscribers in registration order.
    /// Fire-and-forget exceptions are swallowed per-handler so one bad
    /// subscriber does not block the rest.
    /// </summary>
    public async Task NotifySearchChanged(string query)
    {
        foreach (var handler in _searchHandlers.ToList())
        {
            try
            {
                await handler(query);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShopStateService: search handler error — {ex.Message}");
            }
        }
    }

    // ==================== FILTER STATE CACHE ====================

    /// <summary>
    /// Optional: last-known filter state so layout components can
    /// read current filters without needing a direct reference to Shop.
    /// Set by ShopLayout when it pushes a URL change.
    /// </summary>
    public FilterState? LastKnownFilters { get; private set; }

    public void SetLastKnownFilters(FilterState filters)
    {
        LastKnownFilters = filters.Clone();
    }
}
