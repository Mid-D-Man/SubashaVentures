// Services/Shop/ShopStateService.cs
using SubashaVentures.Layout.Shop;
using SubashaVentures.Domain.Shop:
namespace SubashaVentures.Services.Shop;

public class ShopStateService
{
    public event Func<string, Task>? OnSearchChanged;
    public event Func<FilterState, Task>? OnFiltersChanged;

    public async Task NotifySearchChanged(string query)
    {
        if (OnSearchChanged != null)
        {
            await OnSearchChanged.Invoke(query);
        }
    }

    public async Task NotifyFiltersChanged(FilterState filters)
    {
        if (OnFiltersChanged != null)
        {
            await OnFiltersChanged.Invoke(filters);
        }
    }
}
