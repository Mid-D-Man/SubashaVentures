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

    protected override async Task OnInitializedAsync()
    {
        await LoadFilterOptions();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Update UI to match CurrentFilters whenever it changes
        if (CurrentFilters != null)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"📋 Updating filter panel UI to match current filters",
                LogLevel.Debug
            );
            
            SelectedCategories = new List<string>(CurrentFilters.Categories);
            SelectedBrands = new List<string>(CurrentFilters.Brands);
            MinRating = CurrentFilters.MinRating;
            MinPriceText = CurrentFilters.MinPrice > 0 ? CurrentFilters.MinPrice.ToString() : "";
            MaxPriceText = CurrentFilters.MaxPrice < 1000000 ? CurrentFilters.MaxPrice.ToString() : "";
            OnSale = CurrentFilters.OnSale;
            FreeShipping = CurrentFilters.FreeShipping;
            
            StateHasChanged();
        }
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

            if (!Categories.Any())
            {
                Categories = new List<string> 
                { 
                    "Women Shoes", "Men Shoes", "Lifestyle", 
                    "Skateboarding", "Running", "Sports" 
                };
            }

            if (!Brands.Any())
            {
                Brands = new List<string> 
                { 
                    "Nike", "Adidas", "Puma", "New Balance", 
                    "Converse", "Vans", "Reebok" 
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {Categories.Count} categories and {Brands.Count} brands for filter panel",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading filter options");
            
            Categories = new List<string> 
            { 
                "Women Shoes", "Men Shoes", "Lifestyle", 
                "Skateboarding", "Running", "Sports" 
            };
            
            Brands = new List<string> 
            { 
                "Nike", "Adidas", "Puma", "New Balance", 
                "Converse", "Vans", "Reebok" 
            };
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private bool IsChecked(List<string> list, string value)
    {
        return list.Contains(value);
    }

    private void ToggleCategory(string category)
    {
        if (SelectedCategories.Contains(category))
            SelectedCategories.Remove(category);
        else
            SelectedCategories.Add(category);
        
        StateHasChanged();
    }

    private void ToggleBrand(string brand)
    {
        if (SelectedBrands.Contains(brand))
            SelectedBrands.Remove(brand);
        else
            SelectedBrands.Add(brand);
        
        StateHasChanged();
    }

    private void SetMinRating(int rating)
    {
        MinRating = rating;
        StateHasChanged();
    }

    private void ToggleOnSale()
    {
        OnSale = !OnSale;
        StateHasChanged();
    }

    private void ToggleFreeShipping()
    {
        FreeShipping = !FreeShipping;
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

        if (OnFiltersChanged.HasDelegate)
        {
            await OnFiltersChanged.InvokeAsync(filters);
        }
        
        StateHasChanged();
    }
}
