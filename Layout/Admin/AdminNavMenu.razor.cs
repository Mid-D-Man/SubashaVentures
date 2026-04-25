using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Services.VisualElements;

namespace SubashaVentures.Layout.Admin;

public partial class AdminNavMenu : LayoutComponentBase
{
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    public IVisualElementsService VisualElements { get; set; } = default!;

    [Inject]
    public AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private bool isCollapsed = false;
    private string adminName = "Admin";
    private string adminRole = "Administrator";
    private int pendingTemplateCount = 0;
    private int pendingPayoutCount = 0;

    // SVG fields
    private string dashSvg = string.Empty;
    private string analyticsSvg = string.Empty;
    private string orderSvg = string.Empty;
    private string productSvg = string.Empty;
    private string categorySvg = string.Empty;
    private string inventorySvg = string.Empty;
    private string partnerAppSvg = string.Empty;
    private string partnerTemplateSvg = string.Empty;
    private string partnerPayoutSvg = string.Empty;
    private string usersSvg = string.Empty;
    private string segmentSvg = string.Empty;
    private string newsletterSvg = string.Empty;
    private string offerSvg = string.Empty;
    private string bannerSvg = string.Empty;
    private string supportSvg = string.Empty;
    private string reviewSvg = string.Empty;
    private string adminUserSvg = string.Empty;
    private string settingsSvg = string.Empty;
    private string logsSvg = string.Empty;
    private string signOutSvg = string.Empty;
    private string collapseIconSvg = string.Empty;
    private string paymentSvg = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadSvgsAsync(), LoadAdminInfoAsync(), LoadBadgeCountsAsync());
    }

    private async Task LoadSvgsAsync()
    {
        try
        {
            dashSvg = await VisualElements.GetCustomSvgAsync(SvgType.Stats, 20, 20);
            analyticsSvg = await VisualElements.GetCustomSvgAsync(SvgType.Stats, 20, 20);
            orderSvg = await VisualElements.GetCustomSvgAsync(SvgType.Order, 20, 20);
            productSvg = await VisualElements.GetCustomSvgAsync(SvgType.AllProducts, 20, 20);
            categorySvg = await VisualElements.GetCustomSvgAsync(SvgType.Records, 20, 20);
            inventorySvg = await VisualElements.GetCustomSvgAsync(SvgType.AllProducts, 20, 20);
            usersSvg = await VisualElements.GetCustomSvgAsync(SvgType.User, 20, 20);
            newsletterSvg = await VisualElements.GetCustomSvgAsync(SvgType.Messages, 20, 20);
            offerSvg = await VisualElements.GetCustomSvgAsync(SvgType.Offer, 20, 20);
            supportSvg = await VisualElements.GetCustomSvgAsync(SvgType.HelpCenter, 20, 20);
            reviewSvg = await VisualElements.GetCustomSvgAsync(SvgType.Star, 20, 20);
            settingsSvg = await VisualElements.GetCustomSvgAsync(SvgType.Settings, 20, 20);
            adminUserSvg = await VisualElements.GetCustomSvgAsync(SvgType.User, 20, 20);

            // Inline SVGs without matching SvgType
            partnerAppSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round' d='M9 12l2 2 4-4M7.835 4.697a3.42 3.42 0 001.946-.806 3.42 3.42 0 014.438 0 3.42 3.42 0 001.946.806 3.42 3.42 0 013.138 3.138 3.42 3.42 0 00.806 1.946 3.42 3.42 0 010 4.438 3.42 3.42 0 00-.806 1.946 3.42 3.42 0 01-3.138 3.138 3.42 3.42 0 00-1.946.806 3.42 3.42 0 01-4.438 0 3.42 3.42 0 00-1.946-.806 3.42 3.42 0 01-3.138-3.138 3.42 3.42 0 00-.806-1.946 3.42 3.42 0 010-4.438 3.42 3.42 0 00.806-1.946 3.42 3.42 0 013.138-3.138z'/>",
                20, 20, "0 0 24 24", "fill='none'");

            partnerTemplateSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round' d='M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4'/>",
                20, 20, "0 0 24 24", "fill='none'");

            partnerPayoutSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round' d='M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z'/>",
                20, 20, "0 0 24 24", "fill='none'");

            segmentSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round' d='M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z'/>",
                20, 20, "0 0 24 24", "fill='none'");

            bannerSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round' d='M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z'/>",
                20, 20, "0 0 24 24", "fill='none'");

            logsSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round' d='M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z'/>",
                20, 20, "0 0 24 24", "fill='none'");

            signOutSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round' d='M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1'/>",
                20, 20, "0 0 24 24", "fill='none'");

            collapseIconSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M4 6h16M4 12h16M4 18h16'/>",
                20, 20, "0 0 24 24", "fill='none'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminNavMenu SVG load error: {ex.Message}");
        }
    }

    private async Task LoadAdminInfoAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            if (user.Identity?.IsAuthenticated != true) return;

            var first = user.FindFirst("first_name")?.Value ?? "";
            var last = user.FindFirst("last_name")?.Value ?? "";
            adminName = $"{first} {last}".Trim();
            if (string.IsNullOrEmpty(adminName)) adminName = "Admin";

            adminRole = user.FindFirst("admin_role")?.Value ?? "Administrator";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminNavMenu user info: {ex.Message}");
        }
    }

    private async Task LoadBadgeCountsAsync()
    {
        try
        {
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminNavMenu badge counts: {ex.Message}");
        }
    }

    private void ToggleCollapse()
    {
        isCollapsed = !isCollapsed;
        StateHasChanged();
    }
}
