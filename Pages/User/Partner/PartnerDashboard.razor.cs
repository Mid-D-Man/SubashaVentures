using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Services.Partners;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.VisualElements;

namespace SubashaVentures.Pages.User.Partner;

public partial class PartnerDashboard : ComponentBase
{
    [Inject] private IPartnerStoreService        PartnerStoreService { get; set; } = default!;
    [Inject] private IUserService                UserService         { get; set; } = default!;
    [Inject] private IVisualElementsService      VisualElements      { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider   { get; set; } = default!;
    [Inject] private NavigationManager           Navigation          { get; set; } = default!;

    private bool   isLoading  = true;
    private string userId     = string.Empty;
    private string partnerId  = string.Empty;
    private PartnerDashboardViewModel? dashboard = null;

    private string productsIconSvg     = string.Empty;
    private string templatesIconSvg    = string.Empty;
    private string payoutIconSvg       = string.Empty;
    private string revenueIconSvg      = string.Empty;
    private string addIconSvg          = string.Empty;
    private string storeIconSvg        = string.Empty;
    private string financialsIconSvg   = string.Empty;
    private string linkIconSvg         = string.Empty;
    private string thumbPlaceholderSvg = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user      = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                Navigation.NavigateTo("signin");
                return;
            }

            userId = user.FindFirst("sub")?.Value
                  ?? user.FindFirst("id")?.Value
                  ?? string.Empty;

            if (string.IsNullOrEmpty(userId))
            {
                Navigation.NavigateTo("signin");
                return;
            }

            // ── JWT claims first ───────────────────────────────────────────────
            var isPartner = user.FindFirst("is_partner")?.Value == "true";
            var claimPid  = user.FindFirst("partner_id")?.Value ?? string.Empty;

            if (!string.IsNullOrEmpty(claimPid))
            {
                isPartner = true;
                partnerId = claimPid;
            }

            // ── DB fallback (JWT stale after approval) ─────────────────────────
            if (!isPartner || string.IsNullOrEmpty(partnerId))
            {
                try
                {
                    var dbProfile = await UserService.GetUserByIdAsync(userId);
                    if (dbProfile?.IsPartner == true)
                    {
                        isPartner = true;
                        if (string.IsNullOrEmpty(partnerId))
                            partnerId = dbProfile.PartnerId ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PartnerDashboard DB partner check: {ex.Message}");
                }
            }

            if (!isPartner)
            {
                Navigation.NavigateTo("user/partner/apply");
                return;
            }

            if (string.IsNullOrEmpty(partnerId))
            {
                // Still no partnerId — try once more directly
                try
                {
                    var dbProfile = await UserService.GetUserByIdAsync(userId);
                    partnerId = dbProfile?.PartnerId ?? string.Empty;
                }
                catch { /* non-fatal */ }
            }

            await Task.WhenAll(LoadIconsAsync(), LoadDashboard());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerDashboard init error: {ex.Message}");
            isLoading = false;
        }
    }

    private async Task LoadIconsAsync()
    {
        try
        {
            productsIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.AllProducts, width: 28, height: 28, fillColor: "currentColor");

            templatesIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Records, width: 28, height: 28, fillColor: "currentColor");

            payoutIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Payment, width: 28, height: 28, fillColor: "currentColor");

            revenueIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Stats, width: 28, height: 28, fillColor: "currentColor");

            storeIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.ShopNow, width: 28, height: 28, fillColor: "currentColor");

            financialsIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Payment, width: 28, height: 28, fillColor: "currentColor");

            thumbPlaceholderSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.AllProducts, width: 24, height: 24, fillColor: "currentColor");

            addIconSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='2' stroke-linecap='round' d='M12 5v14M5 12h14'/>",
                28, 28, "0 0 24 24", "fill='none'");

            linkIconSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' " +
                "d='M18 13v6a2 2 0 01-2 2H5a2 2 0 01-2-2V8a2 2 0 012-2h6M15 3h6v6M10 14L21 3'/>",
                28, 28, "0 0 24 24", "fill='none'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerDashboard icon error: {ex.Message}");
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
