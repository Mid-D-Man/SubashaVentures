// Models/Supabase/UserPaymentMethodModel.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// User payment method model - stores saved card tokens
/// Maps to user_payment_methods table
/// </summary>
[Table("user_payment_methods")]
public class UserPaymentMethodModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;
    
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [Column("provider")]
    public string Provider { get; set; } = string.Empty; // paystack, flutterwave
    
    [Column("authorization_code")]
    public string AuthorizationCode { get; set; } = string.Empty;
    
    [Column("card_type")]
    public string CardType { get; set; } = string.Empty; // visa, mastercard
    
    [Column("card_last4")]
    public string CardLast4 { get; set; } = string.Empty;
    
    [Column("card_exp_month")]
    public string CardExpMonth { get; set; } = string.Empty;
    
    [Column("card_exp_year")]
    public string CardExpYear { get; set; } = string.Empty;
    
    [Column("card_bank")]
    public string? CardBank { get; set; }
    
    [Column("card_brand")]
    public string? CardBrand { get; set; }
    
    [Column("is_default")]
    public bool IsDefault { get; set; }
    
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    // ISecureEntity fields
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("created_by")]
    public string CreatedBy { get; set; } = "system";
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
    
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
    
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }
    
    [Column("deleted_by")]
    public string? DeletedBy { get; set; }
}
