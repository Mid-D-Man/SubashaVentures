using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Shop;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Shop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase, IDisposable
{
    [Inject] private IProductService ProductService { get; set; } = null!;
    [Inject] private ShopStateService ShopState { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    
    // Data
    private List<ProductViewModel> AllProducts { get; set; } = new();
    private List<ProductViewModel> FilteredProducts { get; set; } = new();
    private List<ProductViewModel> CurrentPageProducts { get; set; } = new();
    
    // Available filter options (loaded from DB)
    private List<string> AvailableCategories { get; set; } = new();
    private List<string> AvailableBrands { get; set; } = new();
    
    // State
    private bool IsLoading { get; set; } = true;
    private bool HasError { get; set; }
    private string ErrorMessage { get; set; } = "";
    private bool IsInitialized { get; set; } = false;
    private bool AreFilterOptionsLoaded { get; set; } = false;
    
    // Current Filters (IN-MEMORY ONLY)
    private FilterState CurrentFilters { get; set; } = FilterState.CreateDefault();
    private string SelectedSort { get; set; } = "default";
    
    // Pagination
    private int CurrentPage { get; set; } = 1;
    private int ItemsPerPage { get; set; } = 12;
    private int TotalPages => (int)Math.Ceiling((double)FilteredProducts.Count / ItemsPerPage);

    // Mobile Filter State
    public bool ShowMobileFilters { get; set; }
    
    // Helper Properties
    private bool HasActiveFilters => CurrentFilters != null && !CurrentFilters.IsEmpty;
    private int ActiveFilterCount
    {
        get
        {
            if (CurrentFilters == null) return 0;
            int count = 0;
            count += CurrentFilters.Categories.Count;
            count += CurrentFilters.Brands.Count;
            if (CurrentFilters.MinRating > 0) count++;
            if (CurrentFilters.MinPrice > 0 || CurrentFilters.MaxPrice < 1000000) count++;
            if (CurrentFilters.OnSale) count++;
            if (CurrentFilters.FreeShipping) count++;
            if (!string.IsNullOrEmpty(CurrentFilters.SearchQuery)) count++;
            return count;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await MID_HelperFunctions.DebugMessageAsync(
            "🚀 Shop page initializing",
            LogLevel.Info
        );
        
        // Subscribe to state service events
        ShopState.OnSearchChanged += HandleSearchChanged;
        ShopState.OnFiltersChanged += HandleFiltersChangedFromState;
        
        // STEP 1: Load products FIRST (need to know what categories/brands exist)
        await LoadProducts();
        
        // STEP 2: Extract available categories/brands from products
        ExtractFilterOptionsFromProducts();
        
        // STEP 3: Load filters from ShopState
        CurrentFilters = await ShopState.GetCurrentFiltersAsync();
        SelectedSort = CurrentFilters.SortBy;
        
        // STEP 4: Validate filters against available options
        ValidateAndFixFilters();
        
        await MID_HelperFunctions.DebugMessageAsync(
            $"📋 Loaded filters: Categories=[{string.Join(", ", CurrentFilters.Categories)}], Search='{CurrentFilters.SearchQuery}'",
            LogLevel.Info
        );
        
        // STEP 5: Apply filters
        await ApplyFilters();
        
        AreFilterOptionsLoaded = true;
        IsInitialized = true;
        
        await MID_HelperFunctions.DebugMessageAsync(
            "✓ Shop page initialized successfully",
            LogLevel.Info
        );
    }

    private async Task LoadProducts()
    {
        IsLoading = true;
        HasError = false;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "📦 Loading all products",
                LogLevel.Info
            );

            AllProducts = await ProductService.GetAllProductsAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {AllProducts.Count} products",
                LogLevel.Info
            );
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

    /// <summary>
    /// Extract actual categories and brands that exist in products
    /// </summary>
    private void ExtractFilterOptionsFromProducts()
    {
        AvailableCategories = AllProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .Select(p => p.Category.Trim())
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        
        AvailableBrands = AllProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Brand))
            .Select(p => p.Brand.Trim())
            .Distinct()
            .OrderBy(b => b)
            .ToList();
        
        Console.WriteLine($"📊 Available categories: [{string.Join(", ", AvailableCategories)}]");
        Console.WriteLine($"📊 Available brands: [{string.Join(", ", AvailableBrands)}]");
    }

    /// <summary>
    /// Validate filters and remove invalid categories/brands
    /// </summary>
    private void ValidateAndFixFilters()
    {
        var originalCategoryCount = CurrentFilters.Categories.Count;
        var originalBrandCount = CurrentFilters.Brands.Count;
        
        // Remove categories that don't exist in products
        CurrentFilters.Categories = CurrentFilters.Categories
            .Where(c => AvailableCategories.Any(ac => 
                ac.Equals(c, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        
        // Remove brands that don't exist in products
        CurrentFilters.Brands = CurrentFilters.Brands
            .Where(b => AvailableBrands.Any(ab => 
                ab.Equals(b, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        
        if (originalCategoryCount != CurrentFilters.Categories.Count)
        {
            Console.WriteLine($"⚠ Removed {originalCategoryCount - CurrentFilters.Categories.Count} invalid categories");
        }
        
        if (originalBrandCount != CurrentFilters.Brands.Count)
        {
            Console.WriteLine($"⚠ Removed {originalBrandCount - CurrentFilters.Brands.Count} invalid brands");
        }
    }

    private async Task HandleFiltersChanged(FilterState filters)
    {
        if (!IsInitialized || !AllProducts.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠ Ignoring filter change - not initialized or no products",
                LogLevel.Warning
            );
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"🔧 Filters changed: {filters.Categories.Count} categories, {filters.Brands.Count} brands",
            LogLevel.Info
        );

        CurrentFilters = filters.Clone();
        SelectedSort = CurrentFilters.SortBy;
        
        // Validate before updating state
        ValidateAndFixFilters();
        
        // Update ShopState with validated filters
        await ShopState.UpdateFiltersAsync(CurrentFilters);
        
        CurrentPage = 1;
        await ApplyFilters();
        CloseMobileFilters();
    }

    private async Task HandleFiltersChangedFromState(FilterState filters)
    {
        // Don't apply if filter options aren't loaded yet
        if (!AreFilterOptionsLoaded)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⏳ Filter options not loaded yet, deferring filter application",
                LogLevel.Debug
            );
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"🔄 Filters changed from state: Categories=[{string.Join(", ", filters.Categories)}]",
            LogLevel.Info
        );
        
        CurrentFilters = filters.Clone();
        SelectedSort = CurrentFilters.SortBy;
        
        // Validate filters
        ValidateAndFixFilters();
        
        if (IsInitialized && AllProducts.Any())
        {
            CurrentPage = 1;
            await ApplyFilters();
        }
    }

    private async Task HandleSearchChanged(string query)
    {
        if (!IsInitialized || !AllProducts.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠ Ignoring search change - not initialized or no products",
                LogLevel.Warning
            );
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"🔍 Search query changed: '{query}'",
            LogLevel.Info
        );

        CurrentFilters.SearchQuery = query ?? "";
        
        CurrentPage = 1;
        await ApplyFilters();
    }

    private async Task HandleSortChanged()
    {
        if (!IsInitialized || !AllProducts.Any())
        {
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"📊 Sort changed to: {SelectedSort}",
            LogLevel.Info
        );
        
        CurrentFilters.SortBy = SelectedSort;
        await ShopState.UpdateFiltersAsync(CurrentFilters);
        
        ApplySorting();
        UpdateCurrentPageProducts();
        StateHasChanged();
    }

    private async Task ApplyFilters()
    {
        if (!AllProducts.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠ Cannot apply filters - no products loaded",
                LogLevel.Debug
            );
            FilteredProducts = new List<ProductViewModel>();
            CurrentPageProducts = new List<ProductViewModel>();
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"🎯 Applying filters - Categories: [{string.Join(", ", CurrentFilters.Categories)}], Search: '{CurrentFilters.SearchQuery}'",
            LogLevel.Info
        );

        // Start with all active products
        FilteredProducts = AllProducts
            .Where(p => p.IsActive && !string.IsNullOrEmpty(p.Name))
            .ToList();

        var startCount = FilteredProducts.Count;
        await MID_HelperFunctions.DebugMessageAsync(
            $"📦 Starting with {startCount} active products",
            LogLevel.Debug
        );

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(CurrentFilters.SearchQuery))
        {
            var query = CurrentFilters.SearchQuery.ToLower().Trim();
            FilteredProducts = FilteredProducts.Where(p =>
                (p.Name?.ToLower().Contains(query) ?? false) ||
                (p.Description?.ToLower().Contains(query) ?? false) ||
                (p.Brand?.ToLower().Contains(query) ?? false) ||
                (p.Category?.ToLower().Contains(query) ?? false)
            ).ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 After search filter: {FilteredProducts.Count} products (filtered by '{query}')",
                LogLevel.Debug
            );
        }

        // Apply category filter (CASE-INSENSITIVE)
        if (CurrentFilters.Categories.Any())
        {
            var beforeCategoryFilter = FilteredProducts.Count;
            
            FilteredProducts = FilteredProducts
                .Where(p => !string.IsNullOrEmpty(p.Category) && 
                           CurrentFilters.Categories.Any(filterCategory => 
                               p.Category.Equals(filterCategory, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"📂 After category filter: {FilteredProducts.Count} products (was {beforeCategoryFilter})",
                LogLevel.Debug
            );
            
            // Log which categories matched
            if (FilteredProducts.Any())
            {
                var matchedCategories = FilteredProducts
                    .Select(p => p.Category)
                    .Distinct()
                    .ToList();
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Matched categories: [{string.Join(", ", matchedCategories)}]",
                    LogLevel.Debug
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"⚠ No products matched categories: [{string.Join(", ", CurrentFilters.Categories)}]",
                    LogLevel.Warning
                );
                
                // FALLBACK: If category filter returns nothing, show all products
                await MID_HelperFunctions.DebugMessageAsync(
                    "🔄 Resetting to all products as fallback",
                    LogLevel.Info
                );
                FilteredProducts = AllProducts
                    .Where(p => p.IsActive && !string.IsNullOrEmpty(p.Name))
                    .ToList();
            }
        }

        // Apply brand filter (CASE-INSENSITIVE)
        if (CurrentFilters.Brands.Any())
        {
            var beforeBrandFilter = FilteredProducts.Count;
            
            FilteredProducts = FilteredProducts
                .Where(p => !string.IsNullOrEmpty(p.Brand) && 
                           CurrentFilters.Brands.Any(filterBrand => 
                               p.Brand.Equals(filterBrand, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🏷️ After brand filter: {FilteredProducts.Count} products (was {beforeBrandFilter})",
                LogLevel.Debug
            );
        }

        // Apply rating filter
        if (CurrentFilters.MinRating > 0)
        {
            var beforeRatingFilter = FilteredProducts.Count;
            
            FilteredProducts = FilteredProducts
                .Where(p => p.Rating >= CurrentFilters.MinRating)
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"⭐ After rating filter (>={CurrentFilters.MinRating}): {FilteredProducts.Count} products (was {beforeRatingFilter})",
                LogLevel.Debug
            );
        }

        // Apply price filter
        if (CurrentFilters.MinPrice > 0 || CurrentFilters.MaxPrice < 1000000)
        {
            var beforePriceFilter = FilteredProducts.Count;
            
            FilteredProducts = FilteredProducts
                .Where(p => p.Price >= CurrentFilters.MinPrice && p.Price <= CurrentFilters.MaxPrice)
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"💰 After price filter (₦{CurrentFilters.MinPrice}-₦{CurrentFilters.MaxPrice}): {FilteredProducts.Count} products (was {beforePriceFilter})",
                LogLevel.Debug
            );
        }

        // Apply sale filter
        if (CurrentFilters.OnSale)
        {
            var beforeSaleFilter = FilteredProducts.Count;
            
            FilteredProducts = FilteredProducts
                .Where(p => p.IsOnSale)
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔥 After sale filter: {FilteredProducts.Count} products (was {beforeSaleFilter})",
                LogLevel.Debug
            );
        }

        // Apply free shipping filter
        if (CurrentFilters.FreeShipping)
        {
            var beforeShippingFilter = FilteredProducts.Count;
            
            FilteredProducts = FilteredProducts
                .Where(p => p.Price >= 50000)
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🚚 After free shipping filter: {FilteredProducts.Count} products (was {beforeShippingFilter})",
                LogLevel.Debug
            );
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ Final filtered products: {FilteredProducts.Count} (started with {startCount})",
            LogLevel.Info
        );

        // Apply sorting
        ApplySorting();
        
        // Update pagination
        UpdateCurrentPageProducts();
        
        StateHasChanged();
    }

    private void ApplySorting()
    {
        var beforeSort = FilteredProducts.Count;
        
        FilteredProducts = SelectedSort switch
        {
            "price-asc" => FilteredProducts.OrderBy(p => p.Price).ToList(),
            "price-desc" => FilteredProducts.OrderByDescending(p => p.Price).ToList(),
            "rating-desc" => FilteredProducts.OrderByDescending(p => p.Rating).ToList(),
            "name-asc" => FilteredProducts.OrderBy(p => p.Name).ToList(),
            "newest" => FilteredProducts.OrderByDescending(p => p.CreatedAt).ToList(),
            _ => FilteredProducts.OrderBy(p => p.Id).ToList() // DEFAULT SORT
        };
        
        Console.WriteLine($"📊 Sorted {beforeSort} products by: {SelectedSort}");
    }

    private void UpdateCurrentPageProducts()
    {
        var skip = (CurrentPage - 1) * ItemsPerPage;
        CurrentPageProducts = FilteredProducts
            .Skip(skip)
            .Take(ItemsPerPage)
            .ToList();
        
        Console.WriteLine($"📄 Showing page {CurrentPage} of {TotalPages} ({CurrentPageProducts.Count} products)");
        
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
        if (!FilteredProducts.Any()) return "0";
        
        var start = (CurrentPage - 1) * ItemsPerPage + 1;
        var end = Math.Min(CurrentPage * ItemsPerPage, FilteredProducts.Count);
        return $"{start}-{end}";
    }

    private async Task HandleAddToCart(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"🛒 Add to cart: {productId}", LogLevel.Info);
    }

    private async Task HandleToggleFavorite(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"❤️ Toggle favorite: {productId}", LogLevel.Info);
    }

    private async Task ResetFilters()
    {
        await MID_HelperFunctions.DebugMessageAsync(
            "🔄 Resetting filters",
            LogLevel.Info
        );
        
        await ShopState.ResetFiltersAsync();
        CurrentFilters = FilterState.CreateDefault();
        SelectedSort = "default";
        CurrentPage = 1;
        
        await ApplyFilters();
        StateHasChanged();
    }

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

    public void Dispose()
    {
        ShopState.OnSearchChanged -= HandleSearchChanged;
        ShopState.OnFiltersChanged -= HandleFiltersChangedFromState;
    }
}