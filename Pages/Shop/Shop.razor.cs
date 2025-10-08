using Microsoft.AspNetCore.Components;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase
{
    [Parameter] public string? Category { get; set; }

    private List<ProductViewModel> products = new();
    private List<string> activeFilters = new();
    private bool isLoading = true;
    private string viewMode = "grid";
    private string sortBy = "relevance";
    
    // Pagination
    private int currentPage = 1;
    private int itemsPerPage = 24;
    private int totalPages = 1;
    private int TotalProducts => products.Count;

    protected override async Task OnInitializedAsync()
    {
        await LoadProducts();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Reload products when category changes
        await LoadProducts();
    }

    private async Task LoadProducts()
    {
        isLoading = true;
        StateHasChanged();

        // Simulate API call
        await Task.Delay(500);

        // Generate sample products
        products = GenerateSampleProducts();
        
        // Calculate pagination
        totalPages = (int)Math.Ceiling(products.Count / (double)itemsPerPage);
        
        isLoading = false;
        StateHasChanged();
    }

    private List<ProductViewModel> GenerateSampleProducts()
    {
        var sampleProducts = new List<ProductViewModel>();
        var random = new Random();
        
        var productNames = new[]
        {
            "Premium Cotton T-Shirt", "Classic Denim Jeans", "Leather Jacket",
            "Summer Dress", "Casual Sneakers", "Wool Sweater",
            "Sport Shorts", "Formal Shirt", "Running Shoes",
            "Winter Coat", "Yoga Pants", "Baseball Cap",
            "Sunglasses", "Backpack", "Watch",
            "Belt", "Scarf", "Gloves",
            "Socks", "Underwear", "Pajamas",
            "Hoodie", "Tank Top", "Swim Trunks"
        };

        for (int i = 0; i < 48; i++)
        {
            var basePrice = random.Next(20, 200);
            var isOnSale = random.Next(0, 3) == 0; // 33% chance of being on sale
            var discount = isOnSale ? random.Next(10, 50) : 0;
            var price = isOnSale ? basePrice * (100 - discount) / 100m : basePrice;

            sampleProducts.Add(new ProductViewModel
            {
                Id = i + 1,
                Name = productNames[random.Next(productNames.Length)] + $" #{i + 1}",
                Price = price,
                OriginalPrice = isOnSale ? basePrice : null,
                ImageUrl = $"https://via.placeholder.com/300x400?text=Product+{i + 1}",
                Rating = random.Next(30, 50) / 10f,
                ReviewCount = random.Next(10, 500),
                IsOnSale = isOnSale,
                Discount = discount,
                Category = Category ?? "all"
            });
        }

        return sampleProducts;
    }

    private string GetCategoryDisplayName(string category)
    {
        return category switch
        {
            "men" => "Men's Fashion",
            "women" => "Women's Fashion",
            "kids" => "Kids & Baby",
            "home" => "Home & Living",
            "accessories" => "Accessories",
            _ => "All Products"
        };
    }

    private void SetViewMode(string mode)
    {
        viewMode = mode;
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "relevance";
        ApplySort();
    }

    private void ApplySort()
    {
        products = sortBy switch
        {
            "price-low" => products.OrderBy(p => p.Price).ToList(),
            "price-high" => products.OrderByDescending(p => p.Price).ToList(),
            "rating" => products.OrderByDescending(p => p.Rating).ToList(),
            "newest" => products.OrderByDescending(p => p.Id).ToList(),
            "popular" => products.OrderByDescending(p => p.ReviewCount).ToList(),
            _ => products
        };
        StateHasChanged();
    }

    private void RemoveFilter(string filter)
    {
        activeFilters.Remove(filter);
        StateHasChanged();
    }

    private void ClearAllFilters()
    {
        activeFilters.Clear();
        StateHasChanged();
    }

    private void ResetFilters()
    {
        activeFilters.Clear();
        sortBy = "relevance";
        currentPage = 1;
        StateHasChanged();
    }

    // Pagination methods
    private void PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            ScrollToTop();
        }
    }

    private void NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            ScrollToTop();
        }
    }

    private void GoToPage(int page)
    {
        currentPage = page;
        ScrollToTop();
    }

    private void ScrollToTop()
    {
        // In a real app, use JSInterop to scroll to top
        // await JSRuntime.InvokeVoidAsync("window.scrollTo", 0, 0);
    }

    // View Model
    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public float Rating { get; set; }
        public int ReviewCount { get; set; }
        public bool IsOnSale { get; set; }
        public int Discount { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}
