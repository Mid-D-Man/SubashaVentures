// Pages/Main/OurStory.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SubashaVentures.Pages.Main;

public partial class OurStory : ComponentBase, IAsyncDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime         JS         { get; set; } = default!;

    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _jsInstance;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            _jsModule   = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./Pages/Main/OurStory.razor.js");
            _jsInstance = await _jsModule.InvokeAsync<IJSObjectReference>(
                "OurStory.create");
            await _jsInstance.InvokeVoidAsync("initialize");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OurStory JS init error: {ex.Message}");
        }
    }

    private void NavigateToShop() => Navigation.NavigateTo("shop");

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_jsInstance is not null)
            {
                await _jsInstance.InvokeVoidAsync("dispose");
                await _jsInstance.DisposeAsync();
            }
            if (_jsModule is not null)
                await _jsModule.DisposeAsync();
        }
        catch { /* suppress */ }
    }
}
