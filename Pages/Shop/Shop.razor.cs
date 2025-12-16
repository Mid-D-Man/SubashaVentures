using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Navigation;
using SubashaVentures.Domain.Product;
using static SubashaVentures.Layout.Shop.ShopFilterPanel;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase, IDisposable
{
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private INavigationService NavigationService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<Shop> Logger { get; set; } = default!;

    [Parameter] public string? Category { get; set; }

    private List<ProductViewModel> allProducts = new();
    private List<ProductViewModel> products = new();
    private List<ProductViewModel> paginatedProducts = new();
    private List<string> activeFilters = new();

    private FilterState? currentFilters;
    private string searchQuery = "";
    private bool isLoading = true;

    private int currentPage = 1;
    private const int itemsPerPage = 24;
    private int totalPages = 1;

    private string PageTitle => string.IsNullOrEmpty(Category) ? "All Products" : Category;
    private int TotalProducts => products.Count;

    protected override async Task OnInitializedAsync()
    {
        NavigationService.SearchQueryChanged += OnSearchChanged;
        await LoadProducts();
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadProducts();
    }

    private async Task LoadProducts()
    {
        isLoading = true;
        StateHasChanged();

        try
        {
            if (string.IsNullOrEmpty(Category))
            {
                allProducts = await ProductService.GetAllProductsAsync();
            }
            else
            {
                var categoryModel = await CategoryService.GetCategoryBySlugAsync(Category);
                if (categoryModel != null)
                {
                    allProducts = await ProductService.GetProductsByCategoryAsync(categoryModel.Id);
                }
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load products");
            allProducts = new();
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void ApplyFilters()
    {
        products = new List<ProductViewModel>(allProducts);
        activeFilters.Clear();

        // Search
        searchQuery = NavigationService.SearchQuery;
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            products = products.Where(p =>
                p.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Brand?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
            activeFilters.Add($"Search: {searchQuery}");
        }

        // Filter State
        if (currentFilters != null)
        {
            if (currentFilters.Categories.Any())
            {
                products = products.Where(p => currentFilters.Categories.Contains(p.CategoryId)).ToList();
                activeFilters.Add($"Categories: {currentFilters.Categories.Count}");
            }

            if (currentFilters.Brands.Any())
            {
                products = products.Where(p => currentFilters.Brands.Contains(p.Brand)).ToList();
                activeFilters.Add($"Brands: {currentFilters.Brands.Count}");
            }

            if (currentFilters.MinPrice.HasValue)
            {
                products = products.Where(p => p.Price >= currentFilters.MinPrice.Value).ToList();
                activeFilters.Add($"Min: ₦{currentFilters.MinPrice:N0}");
            }

            if (currentFilters.MaxPrice.HasValue)
            {
                products = products.Where(p => p.Price <= currentFilters.MaxPrice.Value).ToList();
                activeFilters.Add($"Max: ₦{currentFilters.MaxPrice:N0}");
            }

            if (currentFilters.InStockOnly)
            {
                products = products.Where(p => p.Stock > 0).ToList();
                activeFilters.Add("In Stock");
            }

            if (currentFilters.OnSaleOnly)
            {
                products = products.Where(p => p.IsOnSale).ToList();
                activeFilters.Add("On Sale");
            }
        }

        CalculatePagination();
    }

    private void CalculatePagination()
    {
        totalPages = (int)Math.Ceiling(products.Count / (double)itemsPerPage);
        currentPage = Math.Max(1, Math.Min(currentPage, totalPages));

        var skip = (currentPage - 1) * itemsPerPage;
        paginatedProducts = products.Skip(skip).Take(itemsPerPage).ToList();
    }

    public void HandleFiltersApplied(FilterState filters)
    {
        currentFilters = filters;
        currentPage = 1;
        ApplyFilters();
    }

    private void RemoveFilter(string filter)
    {
        // Implement specific filter removal logic
        currentPage = 1;
        ApplyFilters();
    }

    private void ClearAllFilters()
    {
        currentFilters = null;
        NavigationService.ClearSearchQuery();
        currentPage = 1;
        ApplyFilters();
    }

    private void PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            CalculatePagination();
        }
    }

    private void NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            CalculatePagination();
        }
    }

    private void NavigateToProduct(string slug)
    {
        Navigation.NavigateTo($"/product/{slug}");
    }

    private async void OnSearchChanged(object? sender, string query)
    {
        currentPage = 1;
        ApplyFilters();
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        NavigationService.SearchQueryChanged -= OnSearchChanged;
    }
}
