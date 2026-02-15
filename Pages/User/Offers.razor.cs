
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.VisualElements;


namespace SubashaVentures.Pages.User;


public partial class Offers : ComponentBase
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElementsService { get; set; } = default!;
    
    private bool isLoading = true;


    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (!VisualElementsService.IsInitialized)
            {
                await VisualElementsService.InitializeAsync();
            }
            
            isLoading = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing Offers page: {ex.Message}");
            isLoading = false;
        }
    }
}
