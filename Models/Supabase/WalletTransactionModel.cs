// Models/Supabase/WalletTransactionModel.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Wallet transaction model - immutable audit trail
/// Maps to wallet_transactions table
/// </summary>
[Table("wallet_transactions")]
public class WalletTransactionModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;
    
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [Column("type")]
    public string Type { get; set; } = string.Empty; // credit, debit, refund, topup, purchase
    
    [Column("amount")]
    public decimal Amount { get; set; }
    
    [Column("balance_before")]
    public decimal BalanceBefore { get; set; }
    
    [Column("balance_after")]
    public decimal BalanceAfter { get; set; }
    
    [Column("reference")]
    public string Reference { get; set; } = string.Empty;
    
    [Column("payment_provider")]
    public string? PaymentProvider { get; set; }
    
    [Column("payment_reference")]
    public string? PaymentReference { get; set; }
    
    [Column("description")]
    public string Description { get; set; } = string.Empty;
    
    [Column("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("created_by")]
    public string CreatedBy { get; set; } = "system";
}
