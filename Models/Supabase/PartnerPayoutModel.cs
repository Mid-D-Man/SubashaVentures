using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

[Table("partner_payouts")]
public class PartnerPayoutModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }
    
    [Column("partner_id")]
    public Guid PartnerId { get; set; }
    
    [Column("amount")]
    public decimal Amount { get; set; }
    
    [Column("payout_method")]
    public string PayoutMethod { get; set; } = "bank_transfer";
    
    [Column("transaction_reference")]
    public string? TransactionReference { get; set; }
    
    [Column("period_start")]
    public DateTime PeriodStart { get; set; }
    
    [Column("period_end")]
    public DateTime PeriodEnd { get; set; }
    
    [Column("status")]
    public string Status { get; set; } = "pending";
    
    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }
    
    [Column("notes")]
    public string? Notes { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("created_by")]
    public string CreatedBy { get; set; } = string.Empty;
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
}