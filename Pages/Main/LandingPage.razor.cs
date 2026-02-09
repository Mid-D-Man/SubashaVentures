using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.VisualElements;

namespace SubashaVentures.Pages.Main;

public partial class LandingPage : ComponentBase, IAsyncDisposable
{
    [Inject] private IProductOfTheDayService ProductOfTheDayService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ProductViewModel? productOfTheDay;
    private List<ProductViewModel> featuredProducts = new();
    private List<ReviewViewModel> sampleReviews = new();
    
    private bool isLoadingPOTD = true; 
    private bool isLoadingFeatured = true;
    
    private string newsletterEmail = "";
    
    private string shopNowIcon = "";
    private string storyIcon = "";
    private string allProductsIcon = "";

    private IJSObjectReference? jsModule;
    private IJSObjectReference? landingPageInstance;

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
    
    private async Task LoadButtonIcons()
    {
        try
        {
            shopNowIcon = await VisualElements.GetSvgWithColorAsync(SvgType.ShopNow, 20, 20, "currentColor");
            storyIcon = await VisualElements.GetSvgWithColorAsync(SvgType.Story, 20, 20, "currentColor");
            allProductsIcon = await VisualElements.GetSvgWithColorAsync(SvgType.AllProducts, 20, 20, "currentColor");
            
            Console.WriteLine("✓ Loaded landing page button icons");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading button icons: {ex.Message}");
        }
    }

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
            
            if (availableFeatured.Any())
            {
                var random = new Random();
                featuredProducts = availableFeatured
                    .OrderBy(x => random.Next())
                    .Take(6)
                    .ToList();
            }
            else
            {
                var random = new Random();
                featuredProducts = allProducts
                    .Where(p => p.IsActive && p.Stock > 0)
                    .OrderBy(x => random.Next())
                    .Take(6)
                    .ToList();
            }
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
                UserAvatar = null,
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
                UserAvatar = null,
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
                UserAvatar = null,
                Rating = 5,
                Title = "Perfect for Kids",
                Comment = "Great selection for kids' clothes. My daughter loves her new outfits and they're so comfortable!",
                IsVerifiedPurchase = true,
                HelpfulCount = 31,
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            }
        };
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("shop");
    }

    private void HandlePOTDClick(ProductViewModel product)
    {
        Navigation.NavigateTo($"product/{product.Slug}");
    }

    private void HandleViewPOTDDetails(ProductViewModel product)
    {
        Navigation.NavigateTo($"product/{product.Slug}");
    }

    private async Task HandleAddToCart(ProductViewModel product)
    {
        await Task.CompletedTask;
    }

    private void HandleProductClick(ProductViewModel product)
    {
        Navigation.NavigateTo($"product/{product.Slug}");
    }

    private void HandleViewDetails(ProductViewModel product)
    {
        Navigation.NavigateTo($"product/{product.Slug}");
    }

    private async Task HandleHelpfulClick(ReviewViewModel review)
    {
        await Task.CompletedTask;
    }

    private async Task HandleReviewImageClick(string imageUrl)
    {
        await Task.CompletedTask;
    }

    private async Task HandleNewsletterSubmit()
    {
        if (string.IsNullOrWhiteSpace(newsletterEmail))
        {
            return;
        }

        try
        {
            await Task.Delay(500);
            newsletterEmail = "";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Newsletter subscription error: {ex.Message}");
        }
    }

    private void HandleNewsletterKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(newsletterEmail))
        {
            _ = HandleNewsletterSubmit();
        }
    }

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
            {
                await jsModule.DisposeAsync();
            }
        }
        catch
        {
            // Suppress disposal errors
        }
    }
}
