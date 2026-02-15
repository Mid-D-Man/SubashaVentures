using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Domain.Enums;

namespace SubashaVentures.Components.Shared.Cards;

public partial class FaqItem : ComponentBase
{
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public string Question { get; set; } = string.Empty;
    [Parameter] public string Answer { get; set; } = string.Empty;
    [Parameter] public SupportCategory Category { get; set; }
    [Parameter] public bool IsExpanded { get; set; } = false;
    [Parameter] public bool IsHighlighted { get; set; } = false;
    [Parameter] public List<FaqLink>? RelatedLinks { get; set; }
    [Parameter] public EventCallback<bool> OnToggle { get; set; }

    private ElementReference questionIconRef;
    private ElementReference expandIconRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadIconsAsync();
        }
        
        if (!firstRender)
        {
            await UpdateExpandIconAsync();
        }
    }

    private async Task LoadIconsAsync()
    {
        try
        {
            var questionIcon = GetCategoryIcon();
            var questionSvg = await VisualElements.GetCustomSvgAsync(
                questionIcon,
                width: 24,
                height: 24,
                fillColor: "var(--primary-color)"
            );

            var expandSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Close,
                width: 20,
                height: 20,
                fillColor: "var(--text-secondary)",
                transform: IsExpanded ? "rotate(0)" : "rotate(45)"
            );

            await JSRuntime.InvokeVoidAsync("eval", 
                $"document.querySelector('.faq-item .question-icon').innerHTML = `{questionSvg}`;");
            
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.faq-item .expand-icon').innerHTML = `{expandSvg}`;");
        }
        catch (Exception)
        {
            // Silent fail - icons are non-critical
        }
    }

    private async Task UpdateExpandIconAsync()
    {
        try
        {
            var expandSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Close,
                width: 20,
                height: 20,
                fillColor: "var(--text-secondary)",
                transform: IsExpanded ? "rotate(0)" : "rotate(45)"
            );

            await JSRuntime.InvokeVoidAsync("eval",
                $"if (document.querySelector('.faq-item.expanded .expand-icon, .faq-item:not(.expanded) .expand-icon')) {{ document.querySelector('.faq-item.expanded .expand-icon, .faq-item:not(.expanded) .expand-icon').innerHTML = `{expandSvg}`; }}");
        }
        catch (Exception)
        {
            // Silent fail
        }
    }

    private SvgType GetCategoryIcon()
    {
        return Category switch
        {
            SupportCategory.Orders => SvgType.Order,
            SupportCategory.Payments => SvgType.Payment,
            SupportCategory.Delivery => SvgType.TrackOrders,
            SupportCategory.Returns => SvgType.History,
            SupportCategory.Account => SvgType.User,
            _ => SvgType.HelpCenter
        };
    }

    private async Task ToggleExpand()
    {
        IsExpanded = !IsExpanded;
        
        if (OnToggle.HasDelegate)
        {
            await OnToggle.InvokeAsync(IsExpanded);
        }
        
        StateHasChanged();
        await UpdateExpandIconAsync();
    }
}

public enum SupportCategory
{
    General,
    Orders,
    Payments,
    Delivery,
    Returns,
    Account
}

public class FaqLink
{
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
