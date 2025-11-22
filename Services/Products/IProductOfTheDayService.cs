// Services/Products/IProductOfTheDayService.cs


using SubashaVentures.Domain.Product;

namespace SubashaVentures.Services.Products;

public interface IProductOfTheDayService
{
    Task<ProductViewModel?> GetProductOfTheDayAsync();
    Task<bool> SetProductOfTheDayAsync(int productId);
    Task<bool> AutoSelectProductOfTheDayAsync();
    Task<DateTime?> GetLastUpdateTimeAsync();
}