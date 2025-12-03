using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;

namespace SubashaVentures.Pages.Main;

public partial class LandingPage : ComponentBase
{
    [Inject] private IProductOfTheDayService ProductOfTheDayService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    // State
    private ProductViewModel? productOfTheDay;
    private List<ProductViewModel> featuredProducts = new();
    private List<ReviewViewModel> sampleReviews = new();
    
    private bool isLoadingPOTD = true;
    private bool isLoadingFeatured = true;
    
    private string newsletterEmail = "";
    private string heroBackgroundImage = ""; // TODO: Replace with actual image from Supabase Storage

    protected override async Task OnInitializedAsync()
    {
        await LoadProductOfTheDay();
        await LoadFeaturedProducts();
        LoadSampleReviews();
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
        }
    }

    private async Task LoadFeaturedProducts()
    {
        try
        {
            isLoadingFeatured = true;
            var allProducts = await ProductService.GetProductsAsync(0, 100);
            featuredProducts = allProducts
                .Where(p => p.IsFeatured && p.IsActive)
                .Take(6)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading featured products: {ex.Message}");
        }
        finally
        {
            isLoadingFeatured = false;
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

    // Navigation Handlers
    private void NavigateToShop()
    {
        NavigationManager.NavigateTo("shop");
    }

    private void NavigateToCategory(string category)
    {
        NavigationManager.NavigateTo($"shop/{category}");
    }

    // Product of the Day Handlers
    private void HandlePOTDClick(ProductViewModel product)
    {
        NavigationManager.NavigateTo($"product/{product.Id}");
    }

    private void HandleViewPOTDDetails(ProductViewModel product)
    {
        NavigationManager.NavigateTo($"product/{product.Id}");
    }

    private async Task HandleAddToCart(ProductViewModel product)
    {
        // TODO: Implement add to cart functionality
        Console.WriteLine($"Add to cart: {product.Name}");
        await Task.CompletedTask;
    }

    // Featured Products Handlers
    private void HandleProductClick(ProductViewModel product)
    {
        // Show product details in current view (could open modal or sidebar)
        Console.WriteLine($"Product clicked: {product.Name}");
    }

    private void HandleViewDetails(ProductViewModel product)
    {
        // Navigate to full product page
        NavigationManager.NavigateTo($"product/{product.Id}");
    }

    // Review Handlers
    private async Task HandleHelpfulClick(ReviewViewModel review)
    {
        // TODO: Implement helpful vote functionality
        Console.WriteLine($"Marked review {review.Id} as helpful");
        await Task.CompletedTask;
    }

    private async Task HandleReviewImageClick(string imageUrl)
    {
        // TODO: Open image in lightbox/modal
        Console.WriteLine($"Review image clicked: {imageUrl}");
        await Task.CompletedTask;
    }

    // Newsletter Handlers
    private async Task HandleNewsletterSubmit()
    {
        if (string.IsNullOrWhiteSpace(newsletterEmail))
            return;

        try
        {
            // TODO: Implement newsletter subscription
            Console.WriteLine($"Newsletter subscription: {newsletterEmail}");
            
            // Clear input on success
            newsletterEmail = "";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Newsletter subscription error: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private void HandleNewsletterKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(newsletterEmail))
        {
            _ = HandleNewsletterSubmit();
        }
    }
}
