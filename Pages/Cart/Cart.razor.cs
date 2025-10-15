using Microsoft.AspNetCore.Components;

namespace SubashaVentures.Pages.Cart;

public partial class Cart : ComponentBase
{
    private List<CartItemViewModel> cartItems = new();
    private string promoCode = "";
    
    // Calculated values
    private decimal subtotal => cartItems.Sum(item => item.Price * item.Quantity);
    private decimal shippingCost => subtotal >= 50000 ? 0 : 2000; // Free shipping over ₦50,000
    private decimal discount = 0;
    private decimal total => subtotal + shippingCost - discount;

    protected override async Task OnInitializedAsync()
    {
        await LoadCart();
    }

    private async Task LoadCart()
    {
        // Simulate loading cart from localStorage or API
        await Task.Delay(100);
        
        // In real app: cartItems = await CartService.GetCartItems();
        
        // Sample cart data
        cartItems = new List<CartItemViewModel>
        {
            new CartItemViewModel
            {
                Id = "1",
                ProductId = "prod-001",
                Name = "Premium Cotton T-Shirt",
                ImageUrl = "https://via.placeholder.com/120x120?text=Product+1",
                Price = 8500,
                Quantity = 2,
                Size = "M",
                Color = "#000000",
                Stock = 45
            },
            new CartItemViewModel
            {
                Id = "2",
                ProductId = "prod-002",
                Name = "Classic Denim Jeans",
                ImageUrl = "https://via.placeholder.com/120x120?text=Product+2",
                Price = 15000,
                Quantity = 1,
                Size = "32",
                Color = "#1E40AF",
                Stock = 23
            },
            new CartItemViewModel
            {
                Id = "3",
                ProductId = "prod-003",
                Name = "Leather Jacket",
                ImageUrl = "https://via.placeholder.com/120x120?text=Product+3",
                Price = 45000,
                Quantity = 1,
                Size = "L",
                Color = "#000000",
                Stock = 8
            }
        };
    }

    private async Task IncreaseQuantity(string itemId)
    {
        var item = cartItems.FirstOrDefault(i => i.Id == itemId);
        if (item != null && item.Quantity < item.Stock)
        {
            item.Quantity++;
            await UpdateCart();
        }
    }

    private async Task DecreaseQuantity(string itemId)
    {
        var item = cartItems.FirstOrDefault(i => i.Id == itemId);
        if (item != null && item.Quantity > 1)
        {
            item.Quantity--;
            await UpdateCart();
        }
    }

    private async Task RemoveItem(string itemId)
    {
        var item = cartItems.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            cartItems.Remove(item);
            await UpdateCart();
            
            Console.WriteLine($"Removed {item.Name} from cart");
        }
    }

    private async Task UpdateCart()
    {
        // In real app: await CartService.UpdateCart(cartItems);
        
        // Update localStorage or database
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task ApplyPromoCode()
    {
        if (string.IsNullOrWhiteSpace(promoCode))
        {
            return;
        }

        // Simulate promo code validation
        await Task.Delay(500);
        
        // In real app: var result = await PromoService.ValidatePromoCode(promoCode);
        
        // Sample promo codes
        switch (promoCode.ToUpper())
        {
            case "SAVE10":
                discount = subtotal * 0.10m; // 10% off
                Console.WriteLine("Promo code applied: 10% off");
                break;
            case "WELCOME":
                discount = 5000; // ₦5,000 off
                Console.WriteLine("Promo code applied: ₦5,000 off");
                break;
            case "FREESHIP":
                // This would be handled separately in shipping calculation
                Console.WriteLine("Free shipping applied");
                break;
            default:
                Console.WriteLine("Invalid promo code");
                discount = 0;
                break;
        }
        
        StateHasChanged();
    }

    private async Task ProceedToCheckout()
    {
        if (!cartItems.Any())
        {
            return;
        }

        try
        {
            // Validate cart items
            foreach (var item in cartItems)
            {
                if (item.Stock <= 0)
                {
                    Console.WriteLine($"{item.Name} is out of stock");
                    return;
                }
                
                if (item.Quantity > item.Stock)
                {
                    Console.WriteLine($"Only {item.Stock} units of {item.Name} available");
                    return;
                }
            }

            // Navigate to checkout
            // NavigationManager.NavigateTo("/checkout");
            
            Console.WriteLine("Proceeding to checkout");
            Console.WriteLine($"Total items: {cartItems.Count}");
            Console.WriteLine($"Total amount: ₦{total:N0}");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error proceeding to checkout: {ex.Message}");
        }
    }

    // View Model
    public class CartItemViewModel
    {
        public string Id { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string Name { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public int Stock { get; set; }
    }
}
