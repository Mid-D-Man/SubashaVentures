namespace SubashaVentures.Domain.Product;

public class CategoryViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? IconEmoji { get; set; }
    public string? ParentId { get; set; }
    public int ProductCount { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CategoryViewModel> SubCategories { get; set; } = new();
}
