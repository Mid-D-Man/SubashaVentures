using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Categories;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase, IDisposable
{
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Shop> Logger { get; set; } = default!;
    
    [Parameter] public string? Category { get; set; }

    // State
    private List<ProductViewModel> allProducts = new();
    private List<ProductViewModel> products = new();
    private List<ProductViewModel> paginatedProducts = new();
    private List<CategoryViewModel> categories = new();
    private HashSet<int> wishlistedProductIds = new();
    private List<string> activeFilters = new();
    private bool isLoading = true;
    private bool isInitialLoad = true;
    private string viewMode = "grid";
    private string sortBy = "relevance";
    private string? errorMessage;
    private string? currentCategoryId;
    private CategoryViewModel? currentCategory;
    
    // Pagination
    private int currentPage = 1;
    private int itemsPerPage = 24;
    private int totalPages = 1;
    private int TotalProducts => products.Count;

    // Object pooling for performance
    private MID_ComponentObjectPool<List<ProductViewModel>>? productListPool;

    protected override async Task OnInitializedAsync()
    {
        await MID_HelperFunctions.DebugMessageAsync("Shop component initializing", LogLevel.Info);
        
        // Initialize object pool for product lists
        productListPool = new MID_ComponentObjectPool<List<ProductViewModel>>(
            objectGenerator: () => new List<ProductViewModel>(100),
            resetAction: list => list.Clear(),
            maxPoolSize: 5
        );
        
        // Load categories first
        await LoadCategoriesAsync();
        
        // Then load products
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
            await MID_HelperFunctions.DebugMessageAsync("Loading categories", LogLevel.Info);
            
            categories = await CategoryService.GetAllCategoriesAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {categories.Count} categories",
                LogLevel.Info
            );
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
            
            // Load all products from service
            allProducts = await ProductService.GetProductsAsync(0, 1000);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {allProducts.Count} products from service", 
                LogLevel.Info
            );

            // Filter by category if specified
            if (!string.IsNullOrEmpty(Category) && Category.ToLower() != "all")
            {
                // First try to find category by slug
                currentCategory = await CategoryService.GetCategoryBySlugAsync(Category);
                
                if (currentCategory != null)
                {
                    // Filter by category ID
                    products = allProducts
                        .Where(p => p.CategoryId == currentCategory.Id)
                        .ToList();
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Filtered to {products.Count} products in category '{currentCategory.Name}'", 
                        LogLevel.Info
                    );
                }
                else
                {
                    // Fallback: try matching by category name (case-insensitive)
                    products = allProducts
                        .Where(p => p.Category.Equals(Category, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Filtered to {products.Count} products by category name '{Category}'", 
                        LogLevel.Info
                    );
                }
            }
            else
            {
                // Show all products
                products = new List<ProductViewModel>(allProducts);
                currentCategory = null;
            }

            // Apply current sort
            ApplySort();
            
            // Calculate pagination
            CalculatePagination();
            
            // Load wishlist (in real app, this would come from a service)
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

    private async Task LoadWishlistAsync()
    {
        // TODO: Load from actual wishlist service
        await Task.CompletedTask;
        wishlistedProductIds = new HashSet<int>();
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

    private string GetCategoryDisplayName(string? category)
    {
        if (string.IsNullOrEmpty(category))
            return "All Products";
        
        // First check if we have the current category loaded
        if (currentCategory != null)
            return currentCategory.Name;
        
        // Fallback to matching by slug or name
        var matchedCategory = categories.FirstOrDefault(c => 
            c.Slug.Equals(category, StringComparison.OrdinalIgnoreCase) ||
            c.Name.Equals(category, StringComparison.OrdinalIgnoreCase)
        );
        
        return matchedCategory?.Name ?? category;
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

    private void ApplySort()
    {
        products = sortBy switch
        {
            "price-low" => products.OrderBy(p => p.Price).ToList(),
            "price-high" => products.OrderByDescending(p => p.Price).ToList(),
            "rating" => products.OrderByDescending(p => p.Rating).ThenByDescending(p => p.ReviewCount).ToList(),
            "newest" => products.OrderByDescending(p => p.CreatedAt).ToList(),
            "popular" => products.OrderByDescending(p => p.ViewCount).ThenByDescending(p => p.SalesCount).ToList(),
            "name" => products.OrderBy(p => p.Name).ToList(),
            _ => products // relevance - keep original order
        };
    }

    private void RemoveFilter(string filter)
    {
        activeFilters.Remove(filter);
        // TODO: Apply filter logic
        StateHasChanged();
    }

    private void ClearAllFilters()
    {
        activeFilters.Clear();
        // TODO: Reset filter logic
        StateHasChanged();
    }

    private async Task ResetFilters()
    {
        activeFilters.Clear();
        sortBy = "relevance";
        currentPage = 1;
        await LoadProductsAsync();
    }

    // Navigation
    private void NavigateToProduct(ProductViewModel product)
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
            
            // Show success feedback
            Logger.LogInformation("Added {ProductName} to cart", product.Name);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding to cart");
            Logger.LogError(ex, "Failed to add {ProductId} to cart", product.Id);
        }
    }

    private async Task HandleWishlistToggle(ProductViewModel product)
    {
        try
        {
            if (wishlistedProductIds.Contains(product.Id))
            {
                wishlistedProductIds.Remove(product.Id);
                await MID_HelperFunctions.DebugMessageAsync($"Removed from wishlist: {product.Name}", LogLevel.Info);
            }
            else
            {
                wishlistedProductIds.Add(product.Id);
                await MID_HelperFunctions.DebugMessageAsync($"Added to wishlist: {product.Name}", LogLevel.Info);
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
        productListPool?.Dispose();
    }
}
