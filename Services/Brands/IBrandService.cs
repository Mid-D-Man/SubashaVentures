// Services/Brands/IBrandService.cs
using SubashaVentures.Models.Firebase;

namespace SubashaVentures.Services.Brands;

public interface IBrandService
{
    Task<List<BrandModel>> GetAllBrandsAsync();
    Task<BrandModel?> GetBrandByIdAsync(string brandId);
    Task<BrandModel?> GetBrandByNameAsync(string name);
    Task<string> CreateBrandAsync(CreateBrandRequest request);
    Task<bool> UpdateBrandAsync(string brandId, UpdateBrandRequest request);
    Task<bool> DeleteBrandAsync(string brandId);
    Task<int> GetProductCountForBrandAsync(string brandId);
}

// DTOs
public class CreateBrandRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsFeatured { get; set; }
}

public class UpdateBrandRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFeatured { get; set; }
}
