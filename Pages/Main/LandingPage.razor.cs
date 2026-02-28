// Pages/Main/LandingPage.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Services.Newsletter;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Services.AppStats;

namespace SubashaVentures.Pages.Main;

public partial class LandingPage : ComponentBase, IAsyncDisposable
{
    // ── Injected services ─────────────────────────────────────────────────────
    [Inject] private IProductOfTheDayService ProductOfTheDayService { get; set; } = default!;
    [Inject] private IProductService         ProductService         { get; set; } = default!;
    [Inject] private IVisualElementsService  VisualElements         { get; set; } = default!;
    [Inject] private INewsletterService      NewsletterService      { get; set; } = default!;
    [Inject] private IReviewService          ReviewService          { get; set; } = default!;
    [Inject] private IAppStatsService        AppStatsService        { get; set; } = default!;
    [Inject] private NavigationManager       Navigation             { get; set; } = default!;
    [Inject] private IJSRuntime              JS                     { get; set; } = default!;

    // ═════════════════════════════════════════════════════════════════════════
    // DISPLAY CONFIGURATION — flip these to switch data sources
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Show the stats row at all.</summary>
    private const bool ShowStats = true;

    /// <summary>Use hardcoded numbers instead of the app_stats table.</summary>
    private const bool UseFakeStats = true;

    /// <summary>Use the hardcoded Nigerian review list instead of Firestore.</summary>
    private const bool UseFakeReviews = true;

    // ── Fake stat raw values (only used when UseFakeStats = true) ─────────────
    private const long FakeCustomerCount   = 10_000;
    private const long FakeProductCount    =  5_200;
    private const long FakeOrdersDelivered =  8_400;

    // ── Stat labels ───────────────────────────────────────────────────────────
    private const string CustomersLabel = "Happy Customers";
    private const string ProductsLabel  = "Products";
    private const string OrdersLabel    = "Orders Delivered";

    // ── Animated stat display values (computed in LoadStats) ──────────────────
    private int    _statCustomersTarget;
    private string _statCustomersSuffix = "K+";
    private int    _statProductsTarget;
    private string _statProductsSuffix  = "K+";
    private int    _statOrdersTarget;
    private string _statOrdersSuffix    = "K+";

    // ─────────────────────────────────────────────────────────────────────────
    // Footer / contact variables — edit these without touching markup
    // ─────────────────────────────────────────────────────────────────────────
    // (These are referenced only in the footer component, kept here as the
    //  single source of truth if you ever wire them up via a config service.)
    private const string ContactPhone   = "+234 81 999 999 99";
    private const string ContactEmail   = "subashaventures@gmail.com";
    private const string ContactAddress = "Everywhere you go";

    private const string SocialFacebook  = "#";
    private const string SocialInstagram = "#";
    private const string SocialTwitter   = "#";
    private const string SocialLinkedIn  = "#";

    private const string LegalPrivacyUrl = "privacy";
    private const string LegalTermsUrl   = "terms";
    private const string LegalCookiesUrl = "cookies";

    // ═════════════════════════════════════════════════════════════════════════
    // Component state
    // ═════════════════════════════════════════════════════════════════════════

    private ProductViewModel?       productOfTheDay;
    private List<ProductViewModel>  featuredProducts = new();
    private List<ReviewViewModel>   sampleReviews    = new();

    private bool isLoadingPOTD     = true;
    private bool isLoadingFeatured = true;

    // Newsletter
    private string newsletterEmail        = string.Empty;
    private bool   isSubscribing;
    private bool   newsletterSubscribed;
    private bool   newsletterHasError;
    private string newsletterErrorMessage = string.Empty;

    // SVGs
    private string shopNowIcon     = string.Empty;
    private string storyIcon       = string.Empty;
    private string allProductsIcon = string.Empty;
    private string checkIcon       = string.Empty;
    private string warningIcon     = string.Empty;
    private string dressIcon       = string.Empty;

    // JS
    private IJSObjectReference? jsModule;
    private IJSObjectReference? landingPageInstance;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await LoadButtonIcons();

        await Task.WhenAll(
            LoadProductOfTheDay(),
            LoadFeaturedProducts(),
            LoadStats(),
            LoadReviews()
        );
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            jsModule = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./Pages/Main/LandingPage.razor.js");

            landingPageInstance = await jsModule.InvokeAsync<IJSObjectReference>(
                "LandingPage.create");

            await landingPageInstance.InvokeVoidAsync("initialize");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initialising LandingPage JS: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Icons
    // ─────────────────────────────────────────────────────────────────────────

    private async Task LoadButtonIcons()
    {
        try
        {
            shopNowIcon     = await VisualElements.GetSvgWithColorAsync(SvgType.ShopNow,    20, 20, "currentColor");
            storyIcon       = await VisualElements.GetSvgWithColorAsync(SvgType.Story,      20, 20, "currentColor");
            allProductsIcon = await VisualElements.GetSvgWithColorAsync(SvgType.AllProducts,20, 20, "currentColor");
            checkIcon       = await VisualElements.GetSvgWithColorAsync(SvgType.CheckMark,  22, 22, "var(--success-color, #10b981)");
            warningIcon     = await VisualElements.GetSvgWithColorAsync(SvgType.Warning,    14, 14, "var(--danger-color, #ef4444)");
            dressIcon       = await VisualElements.GetSvgWithColorAsync(SvgType.Dress,      64, 64, "var(--primary-color)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading landing page icons: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stats
    // ─────────────────────────────────────────────────────────────────────────

    private async Task LoadStats()
    {
        if (!ShowStats) return;

        long customers, products, orders;

        if (UseFakeStats)
        {
            customers = FakeCustomerCount;
            products  = FakeProductCount;
            orders    = FakeOrdersDelivered;
        }
        else
        {
            var snap = await AppStatsService.GetStatsAsync();
            customers = snap.TotalCustomers;
            products  = snap.TotalProducts;
            orders    = snap.OrdersDelivered;
        }

        (_statCustomersTarget, _statCustomersSuffix) = FormatStatForAnimation(customers);
        (_statProductsTarget,  _statProductsSuffix)  = FormatStatForAnimation(products);
        (_statOrdersTarget,    _statOrdersSuffix)     = FormatStatForAnimation(orders);
    }

    /// <summary>
    /// Converts a raw long into the display integer + suffix that JS will animate to.
    /// e.g. 10 000 → (10, "K+")   |   5 200 000 → (5, "M+")   |   342 → (342, "+")
    /// </summary>
    private static (int target, string suffix) FormatStatForAnimation(long value)
    {
        if (value >= 1_000_000) return ((int)(value / 1_000_000), "M+");
        if (value >= 1_000)     return ((int)(value / 1_000),     "K+");
        return ((int)value, "+");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Products
    // ─────────────────────────────────────────────────────────────────────────

    private async Task LoadProductOfTheDay()
    {
        try
        {
            isLoadingPOTD   = true;
            productOfTheDay = await ProductOfTheDayService.GetProductOfTheDayAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading Product of the Day: {ex.Message}");
        }
        finally
        {
            isLoadingPOTD = false;
            StateHasChanged();
        }
    }

    private async Task LoadFeaturedProducts()
    {
        try
        {
            isLoadingFeatured = true;
            var allProducts   = await ProductService.GetProductsAsync(0, 100);
            var random        = new Random();

            var available = allProducts
                .Where(p => p.IsFeatured && p.IsActive && p.Stock > 0)
                .ToList();

            featuredProducts = available.Any()
                ? available.OrderBy(_ => random.Next()).Take(6).ToList()
                : allProducts.Where(p => p.IsActive && p.Stock > 0)
                             .OrderBy(_ => random.Next()).Take(6).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading featured products: {ex.Message}");
            featuredProducts = new List<ProductViewModel>();
        }
        finally
        {
            isLoadingFeatured = false;
            StateHasChanged();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Reviews
    // ─────────────────────────────────────────────────────────────────────────

    private async Task LoadReviews()
    {
        if (UseFakeReviews)
        {
            sampleReviews = GetFakeReviews();
            return;
        }

        try
        {
            var approved = await ReviewService.GetApprovedReviewsAsync();

            sampleReviews = approved
                .Take(10)
                .Select(r => new ReviewViewModel
                {
                    Id                 = r.Id,
                    UserName           = r.UserName,
                    UserAvatar         = r.UserAvatar,
                    Rating             = r.Rating,
                    Title              = r.Title,
                    Comment            = r.Comment,
                    Images             = r.Images,
                    IsVerifiedPurchase = r.IsVerifiedPurchase,
                    HelpfulCount       = r.HelpfulCount,
                    CreatedAt          = r.CreatedAt
                })
                .ToList();

            // Fall back to fake if no approved reviews yet
            if (!sampleReviews.Any())
                sampleReviews = GetFakeReviews();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading reviews: {ex.Message}");
            sampleReviews = GetFakeReviews();
        }
    }

    private static List<ReviewViewModel> GetFakeReviews() => new()
    {
        new ReviewViewModel
        {
            Id = "r1", UserName = "Adaeze Okonkwo", Rating = 5,
            Title   = "Absolutely love it!",
            Comment = "I ordered two dresses and they both fit like they were made just for me. " +
                      "The fabric quality is incredible and delivery to Lagos was faster than expected. " +
                      "Will definitely be ordering again!",
            IsVerifiedPurchase = true, HelpfulCount = 47,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        },
        new ReviewViewModel
        {
            Id = "r2", UserName = "Emeka Nwosu", Rating = 5,
            Title   = "Best online shopping in Nigeria!",
            Comment = "I've tried many Nigerian fashion stores online but Subasha is on another level entirely. " +
                      "The packaging was neat, the shirt quality is premium, and customer service is very responsive. 10/10!",
            IsVerifiedPurchase = true, HelpfulCount = 32,
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        },
        new ReviewViewModel
        {
            Id = "r3", UserName = "Fatima Abubakar", Rating = 5,
            Title   = "Kano delivery was so smooth!",
            Comment = "I was skeptical about ordering to Kano but they delivered within 3 days. " +
                      "The hijab collection is beautiful and the colours match the website exactly. Very happy customer!",
            IsVerifiedPurchase = true, HelpfulCount = 28,
            CreatedAt = DateTime.UtcNow.AddDays(-12)
        },
        new ReviewViewModel
        {
            Id = "r4", UserName = "Chidi Eze", Rating = 5,
            Title   = "Home decor is fire!",
            Comment = "Bought throw pillows and a table runner for my living room in Port Harcourt. " +
                      "My whole family keeps asking where I got them. Quality is top notch and the price is very reasonable.",
            IsVerifiedPurchase = true, HelpfulCount = 41,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        },
        new ReviewViewModel
        {
            Id = "r5", UserName = "Blessing Okafor", Rating = 5,
            Title   = "Children's clothes are amazing quality",
            Comment = "Found the perfect outfits for my twins' birthday. Materials are soft, stitching is strong, " +
                      "and they've washed several times without fading. Subasha has a loyal customer for life in me!",
            IsVerifiedPurchase = true, HelpfulCount = 56,
            CreatedAt = DateTime.UtcNow.AddDays(-9)
        },
        new ReviewViewModel
        {
            Id = "r6", UserName = "Tunde Bakare", Rating = 5,
            Title   = "Legit quality for the price",
            Comment = "Honestly wasn't expecting this level of quality at this price point. " +
                      "The ankara outfit is structured perfectly and the vendor clearly knows what they're doing. Big ups Subasha!",
            IsVerifiedPurchase = true, HelpfulCount = 39,
            CreatedAt = DateTime.UtcNow.AddDays(-14)
        },
        new ReviewViewModel
        {
            Id = "r7", UserName = "Ngozi Dike", Rating = 5,
            Title   = "Impressed from Owerri!",
            Comment = "My sister recommended Subasha and I'm so glad she did. Bought a complete bedsheet set " +
                      "and the material is so soft and cool. I sleep better now honestly. Delivery to Owerri was fast too!",
            IsVerifiedPurchase = true, HelpfulCount = 23,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        },
        new ReviewViewModel
        {
            Id = "r8", UserName = "Ibrahim Musa", Rating = 5,
            Title   = "Northern Nigeria approved!",
            Comment = "Ordered native attire for Eid celebrations. The quality was exactly what I hoped for " +
                      "and my family loved it. Subasha deserves all the recognition they're getting. Will order more insha Allah.",
            IsVerifiedPurchase = true, HelpfulCount = 34,
            CreatedAt = DateTime.UtcNow.AddDays(-16)
        },
        new ReviewViewModel
        {
            Id = "r9", UserName = "Amaka Obiora", Rating = 5,
            Title   = "Excellent returns process!",
            Comment = "One item had a slight sizing issue but customer service sorted it out in 24 hours. " +
                      "That level of professionalism is rare in Nigerian e-commerce. The replacement fits beautifully!",
            IsVerifiedPurchase = true, HelpfulCount = 61,
            CreatedAt = DateTime.UtcNow.AddDays(-11)
        },
        new ReviewViewModel
        {
            Id = "r10", UserName = "Seun Adeyemi", Rating = 5,
            Title   = "Long-distance gifting sorted!",
            Comment = "Ordered a birthday gift from Ibadan to be delivered to my friend in Abuja. " +
                      "It arrived on time, beautifully packaged, and my friend was over the moon. Subasha makes gifting so easy!",
            IsVerifiedPurchase = true, HelpfulCount = 44,
            CreatedAt = DateTime.UtcNow.AddDays(-6)
        },
        new ReviewViewModel
        {
            Id = "r11", UserName = "Mei Xiaoli", Rating = 5,
            Title   = "Beautiful African fashion!",
            Comment = "Discovered Subasha through a Nigerian friend. The Ankara fabrics and traditional designs " +
                      "are so beautiful and authentic. Ordered for international delivery and everything arrived perfectly packaged.",
            IsVerifiedPurchase = false, HelpfulCount = 18,
            CreatedAt = DateTime.UtcNow.AddDays(-25)
        }
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Navigation
    // ─────────────────────────────────────────────────────────────────────────

    private void NavigateToShop()     => Navigation.NavigateTo("shop");
    private void NavigateToOurStory() => Navigation.NavigateTo("our-story");

    private void HandlePOTDClick(ProductViewModel p)       => Navigation.NavigateTo($"product/{p.Slug}");
    private void HandleViewPOTDDetails(ProductViewModel p)  => Navigation.NavigateTo($"product/{p.Slug}");
    private void HandleProductClick(ProductViewModel p)     => Navigation.NavigateTo($"product/{p.Slug}");
    private void HandleViewDetails(ProductViewModel p)      => Navigation.NavigateTo($"product/{p.Slug}");

    private Task HandleAddToCart(ProductViewModel _)          => Task.CompletedTask;
    private Task HandleHelpfulClick(ReviewViewModel _)        => Task.CompletedTask;
    private Task HandleReviewImageClick(string _)             => Task.CompletedTask;

    // ─────────────────────────────────────────────────────────────────────────
    // Newsletter
    // ─────────────────────────────────────────────────────────────────────────

    private async Task HandleNewsletterSubmit()
    {
        if (string.IsNullOrWhiteSpace(newsletterEmail) || isSubscribing) return;

        ClearNewsletterError();
        isSubscribing = true;
        StateHasChanged();

        try
        {
            var result = await NewsletterService.SubscribeAsync(
                newsletterEmail.Trim(), "landing_page");

            switch (result)
            {
                case NewsletterSubscribeResult.Success:
                case NewsletterSubscribeResult.AlreadySubscribed:
                    newsletterSubscribed = true;
                    newsletterEmail      = string.Empty;
                    break;

                case NewsletterSubscribeResult.InvalidEmail:
                    SetNewsletterError("Please enter a valid email address.");
                    break;

                default:
                    SetNewsletterError("Something went wrong. Please try again shortly.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Newsletter error: {ex.Message}");
            SetNewsletterError("Something went wrong. Please try again shortly.");
        }
        finally
        {
            isSubscribing = false;
            StateHasChanged();
        }
    }

    private void HandleNewsletterKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(newsletterEmail) && !isSubscribing)
            _ = HandleNewsletterSubmit();
    }

    private void SetNewsletterError(string msg)  { newsletterHasError = true;  newsletterErrorMessage = msg; }
    private void ClearNewsletterError()          { newsletterHasError = false; newsletterErrorMessage = string.Empty; }

    // ─────────────────────────────────────────────────────────────────────────
    // Disposal
    // ─────────────────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (landingPageInstance is not null)
            {
                await landingPageInstance.InvokeVoidAsync("dispose");
                await landingPageInstance.DisposeAsync();
            }
            if (jsModule is not null)
                await jsModule.DisposeAsync();
        }
        catch { /* suppress disposal errors */ }
    }
}
