using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Navigation;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase, IDisposable
{
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private INavigationService NavigationService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Shop> Logger { get; set; } = default!;
    
    [Parameter] public string? Category { get; set; }

    // State
    private List<ProductViewModel> allProducts = new();
    private List<ProductViewModel> products = new();
    private List<ProductViewModel> paginatedProducts = new();
    private List<CategoryViewModel> categories = new();
    private HashSet<int> wishlistedProductIds = new();
    private List<FilterTag> activeFilters = new();
    private bool isLoading = true;
    private bool isInitialLoad = true;
    private string viewMode = "grid";
    private string sortBy = "relevance";
    private string searchQuery = "";
    private string? errorMessage;
    private string? currentCategoryId;
    private CategoryViewModel? currentCategory;
    
    // Pagination
    private int currentPage = 1;
    private int itemsPerPage = 24;
    private int totalPages = 1;
    private int TotalProducts => products.Count;

    // Object pooling
    private MID_ComponentObjectPool<List<ProductViewModel>>? productListPool;

    protected override async Task OnInitializedAsync()
    {
        await MID_HelperFunctions.DebugMessageAsync("Shop component initializing", LogLevel.Info);
        
        // Subscribe to navigation service events
        NavigationService.SearchQueryChanged += OnSearchQueryChanged;
        NavigationService.FiltersChanged += OnFiltersChanged;
        
        // Initialize object pool
        productListPool = new MID_ComponentObjectPool<List<ProductViewModel>>(
            objectGenerator: () => new List<ProductViewModel>(100),
            resetAction: list => list.Clear(),
            maxPoolSize: 5
        );
        
        // Get initial search query
        searchQuery = NavigationService.SearchQuery;
        
        // Load categories
        await LoadCategoriesAsync();
        
        // Load products
        await LoadProductsAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!isInitialLoad && Category != currentCategoryId)
        {
            await MID_HelperFunctions.DebugMessageAsync($"Category changed to: {Category}", LogLevel.Info);
            currentCategoryId = Category;
            await LoadProductsAsync();
        }
        isInitialLoad = false;
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            categories = await CategoryService.GetAllCategoriesAsync();
            await MID_HelperFunctions.DebugMessageAsync($"✓ Loaded {categories.Count} categories", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading categories");
            Logger.LogError(ex, "Failed to load categories");
            categories = new List<CategoryViewModel>();
        }
    }

    private async Task LoadProductsAsync()
    {
        isLoading = true;
        errorMessage = null;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync("Loading products from service", LogLevel.Info);
            
            allProducts = await ProductService.GetProductsAsync(0, 1000);
            
            await MID_HelperFunctions.DebugMessageAsync($"Loaded {allProducts.Count} products", LogLevel.Info);

            // Apply category filter
            await ApplyCategoryFilterAsync();
            
            // Apply search filter
            ApplySearchFilter();
            
            // Apply active filters
            ApplyActiveFilters();
            
            // Apply sort
            ApplySort();
            
            // Calculate pagination
            CalculatePagination();
            
            // Load wishlist
            await LoadWishlistAsync();

            await MID_HelperFunctions.DebugMessageAsync("Products loaded successfully", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading products");
            Logger.LogError(ex, "Failed to load products");
            errorMessage = "Failed to load products. Please try again.";
            products = new List<ProductViewModel>();
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task ApplyCategoryFilterAsync()
    {
        if (!string.IsNullOrEmpty(Category) && Category.ToLower() != "all")
        {
            currentCategory = await CategoryService.GetCategoryBySlugAsync(Category);
            
            if (currentCategory != null)
            {
                products = allProducts.Where(p => p.CategoryId == currentCategory.Id).ToList();
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Filtered to {products.Count} products in category '{currentCategory.Name}'", 
                    LogLevel.Info
                );
            }
            else
            {
                products = allProducts
                    .Where(p => p.Category.Equals(Category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }
        else
        {
            products = new List<ProductViewModel>(allProducts);
            currentCategory = null;
        }
    }

    private void ApplySearchFilter()
    {
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var query = searchQuery.ToLowerInvariant();
            products = products.Where(p =>
                p.Name.ToLowerInvariant().Contains(query) ||
                p.Description.ToLowerInvariant().Contains(query) ||
                p.Brand.ToLowerInvariant().Contains(query) ||
                p.Category.ToLowerInvariant().Contains(query) ||
                p.Tags.Any(t => t.ToLowerInvariant().Contains(query))
            ).ToList();
            
            Logger.LogInformation("Applied search filter: {Query}, Results: {Count}", searchQuery, products.Count);
        }
    }

    private void ApplyActiveFilters()
    {
        foreach (var filter in activeFilters)
        {
            products = filter.FilterType switch
            {
                FilterType.PriceRange => ApplyPriceFilter(products, filter),
                FilterType.Rating => ApplyRatingFilter(products, filter),
                FilterType.Brand => ApplyBrandFilter(products, filter),
                FilterType.InStock => products.Where(p => p.IsInStock).ToList(),
                FilterType.OnSale => products.Where(p => p.IsOnSale).ToList(),
                _ => products
            };
        }
    }

    private List<ProductViewModel> ApplyPriceFilter(List<ProductViewModel> products, FilterTag filter)
    {
        if (filter.MinPrice.HasValue && filter.MaxPrice.HasValue)
        {
            return products.Where(p => p.Price >= filter.MinPrice && p.Price <= filter.MaxPrice).ToList();
        }
        else if (filter.MinPrice.HasValue)
        {
            return products.Where(p => p.Price >= filter.MinPrice).ToList();
        }
        else if (filter.MaxPrice.HasValue)
        {
            return products.Where(p => p.Price <= filter.MaxPrice).ToList();
        }
        return products;
    }

    private List<ProductViewModel> ApplyRatingFilter(List<ProductViewModel> products, FilterTag filter)
    {
        if (filter.MinRating.HasValue)
        {
            return products.Where(p => p.Rating >= filter.MinRating).ToList();
        }
        return products;
    }

    private List<ProductViewModel> ApplyBrandFilter(List<ProductViewModel> products, FilterTag filter)
    {
        if (!string.IsNullOrEmpty(filter.BrandName))
        {
            return products.Where(p => p.Brand.Equals(filter.BrandName, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return products;
    }

    private void ApplySort()
    {
        products = sortBy switch
        {
            "price-low" => products.OrderBy(p => p.Price).ToList(),
            "price-high" => products.OrderByDescending(p => p.Price).ToList(),
            "rating" => products.OrderByDescending(p => p.Rating).ThenByDescending(p => p.ReviewCount).ToList(),
            "newest" => products.OrderByDescending(p => p.CreatedAt).ToList(),
            "popular" => products.OrderByDescending(p => p.ViewCount).ThenByDescending(p => p.SalesCount).ToList(),
            "name-asc" => products.OrderBy(p => p.Name).ToList(),
            "name-desc" => products.OrderByDescending(p => p.Name).ToList(),
            _ => products
        };
    }

    private void CalculatePagination()
    {
        totalPages = (int)Math.Ceiling(products.Count / (double)itemsPerPage);
        currentPage = Math.Min(currentPage, Math.Max(1, totalPages));
        
        paginatedProducts = products
            .Skip((currentPage - 1) * itemsPerPage)
            .Take(itemsPerPage)
            .ToList();
    }

    private async Task LoadWishlistAsync()
    {
        // TODO: Load from actual wishlist service
        await Task.CompletedTask;
        wishlistedProductIds = new HashSet<int>();
    }

    private string GetCategoryDisplayName(string? category)
    {
        if (string.IsNullOrEmpty(category)) return "All Products";
        if (currentCategory != null) return currentCategory.Name;
        
        var matchedCategory = categories.FirstOrDefault(c => 
            c.Slug.Equals(category, StringComparison.OrdinalIgnoreCase) ||
            c.Name.Equals(category, StringComparison.OrdinalIgnoreCase)
        );
        
        return matchedCategory?.Name ?? category;
    }

    // Event Handlers
    private void OnSearchQueryChanged(object? sender, string query)
    {
        searchQuery = query;
        currentPage = 1;
        _ = LoadProductsAsync();
    }

    private void OnFiltersChanged(object? sender, EventArgs e)
    {
        currentPage = 1;
        _ = LoadProductsAsync();
    }

    private void SetViewMode(string mode)
    {
        viewMode = mode;
        StateHasChanged();
    }

    private async Task HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "relevance";
        await MID_HelperFunctions.DebugMessageAsync($"Sort changed to: {sortBy}", LogLevel.Info);
        
        ApplySort();
        CalculatePagination();
        StateHasChanged();
    }

    public void AddFilter(FilterTag filter)
    {
        if (!activeFilters.Any(f => f.Id == filter.Id))
        {
            activeFilters.Add(filter);
            currentPage = 1;
            _ = LoadProductsAsync();
        }
    }

    private void RemoveFilter(FilterTag filter)
    {
        activeFilters.Remove(filter);
        currentPage = 1;
        _ = LoadProductsAsync();
    }

    private void ClearAllFilters()
    {
        activeFilters.Clear();
        sortBy = "relevance";
        currentPage = 1;
        _ = LoadProductsAsync();
    }

    private async Task ResetFilters()
    {
        activeFilters.Clear();
        sortBy = "relevance";
        searchQuery = "";
        NavigationService.ClearSearchQuery();
        currentPage = 1;
        await LoadProductsAsync();
    }

    // Navigation
    private void HandleProductClick(ProductViewModel product)
    {
        NavigationManager.NavigateTo($"/product/{product.Slug}");
    }

    private async Task HandleQuickView(ProductViewModel product)
    {
        await MID_HelperFunctions.DebugMessageAsync($"Quick view: {product.Name}", LogLevel.Info);
        // TODO: Open quick view modal
    }

    private async Task HandleAddToCart(ProductViewModel product)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync($"Adding to cart: {product.Name}", LogLevel.Info);
            // TODO: Add to cart service
            Logger.LogInformation("Added {ProductName} to cart", product.Name);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding to cart");
        }
    }

    private async Task HandleWishlistToggle(ProductViewModel product)
    {
        try
        {
            if (wishlistedProductIds.Contains(product.Id))
            {
                wishlistedProductIds.Remove(product.Id);
            }
            else
            {
                wishlistedProductIds.Add(product.Id);
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling wishlist");
        }
    }

    // Pagination
    private void PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            CalculatePagination();
            ScrollToTop();
        }
    }

    private void NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            CalculatePagination();
            ScrollToTop();
        }
    }

    private void GoToPage(int page)
    {
        if (page >= 1 && page <= totalPages)
        {
            currentPage = page;
            CalculatePagination();
            ScrollToTop();
        }
    }

    private void ScrollToTop()
    {
        // In a real app, use JSInterop to scroll to top
        // await JSRuntime.InvokeVoidAsync("window.scrollTo", 0, 0);
    }

    public void Dispose()
    {
        NavigationService.SearchQueryChanged -= OnSearchQueryChanged;
        NavigationService.FiltersChanged -= OnFiltersChanged;
        productListPool?.Dispose();
    }
}

// Filter models
public enum FilterType
{
    PriceRange,
    Rating,
    Brand,
    InStock,
    OnSale
}

public class FilterTag
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DisplayText { get; set; } = "";
    public FilterType FilterType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public float? MinRating { get; set; }
    public string? BrandName { get; set; }
}
