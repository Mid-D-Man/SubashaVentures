// Services/Shop/ShopStateService.cs
// Simplified — no longer acts as state store.
// Only used as an event bus for the search bar in ShopTop.
namespace SubashaVentures.Services.Shop;

public class ShopStateService
{
    public event Func<string, Task>? OnSearchChanged;

    public async Task NotifySearchChanged(string query)
    {
        if (OnSearchChanged != null)
            await OnSearchChanged.Invoke(query ?? "");
    }
}
