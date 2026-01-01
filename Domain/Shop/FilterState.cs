// Domain/Shop/FilterState.cs - ENHANCED FILTER STATE
namespace SubashaVentures.Domain.Shop;

/// <summary>
/// Represents the complete filter state for the shop page
/// Serializable for local storage persistence
/// </summary>
public class FilterState
{
    public List<string> Categories { get; set; } = new();
    public List<string> Brands { get; set; } = new();
    public int MinRating { get; set; } = 0;
    public decimal MinPrice { get; set; } = 0;
    public decimal MaxPrice { get; set; } = 1000000;
    public bool OnSale { get; set; } = false;
    public bool FreeShipping { get; set; } = false;
    public string SearchQuery { get; set; } = "";
    public string SortBy { get; set; } = "default";
    
    // Metadata
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public bool IsEmpty => !Categories.Any() && 
                           !Brands.Any() && 
                           MinRating == 0 && 
                           MinPrice == 0 && 
                           MaxPrice >= 1000000 && 
                           !OnSale && 
                           !FreeShipping &&
                           string.IsNullOrEmpty(SearchQuery);
    
    /// <summary>
    /// Create a default empty filter state
    /// </summary>
    public static FilterState CreateDefault()
    {
        return new FilterState
        {
            Categories = new List<string>(),
            Brands = new List<string>(),
            MinRating = 0,
            MinPrice = 0,
            MaxPrice = 1000000,
            OnSale = false,
            FreeShipping = false,
            SearchQuery = "",
            SortBy = "default",
            LastUpdated = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Create a deep copy of this filter state
    /// </summary>
    public FilterState Clone()
    {
        return new FilterState
        {
            Categories = new List<string>(this.Categories),
            Brands = new List<string>(this.Brands),
            MinRating = this.MinRating,
            MinPrice = this.MinPrice,
            MaxPrice = this.MaxPrice,
            OnSale = this.OnSale,
            FreeShipping = this.FreeShipping,
            SearchQuery = this.SearchQuery,
            SortBy = this.SortBy,
            LastUpdated = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Reset to default values
    /// </summary>
    public void Reset()
    {
        Categories.Clear();
        Brands.Clear();
        MinRating = 0;
        MinPrice = 0;
        MaxPrice = 1000000;
        OnSale = false;
        FreeShipping = false;
        SearchQuery = "";
        SortBy = "default";
        LastUpdated = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Check if two filter states are equivalent
    /// </summary>
    public bool Equals(FilterState? other)
    {
        if (other == null) return false;
        
        return Categories.SequenceEqual(other.Categories) &&
               Brands.SequenceEqual(other.Brands) &&
               MinRating == other.MinRating &&
               MinPrice == other.MinPrice &&
               MaxPrice == other.MaxPrice &&
               OnSale == other.OnSale &&
               FreeShipping == other.FreeShipping &&
               SearchQuery == other.SearchQuery &&
               SortBy == other.SortBy;
    }
}
