using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Components.Shared.Cards;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class Support : ComponentBase
{
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<Support> Logger { get; set; } = default!;

    private bool IsLoading = true;
    private string ActiveTab = "faqs";
    private SupportCategory SelectedCategory = SupportCategory.General;
    private string SearchQuery = string.Empty;
    private string? ExpandedFaqId = null;

    private List<FaqViewModel> AllFaqs = new();
    private List<FaqViewModel> FilteredFaqs = new();

    private ElementReference headerIconRef;
    private ElementReference faqTabIconRef;
    private ElementReference contactTabIconRef;
    private ElementReference searchIconRef;
    private ElementReference clearIconRef;
    private ElementReference noResultsIconRef;
    private ElementReference whatsappIconRef;
    private ElementReference whatsappArrowRef;
    private ElementReference emailIconRef;
    private ElementReference emailArrowRef;
    private ElementReference safetyIconRef;

    private const string WHATSAPP_NUMBER = "+2349007654321";
    private const string SUPPORT_EMAIL = "support@subashaventures.com";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (!await PermissionService.EnsureAuthenticatedAsync("user/support"))
            {
                Navigation.NavigateTo("signin", true);
                return;
            }

            await LoadFaqsAsync();
            FilteredFaqs = AllFaqs;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing support page");
            Logger.LogError(ex, "Failed to initialize support page");
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadIconsAsync();
        }
    }

    private async Task LoadIconsAsync()
    {
        try
        {
            var headerSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.HelpCenter,
                width: 48,
                height: 48,
                fillColor: "var(--primary-color)"
            );

            var faqTabSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.HelpCenter,
                width: 20,
                height: 20,
                fillColor: ActiveTab == "faqs" ? "var(--primary-color)" : "var(--text-secondary)"
            );

            var contactTabSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Mail,
                width: 20,
                height: 20,
                fillColor: ActiveTab == "contact" ? "var(--primary-color)" : "var(--text-secondary)"
            );

            var searchSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Search,
                width: 20,
                height: 20,
                fillColor: "var(--text-tertiary)"
            );

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelector('.header-icon')) document.querySelector('.header-icon').innerHTML = `{headerSvg}`;");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelectorAll('.tab-icon')[0]) document.querySelectorAll('.tab-icon')[0].innerHTML = `{faqTabSvg}`;");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelectorAll('.tab-icon')[1]) document.querySelectorAll('.tab-icon')[1].innerHTML = `{contactTabSvg}`;");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelector('.search-icon')) document.querySelector('.search-icon').innerHTML = `{searchSvg}`;");

            if (ActiveTab == "faqs")
            {
                await LoadCategoryIconsAsync();
            }
            else
            {
                await LoadContactIconsAsync();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading support icons");
        }
    }

    private async Task LoadCategoryIconsAsync()
    {
        try
        {
            foreach (var category in Enum.GetValues<SupportCategory>())
            {
                var iconType = GetCategoryIconType(category);
                var svg = await VisualElements.GetCustomSvgAsync(
                    iconType,
                    width: 18,
                    height: 18,
                    fillColor: SelectedCategory == category ? "var(--primary-color)" : "var(--text-secondary)"
                );

                await JSRuntime.InvokeVoidAsync("eval",
                    $"if(document.querySelector('.category-icon[data-category=\"{category}\"]')) document.querySelector('.category-icon[data-category=\"{category}\"]').innerHTML = `{svg}`;");
            }

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                var clearSvg = await VisualElements.GetCustomSvgAsync(
                    SvgType.Close,
                    width: 16,
                    height: 16,
                    fillColor: "var(--text-tertiary)"
                );

                await JSRuntime.InvokeVoidAsync("eval",
                    $"if(document.querySelector('.clear-icon')) document.querySelector('.clear-icon').innerHTML = `{clearSvg}`;");
            }

            if (!FilteredFaqs.Any() && !string.IsNullOrEmpty(SearchQuery))
            {
                var noResultsSvg = await VisualElements.GetCustomSvgAsync(
                    SvgType.Search,
                    width: 64,
                    height: 64,
                    fillColor: "var(--text-tertiary)"
                );

                await JSRuntime.InvokeVoidAsync("eval",
                    $"if(document.querySelector('.no-faqs-icon')) document.querySelector('.no-faqs-icon').innerHTML = `{noResultsSvg}`;");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading category icons");
        }
    }

    private async Task LoadContactIconsAsync()
    {
        try
        {
            var whatsappSvg = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413Z'/>",
                32, 32, "0 0 24 24"
            );

            var emailSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Mail,
                width: 32,
                height: 32,
                fillColor: "var(--info-color)"
            );

            var arrowSvg = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z'/>",
                24, 24, "0 0 24 24"
            );

            var safetySvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Warning,
                width: 48,
                height: 48,
                fillColor: "var(--warning-color)"
            );

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelector('.whatsapp .contact-card-icon')) document.querySelector('.whatsapp .contact-card-icon').innerHTML = `{whatsappSvg}`;");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelector('.email .contact-card-icon')) document.querySelector('.email .contact-card-icon').innerHTML = `{emailSvg}`;");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelectorAll('.contact-arrow')[0]) document.querySelectorAll('.contact-arrow')[0].innerHTML = `{arrowSvg}`;");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelectorAll('.contact-arrow')[1]) document.querySelectorAll('.contact-arrow')[1].innerHTML = `{arrowSvg}`;");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelector('.safety-icon')) document.querySelector('.safety-icon').innerHTML = `{safetySvg}`;");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading contact icons");
        }
    }

    private async Task LoadFaqsAsync()
    {
        AllFaqs = new List<FaqViewModel>
        {
            new() { Id = "1", Category = SupportCategory.General, Question = "How do I create an account?", Answer = "To create an account, click on the 'Sign Up' button at the top right corner of the page. Enter your details including your full name, email address, phone number, and create a secure password. You'll receive a verification code via email to confirm your account. Once verified, you can start shopping immediately." },
            new() { Id = "2", Category = SupportCategory.General, Question = "Is my personal information secure?", Answer = "Yes, we take your privacy and security very seriously. We use industry-standard encryption (SSL/TLS) to protect all data transmissions. Your personal information is stored securely and we never share it with third parties without your explicit consent. We comply with GDPR and NDPR data protection regulations." },
            new() { Id = "3", Category = SupportCategory.General, Question = "How do I reset my password?", Answer = "Click on 'Forgot Password' on the login page. Enter your registered email address or phone number. You'll receive a password reset code. Enter the code and create a new secure password. Make sure your new password is at least 8 characters long and includes uppercase, lowercase, and numbers." },
            
            new() { Id = "4", Category = SupportCategory.Orders, Question = "How can I track my order?", Answer = "Go to 'My Orders' section in your account dashboard. Click on any order to see detailed real-time tracking information including current location, estimated delivery date, and delivery status. You can also track using your order number and the tracking number provided via email." },
            new() { Id = "5", Category = SupportCategory.Orders, Question = "Can I cancel my order?", Answer = "Yes, you can cancel orders before they're shipped. Go to 'My Orders', select the order you want to cancel, and click 'Cancel Order'. If the order has already been shipped, you'll need to wait for delivery and then initiate a return. Refunds for cancelled orders are processed within 5-7 business days to your original payment method." },
            new() { Id = "6", Category = SupportCategory.Orders, Question = "What if I receive a damaged or wrong item?", Answer = "Contact our support team immediately with photos of the damaged or wrong item. We'll arrange for a replacement or full refund within 24 hours. You don't need to return the damaged item first. For wrong items, we'll send a courier to pick up the item and deliver the correct one at no extra cost." },
            new() { Id = "7", Category = SupportCategory.Orders, Question = "How long does order processing take?", Answer = "Orders are typically processed within 24 hours on business days. You'll receive a confirmation email once your order is confirmed, and another email when it ships with tracking information. Express processing is available for urgent orders." },
            
            new() { Id = "8", Category = SupportCategory.Payments, Question = "What payment methods do you accept?", Answer = "We accept multiple payment methods: Debit/Credit cards (Visa, Mastercard, Verve), bank transfers, USSD payments, mobile wallet payments (including our own wallet system), and pay-on-delivery for eligible orders. All online transactions are secured with bank-level encryption." },
            new() { Id = "9", Category = SupportCategory.Payments, Question = "When will I receive my refund?", Answer = "Refunds are processed within 5-7 business days to your original payment method. If you paid via card, the refund will appear in your account within 7-14 business days depending on your bank. Wallet refunds are instant. You'll receive an email confirmation once the refund is processed." },
            new() { Id = "10", Category = SupportCategory.Payments, Question = "Is it safe to save my card details?", Answer = "Yes, saved card details are encrypted and stored securely using PCI-DSS compliant systems. We never store your CVV or full card number. You can view, manage, and remove saved cards anytime from your payment settings. Two-factor authentication adds an extra layer of security." },
            new() { Id = "11", Category = SupportCategory.Payments, Question = "What is pay-on-delivery?", Answer = "Pay-on-delivery allows you to pay for your order in cash when it's delivered to you. This option is available for orders within certain delivery zones. A small convenience fee may apply. You can pay the exact amount to the delivery agent who will provide you with a receipt." },
            
            new() { Id = "12", Category = SupportCategory.Delivery, Question = "What are the delivery charges?", Answer = "Delivery charges vary by location: Lagos (within mainland) is 1,500 Naira, other major cities range from 2,000-4,000 Naira. Remote areas may have additional charges. Free delivery is available on all orders above 50,000 Naira. Express delivery options are available at premium rates." },
            new() { Id = "13", Category = SupportCategory.Delivery, Question = "How long does delivery take?", Answer = "Delivery times vary by location: Lagos - 1-3 business days, other major cities - 3-7 business days, remote areas - 5-10 business days. Express delivery is available in Lagos and Abuja for next-day delivery. You'll receive estimated delivery dates at checkout." },
            new() { Id = "14", Category = SupportCategory.Delivery, Question = "Can I change my delivery address?", Answer = "Yes, you can update your delivery address before the order ships. Go to your order details and click 'Edit Delivery Address'. If your order has already shipped, please contact support immediately. We may be able to redirect the shipment or arrange for address correction." },
            new() { Id = "15", Category = SupportCategory.Delivery, Question = "What if I'm not available during delivery?", Answer = "Our delivery agents will attempt to contact you via phone. If you're unavailable, they'll leave a notification and attempt delivery again the next business day. You can also arrange for someone else to receive the package on your behalf, or request to reschedule the delivery through our support team." },
            
            new() { Id = "16", Category = SupportCategory.Returns, Question = "What is your return policy?", Answer = "We offer a 7-day return policy for most items from the date of delivery. Products must be unused, in original packaging with all tags attached. Some items like underwear, cosmetics, and personalized products are non-returnable for hygiene reasons. Electronics must be returned with all accessories and original packaging." },
            new() { Id = "17", Category = SupportCategory.Returns, Question = "How do I initiate a return?", Answer = "Go to 'My Orders', select the item you want to return, click 'Return Item', choose the reason for return, and submit your request. Our team will review and approve eligible returns within 24 hours. We'll arrange for free pickup within 48 hours of approval." },
            new() { Id = "18", Category = SupportCategory.Returns, Question = "Who pays for return shipping?", Answer = "If the item is defective, damaged, or we sent the wrong item, we cover all return shipping costs. For change-of-mind returns, the customer is responsible for return shipping. However, if you use our pickup service, we offer discounted return rates." },
            new() { Id = "19", Category = SupportCategory.Returns, Question = "How long do refunds take after return?", Answer = "Once we receive and inspect your returned item, refunds are processed within 3-5 business days. You'll receive an email confirmation. The refund will appear in your original payment method within 7-14 business days, or instantly if you choose wallet credit." },
            
            new() { Id = "20", Category = SupportCategory.Account, Question = "How do I update my profile information?", Answer = "Go to Settings from your account menu. You can update your personal information, contact details, profile picture, and preferences. Changes to your email or phone number will require verification. Make sure to save changes before leaving the page." },
            new() { Id = "21", Category = SupportCategory.Account, Question = "Can I delete my account?", Answer = "Yes, you can delete your account from Settings > Account Security > Delete Account. Please note this action is permanent and cannot be undone. All your data including order history, saved addresses, and wishlists will be permanently deleted. You'll receive a confirmation email before final deletion." },
            new() { Id = "22", Category = SupportCategory.Account, Question = "What is two-factor authentication?", Answer = "Two-factor authentication (2FA) adds an extra layer of security to your account by requiring a verification code in addition to your password when signing in. You can enable it in Settings > Security. We support both SMS and authenticator app methods for 2FA." },
            new() { Id = "23", Category = SupportCategory.Account, Question = "How do I manage my saved addresses?", Answer = "Go to 'My Addresses' from your account menu. You can add new addresses, edit existing ones, set a default address, or delete addresses you no longer use. Having saved addresses makes checkout faster for future orders." }
        };

        await Task.CompletedTask;
    }

    private async Task SwitchTab(string tab)
    {
        if (ActiveTab == tab) return;

        ActiveTab = tab;
        StateHasChanged();

        await LoadIconsAsync();
    }

    private void SelectCategory(SupportCategory category)
    {
        SelectedCategory = category;
        SearchQuery = string.Empty;
        ApplyFilters();
        StateHasChanged();
        
        InvokeAsync(async () => await LoadCategoryIconsAsync());
    }

    private async Task HandleSearch()
    {
        ApplyFilters();
        StateHasChanged();
        await LoadCategoryIconsAsync();
    }

    private async Task ClearSearch()
    {
        SearchQuery = string.Empty;
        ApplyFilters();
        StateHasChanged();
        await LoadCategoryIconsAsync();
    }

    private void ApplyFilters()
    {
        var query = AllFaqs.AsEnumerable();

        if (SelectedCategory != SupportCategory.General || !string.IsNullOrEmpty(SearchQuery))
        {
            if (SelectedCategory != SupportCategory.General)
            {
                query = query.Where(f => f.Category == SelectedCategory);
            }

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                var searchLower = SearchQuery.ToLower();
                query = query.Where(f =>
                    f.Question.ToLower().Contains(searchLower) ||
                    f.Answer.ToLower().Contains(searchLower)
                );

                foreach (var faq in query)
                {
                    faq.IsHighlighted = true;
                }
            }
        }

        FilteredFaqs = query.ToList();
    }

    private void HandleFaqToggle(string faqId, bool isExpanded)
    {
        ExpandedFaqId = isExpanded ? faqId : null;
    }

    private SvgType GetCategoryIconType(SupportCategory category)
    {
        return category switch
        {
            SupportCategory.Orders => SvgType.Order,
            SupportCategory.Payments => SvgType.Payment,
            SupportCategory.Delivery => SvgType.TrackOrders,
            SupportCategory.Returns => SvgType.History,
            SupportCategory.Account => SvgType.User,
            _ => SvgType.HelpCenter
        };
    }

    private string GetCategoryName(SupportCategory category)
    {
        return category switch
        {
            SupportCategory.General => "All FAQs",
            SupportCategory.Orders => "Orders",
            SupportCategory.Payments => "Payments",
            SupportCategory.Delivery => "Delivery",
            SupportCategory.Returns => "Returns & Refunds",
            SupportCategory.Account => "Account",
            _ => "General"
        };
    }

    private int GetCategoryCount(SupportCategory category)
    {
        if (category == SupportCategory.General)
        {
            return AllFaqs.Count;
        }
        return AllFaqs.Count(f => f.Category == category);
    }

    private async Task OpenWhatsApp()
    {
        try
        {
            var message = Uri.EscapeDataString("Hello, I need support with SubashaVentures.");
            var url = $"https://wa.me/{WHATSAPP_NUMBER.Replace("+", "")}?text={message}";
            await JSRuntime.InvokeVoidAsync("open", url, "_blank");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Opening WhatsApp");
        }
    }

    private async Task OpenEmail()
    {
        try
        {
            var subject = Uri.EscapeDataString("Support Request - SubashaVentures");
            var body = Uri.EscapeDataString("Hello Support Team,\n\nI need assistance with:\n\n");
            var url = $"mailto:{SUPPORT_EMAIL}?subject={subject}&body={body}";
            await JSRuntime.InvokeVoidAsync("open", url, "_self");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Opening email");
        }
    }
}

public class FaqViewModel
{
    public string Id { get; set; } = string.Empty;
    public SupportCategory Category { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public bool IsHighlighted { get; set; } = false;
    public List<FaqLink>? RelatedLinks { get; set; }
}
