namespace SubashaVentures.Domain.Cart;

/// <summary>
/// View model for cart summary and totals
/// </summary>
public class CartSummaryViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    
    public string? PromoCode { get; set; }
    public decimal PromoDiscount { get; set; }
    public string? PromoDescription { get; set; }
    
    public decimal ShippingCost { get; set; }
    public string ShippingMethod { get; set; } = "Standard";
    public bool HasFreeShipping { get; set; }
    
    public decimal TaxAmount { get; set; }
    public decimal TaxRate { get; set; }
    
    public int TotalItems => Items.Sum(i => i.Quantity);
    public decimal Subtotal => Items.Sum(i => i.Subtotal);
    public decimal TotalDiscount => PromoDiscount;
    public decimal Total => Subtotal + ShippingCost + TaxAmount - TotalDiscount;
    
    public string DisplaySubtotal => $"₦{Subtotal:N0}";
    public string DisplayShipping => HasFreeShipping ? "FREE" : $"₦{ShippingCost:N0}";
    public string DisplayDiscount => PromoDiscount > 0 ? $"-₦{PromoDiscount:N0}" : "₦0";
    public string DisplayTax => TaxAmount > 0 ? $"₦{TaxAmount:N0}" : "₦0";
    public string DisplayTotal => $"₦{Total:N0}";
    
    public bool IsEmpty => !Items.Any();
    public bool HasOutOfStockItems => Items.Any(i => !i.IsInStock);
    public bool CanCheckout => !IsEmpty && !HasOutOfStockItems;
    
    public decimal FreeShippingThreshold { get; set; } = 50000;
    public decimal AmountToFreeShipping => FreeShippingThreshold - Subtotal;
    public bool QualifiesForFreeShipping => Subtotal >= FreeShippingThreshold;
}