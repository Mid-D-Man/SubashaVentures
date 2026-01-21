
// Models/Supabase/UserWalletModel.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace SubashaVentures.Models.Supabase;


/// <summary>
/// User wallet model - stores wallet balance
/// Maps to user_wallets table
/// </summary>
[Table("user_wallets")]
public class UserWalletModel : BaseModel
{
    [PrimaryKey("user_id", false)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [Column("balance")]
    public decimal Balance { get; set; }
    
    [Column("currency")]
    public string Currency { get; set; } = "NGN";
    
    [Column("is_locked")]
    public bool IsLocked { get; set; }
    
    // ISecureEntity fields
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("created_by")]
    public string CreatedBy { get; set; } = "system";
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
}
