// Domain/Payment/SavedCardViewModel.cs
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.Payment;

public class SavedCardViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string AuthorizationCode { get; set; } = string.Empty;
    public string CardType { get; set; } = string.Empty;
    public string CardLast4 { get; set; } = string.Empty;
    public string CardExpMonth { get; set; } = string.Empty;
    public string CardExpYear { get; set; } = string.Empty;
    public string? CardBank { get; set; }
    public string? CardBrand { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Display properties
    public string MaskedCardNumber => $"•••• •••• •••• {CardLast4}";
    public string ExpiryDate => $"{CardExpMonth}/{CardExpYear}";
    public string CardIcon => CardType.ToLower() switch
    {
        "visa" => "💳",
        "mastercard" => "💳",
        "verve" => "💳",
        _ => "💳"
    };
    
    public string ProviderDisplay => Provider switch
    {
        "paystack" => "Paystack",
        "flutterwave" => "Flutterwave",
        _ => Provider
    };
    
    public bool IsExpired
    {
        get
        {
            if (int.TryParse(CardExpMonth, out var month) && int.TryParse(CardExpYear, out var year))
            {
                var expiry = new DateTime(2000 + year, month, 1).AddMonths(1).AddDays(-1);
                return DateTime.Now > expiry;
            }
            return false;
        }
    }
    
    public static SavedCardViewModel FromModel(UserPaymentMethodModel model)
    {
        return new SavedCardViewModel
        {
            Id = model.Id,
            UserId = model.UserId,
            Provider = model.Provider,
            AuthorizationCode = model.AuthorizationCode,
            CardType = model.CardType,
            CardLast4 = model.CardLast4,
            CardExpMonth = model.CardExpMonth,
            CardExpYear = model.CardExpYear,
            CardBank = model.CardBank,
            CardBrand = model.CardBrand,
            IsDefault = model.IsDefault,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt
        };
    }
}
