using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Shop;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Brands;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Layout.Shop;

public partial class ShopFilterPanel : ComponentBase
{
    [Parameter] public EventCallback<FilterState> OnFiltersChanged { get; set; }
    [Parameter] public FilterState? CurrentFilters { get; set; }
    
    [Inject] private ICategoryService CategoryService { get; set; } = null!;
    [Inject] private IBrandService BrandService { get; set; } = null!;

    private List<string> Categories = new();
    private List<string> Brands = new();
    
    private List<string> SelectedCategories = new();
    private List<string> SelectedBrands = new();
    
    private string MinPriceText = "";
    private string MaxPriceText = "";
    
    private int MinRating = 0;
    private bool OnSale = false;
    private bool FreeShipping = false;
    
    private bool IsLoading = true;
    
    // ✅ FIX: Track last synced state to avoid re-syncing unnecessarily
    private FilterState? LastSyncedFilters = null;

    protected override async Task OnInitializedAsync()
    {
        await LoadFilterOptions();
        
        // Sync with current filters after loading options
        if (CurrentFilters != null)
        {
            SyncWithCurrentFilters();
        }
    }

    protected override void OnParametersSet()
    {
        // ✅ FIX: Only sync if CurrentFilters actually changed
        if (CurrentFilters != null && !IsLoading)
        {
            // Check if filters actually changed
            if (LastSyncedFilters == null || !FiltersAreEqual(LastSyncedFilters, CurrentFilters))
            {
                Console.WriteLine($"🔄 ShopFilterPanel: CurrentFilters changed, syncing UI");
                SyncWithCurrentFilters();
            }
        }
    }

    /// <summary>
    /// Check if two filter states are equal
    /// </summary>
    private bool FiltersAreEqual(FilterState a, FilterState b)
    {
        if (a == null || b == null) return false;
        
        return a.Categories.SequenceEqual(b.Categories) &&
               a.Brands.SequenceEqual(b.Brands) &&
               a.MinRating == b.MinRating &&
               a.MinPrice == b.MinPrice &&
               a.MaxPrice == b.MaxPrice &&
               a.OnSale == b.OnSale &&
               a.FreeShipping == b.FreeShipping &&
               a.SearchQuery == b.SearchQuery &&
               a.SortBy == b.SortBy;
    }

    /// <summary>
    /// Sync UI state with CurrentFilters
    /// </summary>
    private void SyncWithCurrentFilters()
    {
        if (CurrentFilters == null) return;
        
        Console.WriteLine($"🔄 Syncing filter panel with current filters");
        Console.WriteLine($"   Categories: [{string.Join(", ", CurrentFilters.Categories)}]");
        Console.WriteLine($"   Brands: [{string.Join(", ", CurrentFilters.Brands)}]");
        
        SelectedCategories = new List<string>(CurrentFilters.Categories);
        SelectedBrands = new List<string>(CurrentFilters.Brands);
        MinRating = CurrentFilters.MinRating;
        MinPriceText = CurrentFilters.MinPrice > 0 ? CurrentFilters.MinPrice.ToString() : "";
        MaxPriceText = CurrentFilters.MaxPrice < 1000000 ? CurrentFilters.MaxPrice.ToString() : "";
        OnSale = CurrentFilters.OnSale;
        FreeShipping = CurrentFilters.FreeShipping;
        
        // ✅ FIX: Store last synced state
        LastSyncedFilters = CurrentFilters.Clone();
        
        StateHasChanged();
    }

    private async Task LoadFilterOptions()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            var categories = await CategoryService.GetAllCategoriesAsync();
            Categories = categories
                .Select(c => c.Name)
                .OrderBy(n => n)
                .ToList();

            var brands = await BrandService.GetAllBrandsAsync();
            Brands = brands
                .Select(b => b.Name)
                .OrderBy(n => n)
                .ToList();

            // Fallback defaults
            if (!Categories.Any())
            {
                Categories = new List<string> 
                { 
                    "Womens Clothing", "Mens Clothing", "Kids Clothing", "Baby Essentials"
                };
            }

            if (!Brands.Any())
            {
                Brands = new List<string> 
                { 
                    "SubashaVentures", "Premium Collection"
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {Categories.Count} categories and {Brands.Count} brands for filter panel",
                LogLevel.Info
            );
            
            Console.WriteLine($"📋 Available categories: [{string.Join(", ", Categories)}]");
            Console.WriteLine($"📋 Available brands: [{string.Join(", ", Brands)}]");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading filter options");
            
            // Use fallback defaults
            Categories = new List<string> 
            { 
                "Womens Clothing", "Mens Clothing", "Kids Clothing", "Baby Essentials"
            };
            
            Brands = new List<string> 
            { 
                "SubashaVentures", "Premium Collection"
            };
        }
        finally
        {
            IsLoading = false;
            
            // Sync after loading
            if (CurrentFilters != null)
            {
                SyncWithCurrentFilters();
            }
            
            StateHasChanged();
        }
    }

    private bool IsChecked(List<string> list, string value)
    {
        return list.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private void ToggleCategory(string category)
    {
        // Case-insensitive toggle
        var existing = SelectedCategories.FirstOrDefault(c => 
            c.Equals(category, StringComparison.OrdinalIgnoreCase));
        
        if (existing != null)
        {
            SelectedCategories.Remove(existing);
            Console.WriteLine($"❌ Removed category: {category}");
        }
        else
        {
            SelectedCategories.Add(category);
            Console.WriteLine($"✅ Added category: {category}");
        }
        
        Console.WriteLine($"📂 Selected categories: [{string.Join(", ", SelectedCategories)}]");
        StateHasChanged();
    }

    private void ToggleBrand(string brand)
    {
        // Case-insensitive toggle
        var existing = SelectedBrands.FirstOrDefault(b => 
            b.Equals(brand, StringComparison.OrdinalIgnoreCase));
        
        if (existing != null)
        {
            SelectedBrands.Remove(existing);
            Console.WriteLine($"❌ Removed brand: {brand}");
        }
        else
        {
            SelectedBrands.Add(brand);
            Console.WriteLine($"✅ Added brand: {brand}");
        }
        
        Console.WriteLine($"🏷️ Selected brands: [{string.Join(", ", SelectedBrands)}]");
        StateHasChanged();
    }

    private void SetMinRating(int rating)
    {
        MinRating = rating;
        Console.WriteLine($"⭐ Min rating set to: {rating}");
        StateHasChanged();
    }

    private void ToggleOnSale()
    {
        OnSale = !OnSale;
        Console.WriteLine($"🔥 On sale filter: {OnSale}");
        StateHasChanged();
    }

    private void ToggleFreeShipping()
    {
        FreeShipping = !FreeShipping;
        Console.WriteLine($"🚚 Free shipping filter: {FreeShipping}");
        StateHasChanged();
    }

    private async Task ApplyFilters()
    {
        var minPrice = 0m;
        var maxPrice = 1000000m;

        if (decimal.TryParse(MinPriceText, out var parsedMin))
            minPrice = parsedMin;

        if (decimal.TryParse(MaxPriceText, out var parsedMax))
            maxPrice = parsedMax;

        var filters = new FilterState
        {
            Categories = new List<string>(SelectedCategories),
            Brands = new List<string>(SelectedBrands),
            MinRating = MinRating,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            OnSale = OnSale,
            FreeShipping = FreeShipping,
            SearchQuery = CurrentFilters?.SearchQuery ?? "",
            SortBy = CurrentFilters?.SortBy ?? "default"
        };

        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ Applying filters: {filters.Categories.Count} categories, {filters.Brands.Count} brands",
            LogLevel.Info
        );
        
        Console.WriteLine($"📤 Sending filters to Shop page: Categories=[{string.Join(", ", filters.Categories)}]");

        // ✅ FIX: Update last synced state before invoking callback
        LastSyncedFilters = filters.Clone();

        if (OnFiltersChanged.HasDelegate)
        {
            await OnFiltersChanged.InvokeAsync(filters);
        }
    }

    private async Task ResetFilters()
    {
        SelectedCategories.Clear();
        SelectedBrands.Clear();
        MinPriceText = "";
        MaxPriceText = "";
        MinRating = 0;
        OnSale = false;
        FreeShipping = false;

        var filters = new FilterState
        {
            Categories = new List<string>(),
            Brands = new List<string>(),
            MinRating = 0,
            MinPrice = 0,
            MaxPrice = 1000000,
            OnSale = false,
            FreeShipping = false,
            SearchQuery = "",
            SortBy = "default"
        };

        await MID_HelperFunctions.DebugMessageAsync(
            "🔄 Resetting all filters",
            LogLevel.Info
        );

        // ✅ FIX: Update last synced state before invoking callback
        LastSyncedFilters = filters.Clone();

        if (OnFiltersChanged.HasDelegate)
        {
            await OnFiltersChanged.InvokeAsync(filters);
        }
        
        StateHasChanged();
    }
}
