namespace SubashaVentures.Domain.Shop;

public class FilterState
{
    public List<string> Categories { get; set; } = new();
    public List<string> SubCategories { get; set; } = new();
    public List<string> Brands { get; set; } = new();

    public int MinRating { get; set; } = 0;
    public decimal MinPrice { get; set; } = 0m;
    public decimal MaxPrice { get; set; } = 1_000_000m;

    public bool OnSale { get; set; } = false;
    public bool FreeShipping { get; set; } = false;

    public string SearchQuery { get; set; } = string.Empty;
    public string SortBy { get; set; } = "default";

    // ==================== COMPUTED ====================

    public bool IsEmpty =>
        !Categories.Any() &&
        !SubCategories.Any() &&
        !Brands.Any() &&
        MinRating == 0 &&
        MinPrice == 0 &&
        MaxPrice >= 1_000_000m &&
        !OnSale &&
        !FreeShipping &&
        string.IsNullOrEmpty(SearchQuery) &&
        (SortBy == "default" || string.IsNullOrEmpty(SortBy));

    // ==================== METHODS ====================

    public FilterState Clone() => new FilterState
    {
        Categories = new List<string>(Categories),
        SubCategories = new List<string>(SubCategories),
        Brands = new List<string>(Brands),
        MinRating = MinRating,
        MinPrice = MinPrice,
        MaxPrice = MaxPrice,
        OnSale = OnSale,
        FreeShipping = FreeShipping,
        SearchQuery = SearchQuery,
        SortBy = SortBy
    };

    public static FilterState CreateDefault() => new FilterState();

    /// <summary>
    /// Returns a new FilterState with only the SearchQuery carried over.
    /// Useful when navigating to shop with a search term pre-filled.
    /// </summary>
    public static FilterState FromSearch(string query) => new FilterState
    {
        SearchQuery = query ?? string.Empty
    };

    /// <summary>
    /// Returns a new FilterState with only the given category selected.
    /// </summary>
    public static FilterState FromCategory(string categoryName) => new FilterState
    {
        Categories = new List<string> { categoryName }
    };
}
