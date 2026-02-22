// Pages/Main/LandingPage.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Services.Newsletter;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.VisualElements;

namespace SubashaVentures.Pages.Main;

public partial class LandingPage : ComponentBase, IAsyncDisposable
{
    [Inject] private IProductOfTheDayService ProductOfTheDayService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private INewsletterService NewsletterService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Product state ─────────────────────────────────────────────────────────
    private ProductViewModel? productOfTheDay;
    private List<ProductViewModel> featuredProducts = new();
    private List<ReviewViewModel> sampleReviews = new();
    private bool isLoadingPOTD = true;
    private bool isLoadingFeatured = true;

    // ── Newsletter state ──────────────────────────────────────────────────────
    private string newsletterEmail = string.Empty;
    private bool isSubscribing;
    private bool newsletterSubscribed;
    private bool newsletterHasError;
    private string newsletterErrorMessage = string.Empty;

    // ── SVGs ──────────────────────────────────────────────────────────────────
    private string shopNowIcon = string.Empty;
    private string storyIcon = string.Empty;
    private string allProductsIcon = string.Empty;
    private string checkIcon = string.Empty;
    private string warningIcon = string.Empty;
    private string dressIcon = string.Empty;

    // ── JS ────────────────────────────────────────────────────────────────────
    private IJSObjectReference? jsModule;
    private IJSObjectReference? landingPageInstance;

    // ─────────────────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await LoadButtonIcons();
        await LoadProductOfTheDay();
        await LoadFeaturedProducts();
        LoadSampleReviews();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                jsModule = await JS.InvokeAsync<IJSObjectReference>(
                    "import",
                    "./Pages/Main/LandingPage.razor.js");

                landingPageInstance = await jsModule.InvokeAsync<IJSObjectReference>(
                    "LandingPage.create");

                await landingPageInstance.InvokeVoidAsync("initialize");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing LandingPage JS: {ex.Message}");
            }
        }
    }

    // ── Icons ─────────────────────────────────────────────────────────────────

    private async Task LoadButtonIcons()
    {
        try
        {
            shopNowIcon     = await VisualElements.GetSvgWithColorAsync(SvgType.ShopNow,   20, 20, "currentColor");
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

    // ── Products ──────────────────────────────────────────────────────────────

    private async Task LoadProductOfTheDay()
    {
        try
        {
            isLoadingPOTD = true;
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

            var allProducts = await ProductService.GetProductsAsync(0, 100);

            var availableFeatured = allProducts
                .Where(p => p.IsFeatured && p.IsActive && p.Stock > 0)
                .ToList();

            var random = new Random();

            featuredProducts = availableFeatured.Any()
                ? availableFeatured.OrderBy(_ => random.Next()).Take(6).ToList()
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

    private void LoadSampleReviews()
    {
        sampleReviews = new List<ReviewViewModel>
        {
            new ReviewViewModel
            {
                Id = "1",
                UserName = "Sarah Johnson",
                Rating = 5,
                Title = "Amazing Quality!",
                Comment = "The clothes fit perfectly and the style is exactly what I was looking for. Fast shipping and excellent customer service!",
                IsVerifiedPurchase = true,
                HelpfulCount = 24,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new ReviewViewModel
            {
                Id = "2",
                UserName = "Mike Chen",
                Rating = 5,
                Title = "Love the home decor!",
                Comment = "Found the perfect pieces to complete my living room makeover. The quality exceeded my expectations.",
                IsVerifiedPurchase = true,
                HelpfulCount = 18,
                CreatedAt = DateTime.UtcNow.AddDays(-12)
            },
            new ReviewViewModel
            {
                Id = "3",
                UserName = "Emma Davis",
                Rating = 5,
                Title = "Perfect for Kids",
                Comment = "Great selection for kids clothes. My daughter loves her new outfits and they are so comfortable!",
                IsVerifiedPurchase = true,
                HelpfulCount = 31,
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            }
        };
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void NavigateToShop() => Navigation.NavigateTo("shop");

    private void HandlePOTDClick(ProductViewModel product)      => Navigation.NavigateTo($"product/{product.Slug}");
    private void HandleViewPOTDDetails(ProductViewModel product) => Navigation.NavigateTo($"product/{product.Slug}");
    private void HandleProductClick(ProductViewModel product)    => Navigation.NavigateTo($"product/{product.Slug}");
    private void HandleViewDetails(ProductViewModel product)     => Navigation.NavigateTo($"product/{product.Slug}");

    private async Task HandleAddToCart(ProductViewModel product) => await Task.CompletedTask;
    private async Task HandleHelpfulClick(ReviewViewModel review) => await Task.CompletedTask;
    private async Task HandleReviewImageClick(string imageUrl)    => await Task.CompletedTask;

    // ── Newsletter ────────────────────────────────────────────────────────────

    private async Task HandleNewsletterSubmit()
    {
        if (string.IsNullOrWhiteSpace(newsletterEmail) || isSubscribing)
            return;

        ClearNewsletterError();
        isSubscribing = true;
        StateHasChanged();

        try
        {
            var result = await NewsletterService.SubscribeAsync(
                newsletterEmail.Trim(),
                "landing_page");

            switch (result)
            {
                case NewsletterSubscribeResult.Success:
                    newsletterSubscribed = true;
                    newsletterEmail = string.Empty;
                    break;

                case NewsletterSubscribeResult.AlreadySubscribed:
                    // Treat as success — no need to alarm the user
                    newsletterSubscribed = true;
                    newsletterEmail = string.Empty;
                    break;

                case NewsletterSubscribeResult.InvalidEmail:
                    SetNewsletterError("Please enter a valid email address.");
                    break;

                case NewsletterSubscribeResult.Failed:
                default:
                    SetNewsletterError("Something went wrong. Please try again shortly.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Newsletter subscribe error: {ex.Message}");
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

    private void SetNewsletterError(string message)
    {
        newsletterHasError = true;
        newsletterErrorMessage = message;
    }

    private void ClearNewsletterError()
    {
        newsletterHasError = false;
        newsletterErrorMessage = string.Empty;
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (landingPageInstance != null)
            {
                await landingPageInstance.InvokeVoidAsync("dispose");
                await landingPageInstance.DisposeAsync();
            }

            if (jsModule != null)
                await jsModule.DisposeAsync();
        }
        catch
        {
            // Suppress disposal errors
        }
    }
}
