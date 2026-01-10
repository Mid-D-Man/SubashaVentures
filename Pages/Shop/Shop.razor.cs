using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Shop;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Brands;
using SubashaVentures.Services.Shop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase, IDisposable
{
    [Inject] private IProductService ProductService { get; set; } = null!;
    [Inject] private ICategoryService CategoryService { get; set; } = null!;
    [Inject] private IBrandService BrandService { get; set; } = null!;
    [Inject] private ShopStateService ShopState { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    
    // Data
    private List<ProductViewModel> AllProducts { get; set; } = new();
    private List<ProductViewModel> FilteredProducts { get; set; } = new();
    private List<ProductViewModel> CurrentPageProducts { get; set; } = new();
    
    // ✅ FIREBASE AUTHORITATIVE SOURCES (NOT from products!)
    private List<CategoryViewModel> FirebaseCategories { get; set; } = new();
    private List<string> AvailableCategories { get; set; } = new();
    private List<string> AvailableBrands { get; set; } = new();
    
    // State
    private bool IsLoading { get; set; } = true;
    private bool HasError { get; set; }
    private string ErrorMessage { get; set; } = "";
    private bool IsInitialized { get; set; } = false;
    
    // ✅ Explicit initialization stages
    private bool AreCategoriesLoaded { get; set; } = false;
    private bool AreBrandsLoaded { get; set; } = false;
    private bool AreProductsLoaded { get; set; } = false;
    
    // Current Filters
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
        
        IsLoading = true;
        
        try
        {
            // ✅ STEP 1: Subscribe to events FIRST
            ShopState.OnSearchChanged += HandleSearchChanged;
            ShopState.OnFiltersChanged += HandleFiltersChangedFromState;
            
            // ✅ STEP 2: Load FIREBASE categories/brands FIRST (authoritative source)
            await LoadFirebaseCategoriesAndBrands();
            
            // ✅ STEP 3: Load products
            await LoadProducts();
            
            // ✅ STEP 4: NOW load filters from localStorage (categories are ready to validate against)
            var stateFilters = await ShopState.GetCurrentFiltersAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"📋 Initial filters from state: Categories=[{string.Join(", ", stateFilters.Categories)}], Search='{stateFilters.SearchQuery}'",
                LogLevel.Info
            );
            
            CurrentFilters = stateFilters;
            SelectedSort = CurrentFilters.SortBy;
            
            // ✅ STEP 5: Validate filters against FIREBASE categories (exact match only)
            ValidateFiltersAgainstFirebase();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"📋 Validated filters: Categories=[{string.Join(", ", CurrentFilters.Categories)}], Search='{CurrentFilters.SearchQuery}'",
                LogLevel.Info
            );
            
            // ✅ STEP 6: Apply filters
            await ApplyFilters();
            
            IsInitialized = true;
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Shop page initialized successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = "Failed to initialize shop";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Shop initialization");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// ✅ Load categories and brands from FIREBASE (authoritative source)
    /// </summary>
    private async Task LoadFirebaseCategoriesAndBrands()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "📦 Loading categories and brands from Firebase",
                LogLevel.Info
            );
            
            // Load from Firebase
            FirebaseCategories = await CategoryService.GetAllCategoriesAsync();
            var firebaseBrands = await BrandService.GetAllBrandsAsync();
            
            // Extract names (these are the CORRECT names like "Mens Clothing")
            AvailableCategories = FirebaseCategories
                .Where(c => c.IsActive)
                .Select(c => c.Name.Trim())
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            
            AvailableBrands = firebaseBrands
                .Where(b => b.IsActive)
                .Select(b => b.Name.Trim())
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            
            AreCategoriesLoaded = true;
            AreBrandsLoaded = true;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {AvailableCategories.Count} Firebase categories: [{string.Join(", ", AvailableCategories)}]",
                LogLevel.Info
            );
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {AvailableBrands.Count} Firebase brands: [{string.Join(", ", AvailableBrands)}]",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading Firebase categories/brands");
            
            // NO FALLBACKS - if Firebase fails, we have bigger problems
            AvailableCategories = new List<string>();
            AvailableBrands = new List<string>();
        }
    }

    private async Task LoadProducts()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "📦 Loading all products",
                LogLevel.Info
            );

            AllProducts = await ProductService.GetAllProductsAsync();
            AreProductsLoaded = true;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {AllProducts.Count} products",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading products");
            AllProducts = new List<ProductViewModel>();
        }
    }

    /// <summary>
    /// ✅ Validate filters against FIREBASE categories (EXACT match only, no fuzzy matching!)
    /// </summary>
    private void ValidateFiltersAgainstFirebase()
    {
        if (!AreCategoriesLoaded || !AreBrandsLoaded)
        {
            Console.WriteLine("⚠️ Cannot validate - Firebase data not loaded yet");
            return;
        }
        
        var originalCategoryCount = CurrentFilters.Categories.Count;
        var originalBrandCount = CurrentFilters.Brands.Count;
        
        Console.WriteLine($"📊 Validating against Firebase categories: [{string.Join(", ", AvailableCategories)}]");
        Console.WriteLine($"📊 Current filter categories: [{string.Join(", ", CurrentFilters.Categories)}]");
        
        // ✅ EXACT MATCH ONLY - no fuzzy matching that corrupts category names!
        var validatedCategories = CurrentFilters.Categories
            .Where(filterCat => AvailableCategories.Any(fbCat => 
                fbCat.Equals(filterCat, StringComparison.Ordinal))) // EXACT match
            .ToList();
        
        var validatedBrands = CurrentFilters.Brands
            .Where(filterBrand => AvailableBrands.Any(fbBrand => 
                fbBrand.Equals(filterBrand, StringComparison.Ordinal))) // EXACT match
            .ToList();
        
        CurrentFilters.Categories = validatedCategories;
        CurrentFilters.Brands = validatedBrands;
        
        if (originalCategoryCount != CurrentFilters.Categories.Count)
        {
            Console.WriteLine($"⚠️ Removed {originalCategoryCount - CurrentFilters.Categories.Count} invalid categories");
        }
        
        if (originalBrandCount != CurrentFilters.Brands.Count)
        {
            Console.WriteLine($"⚠️ Removed {originalBrandCount - CurrentFilters.Brands.Count} invalid brands");
        }
        
        Console.WriteLine($"✅ Validated categories: [{string.Join(", ", CurrentFilters.Categories)}]");
    }

    private async Task HandleFiltersChanged(FilterState filters)
    {
        if (!IsInitialized)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠️ Ignoring filter change - not initialized",
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
        
        // Validate against Firebase
        ValidateFiltersAgainstFirebase();
        
        // Update ShopState
        await ShopState.UpdateFiltersAsync(CurrentFilters);
        
        CurrentPage = 1;
        await ApplyFilters();
        CloseMobileFilters();
    }

    private async Task HandleFiltersChangedFromState(FilterState filters)
    {
        if (!AreCategoriesLoaded || !AreBrandsLoaded)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"⏳ Firebase data not loaded yet, ignoring state update",
                LogLevel.Info
            );
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"🔄 Filters changed from state: Categories=[{string.Join(", ", filters.Categories)}]",
            LogLevel.Info
        );
        
        CurrentFilters = filters.Clone();
        SelectedSort = CurrentFilters.SortBy;
        
        // Validate against Firebase
        ValidateFiltersAgainstFirebase();
        
        if (IsInitialized)
        {
            CurrentPage = 1;
            await ApplyFilters();
        }
    }

    private async Task HandleSearchChanged(string query)
    {
        if (!IsInitialized)
        {
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
        if (!IsInitialized)
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
        if (!AreProductsLoaded)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠️ Cannot apply filters - products not loaded",
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
                $"🔍 After search filter: {FilteredProducts.Count} products",
                LogLevel.Debug
            );
        }

        // ✅ Apply category filter - EXACT MATCH against Firebase categories
        if (CurrentFilters.Categories.Any())
        {
            var beforeCategoryFilter = FilteredProducts.Count;
            
            FilteredProducts = FilteredProducts
                .Where(p => !string.IsNullOrEmpty(p.Category) && 
                           CurrentFilters.Categories.Contains(p.Category, StringComparer.Ordinal)) // EXACT match
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"📂 After category filter: {FilteredProducts.Count} products (was {beforeCategoryFilter})",
                LogLevel.Debug
            );
            
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
                    $"⚠️ No products matched categories: [{string.Join(", ", CurrentFilters.Categories)}]",
                    LogLevel.Warning
                );
            }
        }

        // Apply brand filter
        if (CurrentFilters.Brands.Any())
        {
            var beforeBrandFilter = FilteredProducts.Count;
            
            FilteredProducts = FilteredProducts
                .Where(p => !string.IsNullOrEmpty(p.Brand) && 
                           CurrentFilters.Brands.Contains(p.Brand, StringComparer.Ordinal)) // EXACT match
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🏷️ After brand filter: {FilteredProducts.Count} products (was {beforeBrandFilter})",
                LogLevel.Debug
            );
        }

        // Apply rating filter
        if (CurrentFilters.MinRating > 0)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.Rating >= CurrentFilters.MinRating)
                .ToList();
        }

        // Apply price filter
        if (CurrentFilters.MinPrice > 0 || CurrentFilters.MaxPrice < 1000000)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.Price >= CurrentFilters.MinPrice && p.Price <= CurrentFilters.MaxPrice)
                .ToList();
        }

        // Apply sale filter
        if (CurrentFilters.OnSale)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.IsOnSale)
                .ToList();
        }

        // Apply free shipping filter
        if (CurrentFilters.FreeShipping)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.Price >= 50000)
                .ToList();
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
        FilteredProducts = SelectedSort switch
        {
            "price-asc" => FilteredProducts.OrderBy(p => p.Price).ToList(),
            "price-desc" => FilteredProducts.OrderByDescending(p => p.Price).ToList(),
            "rating-desc" => FilteredProducts.OrderByDescending(p => p.Rating).ToList(),
            "name-asc" => FilteredProducts.OrderBy(p => p.Name).ToList(),
            "newest" => FilteredProducts.OrderByDescending(p => p.CreatedAt).ToList(),
            _ => FilteredProducts.OrderBy(p => p.Id).ToList()
        };
        
        Console.WriteLine($"📊 Sorted {FilteredProducts.Count} products by: {SelectedSort}");
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
