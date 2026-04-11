using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Services.Partners;

namespace SubashaVentures.Pages.User.Partner;

public partial class PartnerDashboard : ComponentBase
{
    [Inject] private IPartnerStoreService PartnerStoreService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool isLoading = true;
    private string userId = string.Empty;
    private string partnerId = string.Empty;
    private PartnerDashboardViewModel? dashboard = null;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                Navigation.NavigateTo("signin");
                return;
            }

            userId = user.FindFirst("sub")?.Value
                  ?? user.FindFirst("id")?.Value
                  ?? string.Empty;

            // Partner ID comes from claims set by approve-partner-application edge fn
            partnerId = user.FindFirst("partner_id")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(userId))
            {
                Navigation.NavigateTo("signin");
                return;
            }

            // If no partner_id claim yet check if they are a partner at all
            // The claim might not be refreshed yet so we check the user metadata
            var isPartner = user.FindFirst("is_partner")?.Value == "true";

            if (!isPartner && string.IsNullOrEmpty(partnerId))
            {
                // Not a partner — redirect to apply
                Navigation.NavigateTo("user/partner/apply");
                return;
            }

            await LoadDashboard();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerDashboard init error: {ex.Message}");
            isLoading = false;
        }
    }

    private async Task LoadDashboard()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            dashboard = await PartnerStoreService.GetDashboardAsync(partnerId, userId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerDashboard load error: {ex.Message}");
            dashboard = null;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}
