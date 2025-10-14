using Microsoft.AspNetCore.Components;

namespace SubashaVentures.Pages.Product;

public partial class ProductDetails : ComponentBase
{
    [Parameter] public string? ProductId { get; set; }

    private ProductViewModel? product;
    private bool isLoading = true;
    
    // Selected options
    private string selectedImage = "";
    private string selectedSize = "";
    private string selectedColor = "";
    private int quantity = 1;
    
    // UI state
    private bool isInWishlist = false;
    private string activeTab = "description";

    protected override async Task OnInitializedAsync()
    {
        await LoadProduct();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Reload product when ProductId changes
        if (!string.IsNullOrEmpty(ProductId))
        {
            await LoadProduct();
        }
    }

    private async Task LoadProduct()
    {
        isLoading = true;
        StateHasChanged();

        try
        {
            // Simulate API call
            await Task.Delay(500);
            
            // In real app: product = await ProductService.GetProductById(ProductId);
            
            // Sample product data
            product = new ProductViewModel
            {
                Id = ProductId ?? "1",
                Name = "Premium Cotton T-Shirt",
                Description = "High-quality cotton t-shirt with modern fit and comfortable feel. Perfect for everyday wear.",
                LongDescription = "This premium cotton t-shirt is crafted from 100% organic cotton, providing ultimate comfort and breathability. The modern fit ensures a flattering silhouette while maintaining ease of movement. Features include reinforced seams, pre-shrunk fabric, and color-fast dye for long-lasting quality.",
                Price = 8500,
                OriginalPrice = 12000,
                IsOnSale = true,
                Discount = 29,
                Rating = 4.5f,
                ReviewCount = 127,
                Stock = 45,
                Sku = "TSH-001-BLK-M",
                Category = "Men's Fashion",
                Brand = "SubashaVentures",
                Images = new List<string>
                {
                    "https://via.placeholder.com/600x600?text=Product+Image+1",
                    "https://via.placeholder.com/600x600?text=Product+Image+2",
                    "https://via.placeholder.com/600x600?text=Product+Image+3",
                    "https://via.placeholder.com/600x600?text=Product+Image+4"
                },
                Sizes = new List<string> { "XS", "S", "M", "L", "XL", "XXL" },
                Colors = new List<string> { "#000000", "#FFFFFF", "#1E40AF", "#DC2626", "#059669" }
            };

            // Set initial selections
            if (product.Images.Any())
                selectedImage = product.Images.First();
            
            if (product.Sizes.Any())
                selectedSize = product.Sizes[2]; // Default to M
                
            if (product.Colors.Any())
                selectedColor = product.Colors.First();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading product: {ex.Message}");
            product = null;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void SelectImage(string image)
    {
        selectedImage = image;
    }

    private void SelectSize(string size)
    {
        selectedSize = size;
    }

    private void SelectColor(string color)
    {
        selectedColor = color;
    }

    private void IncreaseQuantity()
    {
        if (product != null && quantity < product.Stock)
        {
            quantity++;
        }
    }

    private void DecreaseQuantity()
    {
        if (quantity > 1)
        {
            quantity--;
        }
    }

    private async Task AddToCart()
    {
        if (product == null) return;

        // Validate selections
        if (product.Sizes.Any() && string.IsNullOrEmpty(selectedSize))
        {
            Console.WriteLine("Please select a size");
            return;
        }

        if (product.Colors.Any() && string.IsNullOrEmpty(selectedColor))
        {
            Console.WriteLine("Please select a color");
            return;
        }

        try
        {
            // In real app: await CartService.AddToCart(product.Id, quantity, selectedSize, selectedColor);
            
            Console.WriteLine($"Added to cart: {product.Name}");
            Console.WriteLine($"Quantity: {quantity}");
            Console.WriteLine($"Size: {selectedSize}");
            Console.WriteLine($"Color: {selectedColor}");
            
            // Show success message or navigate to cart
            // NavigationManager.NavigateTo("/cart");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding to cart: {ex.Message}");
        }
    }

    private async Task ToggleWishlist()
    {
        if (product == null) return;

        try
        {
            isInWishlist = !isInWishlist;
            
            // In real app:
            // if (isInWishlist)
            //     await WishlistService.AddToWishlist(product.Id);
            // else
            //     await WishlistService.RemoveFromWishlist(product.Id);
            
            Console.WriteLine($"Wishlist toggled: {isInWishlist}");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling wishlist: {ex.Message}");
            isInWishlist = !isInWishlist; // Revert on error
        }
    }

    private void SetActiveTab(string tab)
    {
        activeTab = tab;
    }

    // View Model
    public class ProductViewModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string LongDescription { get; set; } = "";
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public bool IsOnSale { get; set; }
        public int Discount { get; set; }
        public float Rating { get; set; }
        public int ReviewCount { get; set; }
        public int Stock { get; set; }
        public string Sku { get; set; } = "";
        public string Category { get; set; } = "";
        public string Brand { get; set; } = "";
        public List<string> Images { get; set; } = new();
        public List<string> Sizes { get; set; } = new();
        public List<string> Colors { get; set; } = new();
    }
}
