// Domain/Payment/WalletTransactionViewModel.cs
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.Payment;

public class WalletTransactionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? PaymentProvider { get; set; }
    public string? PaymentReference { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // Display properties
    public string FormattedAmount => $"₦{Amount:N0}";
    public string TypeDisplay => Type switch
    {
        "credit" => "Credit",
        "debit" => "Debit",
        "refund" => "Refund",
        "topup" => "Top Up",
        "purchase" => "Purchase",
        _ => Type
    };
    
    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - CreatedAt;
            if (span.TotalDays > 365) return $"{(int)(span.TotalDays / 365)} year(s) ago";
            if (span.TotalDays > 30) return $"{(int)(span.TotalDays / 30)} month(s) ago";
            if (span.TotalDays > 1) return $"{(int)span.TotalDays} day(s) ago";
            if (span.TotalHours > 1) return $"{(int)span.TotalHours} hour(s) ago";
            if (span.TotalMinutes > 1) return $"{(int)span.TotalMinutes} minute(s) ago";
            return "Just now";
        }
    }
    
    public static WalletTransactionViewModel FromModel(WalletTransactionModel model)
    {
        return new WalletTransactionViewModel
        {
            Id = model.Id,
            UserId = model.UserId,
            Type = model.Type,
            Amount = model.Amount,
            BalanceBefore = model.BalanceBefore,
            BalanceAfter = model.BalanceAfter,
            Reference = model.Reference,
            PaymentProvider = model.PaymentProvider,
            PaymentReference = model.PaymentReference,
            Description = model.Description,
            CreatedAt = model.CreatedAt
        };
    }
}
