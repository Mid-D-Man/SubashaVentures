namespace SubashaVentures.Domain.Shop;

public class FilterState
{
    public List<string> Categories { get; set; } = new();
    public List<string> SubCategories { get; set; } = new();
    public List<string> Brands { get; set; } = new();
    public int MinRating { get; set; } = 0;
    public decimal MinPrice { get; set; } = 0;
    public decimal MaxPrice { get; set; } = 1000000;
    public bool OnSale { get; set; } = false;
    public bool FreeShipping { get; set; } = false;
    public string SearchQuery { get; set; } = "";
    public string SortBy { get; set; } = "default";
    public DateTime? LastUpdated { get; set; }

    public bool IsEmpty =>
        !Categories.Any() &&
        !SubCategories.Any() &&
        !Brands.Any() &&
        MinRating == 0 &&
        MinPrice == 0 &&
        MaxPrice == 1000000 &&
        !OnSale &&
        !FreeShipping &&
        string.IsNullOrEmpty(SearchQuery);

    public static FilterState CreateDefault() => new();

    public FilterState Clone() => new()
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
        SortBy = SortBy,
        LastUpdated = LastUpdated
    };
}
