// Domain/Shop/FilterState.cs
namespace SubashaVentures.Domain.Shop;

/// <summary>
/// Represents the current filter state for the shop page
/// </summary>
public class FilterState
{
    public List<string> Categories { get; set; } = new();
    public List<string> Brands { get; set; } = new();
    public int MinRating { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public bool OnSale { get; set; }
    public bool FreeShipping { get; set; }
}
