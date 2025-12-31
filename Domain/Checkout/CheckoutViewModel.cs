

// ===== Domain/Checkout/CheckoutViewModel.cs =====

using SubashaVentures.Domain.Order;

namespace SubashaVentures.Domain.Checkout;

public class CheckoutViewModel
{
    public List<SubashaVentures.Domain.Cart.CartItemViewModel> Items { get; set; } = new();
    public SubashaVentures.Domain.User.AddressViewModel? ShippingAddress { get; set; }
    public SubashaVentures.Domain.User.AddressViewModel? BillingAddress { get; set; }
    public bool UseSameAddressForBilling { get; set; } = true;
    
    public string? PromoCode { get; set; }
    public decimal PromoDiscount { get; set; }
    
    public string ShippingMethod { get; set; } = "Standard";
    public decimal ShippingCost { get; set; }
    
    public PaymentMethod PaymentMethod { get; set; }
    
    public decimal Subtotal => Items.Sum(i => i.Subtotal);
    public decimal Total => Subtotal + ShippingCost - PromoDiscount;
    
    public string? CustomerNotes { get; set; }
    
    public bool IsValid => Items.Any() && ShippingAddress != null;
}
