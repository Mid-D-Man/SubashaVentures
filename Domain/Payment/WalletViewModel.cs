
// Domain/Payment/WalletViewModel.cs
using SubashaVentures.Models.Supabase;


namespace SubashaVentures.Domain.Payment;


public class WalletViewModel
{
    public string UserId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NGN";
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Display properties
    public string FormattedBalance => $"₦{Balance:N0}";
    public string Status => IsLocked ? "Locked" : "Active";
    
    public static WalletViewModel FromModel(UserWalletModel model)
    {
        return new WalletViewModel
        {
            UserId = model.UserId,
            Balance = model.Balance,
            Currency = model.Currency,
            IsLocked = model.IsLocked,
            CreatedAt = model.CreatedAt
        };
    }
}
