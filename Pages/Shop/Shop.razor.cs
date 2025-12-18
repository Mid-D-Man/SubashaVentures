using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;
using SubashaVentures.Layout.Shop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase
{
    [Inject] private IProductService ProductService { get; set; } = null!;

    private List<ProductViewModel> AllProducts { get; set; } = new();
    private List<ProductViewModel> FilteredProducts { get; set; } = new();
    private List<ProductViewModel> CurrentPageProducts { get; set; } = new();
    
    private bool IsLoading { get; set; } = true;
    private bool HasError { get; set; }
    private string ErrorMessage { get; set; } = "";
    
    // Search and Sort
    private string SearchQuery { get; set; } = "";
    private string SelectedSort { get; set; } = "default";
    
    // Active Filters
    private List<string> ActiveCategories = new();
    private List<string> ActiveBrands = new();
    private int ActiveMinRating = 0;
    private decimal ActiveMinPrice = 0;
    private decimal ActiveMaxPrice = 1000000;
    private bool ActiveOnSale = false;
    private bool ActiveFreeShipping = false;
    
    // Pagination
    private int CurrentPage { get; set; } = 1;
    private int ItemsPerPage { get; set; } = 12;
    private int TotalPages => (int)Math.Ceiling((double)FilteredProducts.Count / ItemsPerPage);

    // Mobile Filter State
    public bool ShowMobileFilters { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadProducts();
    }

    private async Task LoadProducts()
    {
        IsLoading = true;
        HasError = false;
        StateHasChanged();

        try
        {
            AllProducts = await ProductService.GetAllProductsAsync();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = "Unable to load products. Please try again.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading products");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    // PUBLIC - Called from ShopFilterPanel
    public void HandleFiltersChange(ShopFilterPanel.FilterState filters)
    {
        ActiveCategories = filters.Categories;
        ActiveBrands = filters.Brands;
        ActiveMinRating = filters.MinRating;
        ActiveMinPrice = filters.MinPrice;
        ActiveMaxPrice = filters.MaxPrice;
        ActiveOnSale = filters.OnSale;
        ActiveFreeShipping = filters.FreeShipping;
        
        CurrentPage = 1;
        ApplyFilters();
        CloseMobileFilters();
    }

    // PUBLIC - Called from ShopTop
    public void HandleSearchChange(string query)
    {
        SearchQuery = query ?? "";
        CurrentPage = 1;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        FilteredProducts = AllProducts.Where(p => p.IsActive).ToList();

        // Search
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLower();
            FilteredProducts = FilteredProducts.Where(p =>
                p.Name.ToLower().Contains(query) ||
                p.Description.ToLower().Contains(query) ||
                p.Brand.ToLower().Contains(query) ||
                p.Category.ToLower().Contains(query)
            ).ToList();
        }

        // Categories
        if (ActiveCategories.Any())
        {
            FilteredProducts = FilteredProducts
                .Where(p => ActiveCategories.Contains(p.Category))
                .ToList();
        }

        // Brands
        if (ActiveBrands.Any())
        {
            FilteredProducts = FilteredProducts
                .Where(p => ActiveBrands.Contains(p.Brand))
                .ToList();
        }

        // Rating
        if (ActiveMinRating > 0)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.Rating >= ActiveMinRating)
                .ToList();
        }

        // Price
        FilteredProducts = FilteredProducts
            .Where(p => p.Price >= ActiveMinPrice && p.Price <= ActiveMaxPrice)
            .ToList();

        // On Sale
        if (ActiveOnSale)
        {
            FilteredProducts = FilteredProducts.Where(p => p.IsOnSale).ToList();
        }

        // Free Shipping
        if (ActiveFreeShipping)
        {
            FilteredProducts = FilteredProducts.Where(p => p.Price >= 50000).ToList();
        }

        ApplySorting();
        UpdateCurrentPageProducts();
    }

    private void ApplySorting()
    {
        FilteredProducts = SelectedSort switch
        {
            "price-asc" => FilteredProducts.OrderBy(p => p.Price).ToList(),
            "price-desc" => FilteredProducts.OrderByDescending(p => p.Price).ToList(),
            "rating-desc" => FilteredProducts.OrderByDescending(p => p.Rating).ToList(),
            "name-asc" => FilteredProducts.OrderBy(p => p.Name).ToList(),
            "newest" => FilteredProducts.OrderByDescending(p => p.CreatedAt).ToList(),
            _ => FilteredProducts
        };
    }

    private void UpdateCurrentPageProducts()
    {
        var skip = (CurrentPage - 1) * ItemsPerPage;
        CurrentPageProducts = FilteredProducts.Skip(skip).Take(ItemsPerPage).ToList();
        StateHasChanged();
    }

    private void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;
        CurrentPage = page;
        UpdateCurrentPageProducts();
    }

    private void NextPage() => GoToPage(CurrentPage + 1);
    private void PreviousPage() => GoToPage(CurrentPage - 1);

    private List<int> GetVisiblePages()
    {
        var pages = new List<int>();
        if (TotalPages <= 5)
        {
            for (int i = 1; i <= TotalPages; i++) pages.Add(i);
        }
        else if (CurrentPage <= 3)
        {
            for (int i = 1; i <= 5; i++) pages.Add(i);
        }
        else if (CurrentPage >= TotalPages - 2)
        {
            for (int i = TotalPages - 4; i <= TotalPages; i++) pages.Add(i);
        }
        else
        {
            for (int i = CurrentPage - 2; i <= CurrentPage + 2; i++) pages.Add(i);
        }
        return pages;
    }

    private string GetResultsRange()
    {
        var start = (CurrentPage - 1) * ItemsPerPage + 1;
        var end = Math.Min(CurrentPage * ItemsPerPage, FilteredProducts.Count);
        return $"{start}-{end}";
    }

    private async Task HandleAddToCart(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"Add to cart: {productId}", LogLevel.Info);
    }

    private async Task HandleToggleFavorite(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"Toggle favorite: {productId}", LogLevel.Info);
    }

    private void ResetFilters()
    {
        SearchQuery = "";
        ActiveCategories.Clear();
        ActiveBrands.Clear();
        ActiveMinRating = 0;
        ActiveMinPrice = 0;
        ActiveMaxPrice = 1000000;
        ActiveOnSale = false;
        ActiveFreeShipping = false;
        SelectedSort = "default";
        CurrentPage = 1;
        ApplyFilters();
    }

    // PUBLIC - Mobile filter controls
    public void OpenMobileFilters()
    {
        ShowMobileFilters = true;
        StateHasChanged();
    }

    public void CloseMobileFilters()
    {
        ShowMobileFilters = false;
        StateHasChanged();
    }
}
