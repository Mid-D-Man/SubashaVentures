using Microsoft.AspNetCore.Components;

namespace SubashaVentures.Pages.Main;

public partial class LandingPage : ComponentBase
{
    // Sample data for demonstration
    private readonly List<CategoryItem> categories = new()
    {
        new CategoryItem("Men's Fashion", "👔", "Stylish clothing for the modern man", "/men"),
        new CategoryItem("Women's Fashion", "👚", "Elegant styles for every occasion", "/women"),
        new CategoryItem("Kids & Baby", "🧸", "Comfortable clothes for little ones", "/children"),
        new CategoryItem("Home & Living", "🏠", "Beautiful items for your home", "/home")
    };
    
    private readonly List<ProductItem> featuredProducts = new()
    {
        new ProductItem("Premium Shirt", "Clothing", 39.99m, 49.99m, 4.5f),
        new ProductItem("Designer Dress", "Fashion", 49.99m, 59.99m, 4.8f),
        new ProductItem("Cozy Sweater", "Clothing", 59.99m, 69.99m, 4.6f),
        new ProductItem("Home Decor Set", "Home", 69.99m, 79.99m, 4.7f),
        new ProductItem("Kids Outfit", "Children", 29.99m, 39.99m, 4.9f),
        new ProductItem("Luxury Bedding", "Home", 89.99m, 99.99m, 4.8f)
    };
    
    private readonly List<TestimonialItem> testimonials = new()
    {
        new TestimonialItem("Sarah Johnson", "New York, NY", "👩‍💼", 
            "Amazing quality and fast shipping! The clothes fit perfectly and the style is exactly what I was looking for.", 5),
        new TestimonialItem("Mike Chen", "San Francisco, CA", "👨‍💻", 
            "Love the home decor section! Found the perfect pieces to complete my living room makeover.", 5),
        new TestimonialItem("Emma Davis", "Austin, TX", "👩‍👧", 
            "Great selection for kids' clothes. My daughter loves her new outfits and they're so comfortable!", 5)
    };

    protected override async Task OnInitializedAsync()
    {
        // Initialize component - removed unused isLoading and currentTheme fields
        try
        {
            // Simulate loading data
            await Task.Delay(100);
            
            // Any initialization logic
            await LoadInitialData();
        }
        catch (Exception ex)
        {
            // Handle initialization errors
            Console.WriteLine($"Error initializing landing page: {ex.Message}");
        }
    }

    private async Task LoadInitialData()
    {
        try
        {
            // In a real app, you'd load data from APIs here
            // For now, we're using the static data defined above
            
            // Simulate API delay
            await Task.Delay(50);
            
            // Data is already loaded in the constructor
            // This method is here for future API integration
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading initial data: {ex.Message}");
        }
    }

    // Event handlers for user interactions
    private async Task HandleShopNowClick()
    {
        try
        {
            // Navigate to shop page
            // NavigationManager.NavigateTo("/shop");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling shop now click: {ex.Message}");
        }
    }

    private async Task HandleOurStoryClick()
    {
        try
        {
            // Navigate to about page
            // NavigationManager.NavigateTo("/about");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling our story click: {ex.Message}");
        }
    }

    private async Task HandleCategoryClick(string categoryUrl)
    {
        try
        {
            // Navigate to category page
            // NavigationManager.NavigateTo(categoryUrl);
            Console.WriteLine($"Navigating to: {categoryUrl}");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling category click: {ex.Message}");
        }
    }

    private async Task HandleProductQuickView(ProductItem product)
    {
        try
        {
            // Show product quick view modal
            Console.WriteLine($"Quick viewing: {product.Name}");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling product quick view: {ex.Message}");
        }
    }

    private async Task HandleNewsletterSubscribe(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                Console.WriteLine("Email is required for newsletter subscription");
                return;
            }

            // Validate email
            if (!IsValidEmail(email))
            {
                Console.WriteLine("Please enter a valid email address");
                return;
            }

            // Subscribe to newsletter
            Console.WriteLine($"Subscribing email: {email}");
            
            // In a real app, you'd call an API here
            await Task.Delay(1000);
            
            Console.WriteLine("Successfully subscribed to newsletter!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error subscribing to newsletter: {ex.Message}");
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    // Data models for the page components
    public record CategoryItem(string Name, string Icon, string Description, string Url);
    
    public record ProductItem(string Name, string Category, decimal CurrentPrice, decimal OriginalPrice, float Rating);
    
    public record TestimonialItem(string AuthorName, string Location, string Avatar, string Text, int Rating);

    // Component lifecycle methods
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            // Any JavaScript interop or DOM manipulation after first render
            Console.WriteLine("Landing page first render completed");
        }
    }

    public void Dispose()
    {
        // Clean up any resources if needed
        // This component implements IDisposable implicitly through ComponentBase
    }
}