using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;

namespace SubashaVentures.Pages.Main;

public partial class LandingPage : ComponentBase
{
   [Inject] private IProductOfTheDayService ProductOfTheDayService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // State
    private ProductViewModel? productOfTheDay;
    private List<ProductViewModel> featuredProducts = new();
    private List<ReviewViewModel> sampleReviews = new();
    
    private bool isLoadingPOTD = true;
    private bool isLoadingFeatured = true;
    
    private string newsletterEmail = "";

    // JS Module references
    private IJSObjectReference? jsModule;
    private IJSObjectReference? landingPageInstance;

    protected override async Task OnInitializedAsync()
    {
        Console.WriteLine("🔵 LandingPage: OnInitializedAsync called");
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
                Console.WriteLine("🔵 LandingPage: OnAfterRenderAsync (firstRender=true)");
                
                // Import the ES6 module
                jsModule = await JS.InvokeAsync<IJSObjectReference>(
                    "import", 
                    "./Pages/Main/LandingPage.razor.js");
                
                Console.WriteLine("✓ Module imported successfully");

                // Create LandingPage instance
                landingPageInstance = await jsModule.InvokeAsync<IJSObjectReference>(
                    "LandingPage.create");
                
                Console.WriteLine("✓ LandingPage instance created");

                // Initialize
                await landingPageInstance.InvokeVoidAsync("initialize");
                
                Console.WriteLine("✅ LandingPage JS initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initializing LandingPage JS: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
            }
        }
    }

    private async Task LoadProductOfTheDay()
    {
        try
        {
            isLoadingPOTD = true;
            productOfTheDay = await ProductOfTheDayService.GetProductOfTheDayAsync();
            
            if (productOfTheDay != null)
            {
                Console.WriteLine($"✓ Product of the Day loaded: {productOfTheDay.Name}");
            }
            else
            {
                Console.WriteLine("⚠ No Product of the Day available");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading Product of the Day: {ex.Message}");
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
            Console.WriteLine($"📦 Total products retrieved: {allProducts.Count}");
            
            var availableFeatured = allProducts
                .Where(p => p.IsFeatured && p.IsActive && p.Stock > 0)
                .ToList();
            
            Console.WriteLine($"⭐ Featured products found: {availableFeatured.Count}");
            
            if (availableFeatured.Any())
            {
                var random = new Random();
                featuredProducts = availableFeatured
                    .OrderBy(x => random.Next())
                    .Take(6)
                    .ToList();
                
                Console.WriteLine($"🎲 Featured products selected (random order): {featuredProducts.Count}");
            }
            else
            {
                Console.WriteLine("⚠ No featured products, using random active products instead");
                
                var random = new Random();
                featuredProducts = allProducts
                    .Where(p => p.IsActive && p.Stock > 0)
                    .OrderBy(x => random.Next())
                    .Take(6)
                    .ToList();
                
                Console.WriteLine($"📊 Random active products selected: {featuredProducts.Count}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading featured products: {ex.Message}");
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

    // ===== NAVIGATION HANDLERS =====
    private void NavigateToShop()
    {
        Navigation.NavigateTo("shop");
    }

    // ===== PRODUCT OF THE DAY HANDLERS =====
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
        Console.WriteLine($"🛒 Add to cart: {product.Name}");
        await Task.CompletedTask;
    }

    // ===== FEATURED PRODUCTS HANDLERS =====
    private void HandleProductClick(ProductViewModel product)
    {
        Navigation.NavigateTo($"product/{product.Slug}");
    }

    private void HandleViewDetails(ProductViewModel product)
    {
        Navigation.NavigateTo($"product/{product.Slug}");
    }

    // ===== REVIEW HANDLERS =====
    private async Task HandleHelpfulClick(ReviewViewModel review)
    {
        Console.WriteLine($"👍 Marked review {review.Id} as helpful");
        await Task.CompletedTask;
    }

    private async Task HandleReviewImageClick(string imageUrl)
    {
        Console.WriteLine($"🖼️ Review image clicked: {imageUrl}");
        await Task.CompletedTask;
    }

    // ===== NEWSLETTER HANDLERS =====
    private async Task HandleNewsletterSubmit()
    {
        if (string.IsNullOrWhiteSpace(newsletterEmail))
        {
            Console.WriteLine("⚠️ Newsletter email is empty");
            return;
        }

        try
        {
            Console.WriteLine($"📧 Newsletter subscription attempt: {newsletterEmail}");
            
            await Task.Delay(500);
            
            Console.WriteLine($"✅ Newsletter subscription successful: {newsletterEmail}");
            
            newsletterEmail = "";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Newsletter subscription error: {ex.Message}");
        }
    }

    private void HandleNewsletterKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(newsletterEmail))
        {
            _ = HandleNewsletterSubmit();
        }
    }

    // ===== DISPOSAL =====
    public async ValueTask DisposeAsync()
    {
        try
        {
            Console.WriteLine("🔵 LandingPage: DisposeAsync called");
            
            if (landingPageInstance != null)
            {
                await landingPageInstance.InvokeVoidAsync("dispose");
                await landingPageInstance.DisposeAsync();
                Console.WriteLine("✓ LandingPage instance disposed");
            }

            if (jsModule != null)
            {
                await jsModule.DisposeAsync();
                Console.WriteLine("✓ JS module disposed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error disposing LandingPage: {ex.Message}");
        }
    }
}