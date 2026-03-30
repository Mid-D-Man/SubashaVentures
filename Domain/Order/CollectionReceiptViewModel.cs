namespace SubashaVentures.Domain.Order;

public class CollectionReceiptViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string ShippingMethod { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public DateTime OrderedAt { get; set; }
    public DateTime CollectedAt { get; set; }
    public List<CollectionReceiptItemViewModel> Items { get; set; } = new();

    public string DisplayTotal => $"₦{Total:N0}";
    public string DisplaySubtotal => $"₦{Subtotal:N0}";
    public string DisplayDiscount => $"₦{Discount:N0}";
}

public class CollectionReceiptItemViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public decimal Subtotal { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string DisplayPrice => $"₦{Price:N0}";
    public string DisplaySubtotal => $"₦{Subtotal:N0}";
}
