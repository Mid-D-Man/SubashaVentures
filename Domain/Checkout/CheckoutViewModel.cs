// Domain/Checkout/CheckoutViewModel.cs - UPDATED WITH SKU
using SubashaVentures.Domain.Cart;
using SubashaVentures.Domain.User;
using SubashaVentures.Domain.Order;

namespace SubashaVentures.Domain.Checkout;

public class CheckoutViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public AddressViewModel? ShippingAddress { get; set; }
    public AddressViewModel? BillingAddress { get; set; }
    public bool UseSameAddressForBilling { get; set; } = true;
    
    public string? PromoCode { get; set; }
    public decimal PromoDiscount { get; set; }
    
    public string ShippingMethod { get; set; } = "Standard";
    public decimal ShippingCost { get; set; }
    
    // Shipping rate ID (from Terminal Africa or other provider)
    public string? ShippingRateId { get; set; }
    
    public PaymentMethod PaymentMethod { get; set; }
    
    public decimal Subtotal => Items.Sum(i => i.Subtotal);
    public decimal Total => Subtotal + ShippingCost - PromoDiscount;
    
    // Partner commission breakdown
    public decimal StoreRevenue { get; set; }
    public decimal PartnerCommissions { get; set; }
    public Dictionary<Guid, decimal>? PartnerBreakdown { get; set; }
    
    public string? CustomerNotes { get; set; }
    
    public bool IsValid => Items.Any() && ShippingAddress != null;
}

// Checkout-specific item model (includes SKU)
public class CheckoutItemViewModel
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string Sku { get; set; } = string.Empty; // SKU field added
    public decimal Subtotal => Price * Quantity;
}
