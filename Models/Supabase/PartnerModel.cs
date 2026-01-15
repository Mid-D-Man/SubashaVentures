using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
namespace SubashaVentures.Models.Supabase;
using JsonPropertyName = Newtonsoft.Json.JsonPropertyAttribute;
/// <summary>
/// Partner/Vendor model
/// Has both UUID (internal) and base64 unique ID (external)
/// </summary>
[Table("partners")]
public class PartnerModel : BaseModel
{
    // Internal UUID (primary key)
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }
    
    // External base64-encoded ID (for URLs, human-readable references)
    [Column("unique_partner_id")]
    public string UniquePartnerId { get; set; } = string.Empty;
    
    // Partner Information
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    
    [Column("email")]
    public string Email { get; set; } = string.Empty;
    
    [Column("phone_number")]
    public string? PhoneNumber { get; set; }
    
    [Column("company_name")]
    public string? CompanyName { get; set; }
    
    [Column("business_registration_number")]
    public string? BusinessRegistrationNumber { get; set; }
    
    // Financial Details
    [Column("commission_rate")]
    public decimal CommissionRate { get; set; } = 50.00m;
    
    [Column("payment_method")]
    public string PaymentMethod { get; set; } = "bank_transfer";
    
    [Column("bank_details")]
    public BankDetails? BankDetails { get; set; }
    
    // Contact & Address
    [Column("address")]
    public PartnerAddress? Address { get; set; }
    
    [Column("contact_person")]
    public string? ContactPerson { get; set; }
    
    // Business Metrics (auto-calculated)
    [Column("total_products")]
    public int TotalProducts { get; set; }
    
    [Column("total_sales")]
    public decimal TotalSales { get; set; }
    
    [Column("total_commission")]
    public decimal TotalCommission { get; set; }
    
    [Column("pending_payout")]
    public decimal PendingPayout { get; set; }
    
    // Status
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    [Column("verification_status")]
    public string VerificationStatus { get; set; } = "pending";
    
    [Column("verification_documents")]
    public List<string> VerificationDocuments { get; set; } = new();
    
    // Notes & Metadata
    [Column("notes")]
    public string? Notes { get; set; }
    
    [Column("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    // Audit Fields
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("created_by")]
    public string CreatedBy { get; set; } = string.Empty;
    
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

public class BankDetails
{
    [JsonPropertyName("account_name")]
    public string AccountName { get; set; } = string.Empty;
    
    [JsonPropertyName("account_number")]
    public string AccountNumber { get; set; } = string.Empty;
    
    [JsonPropertyName("bank_name")]
    public string BankName { get; set; } = string.Empty;
    
    [JsonPropertyName("bank_code")]
    public string? BankCode { get; set; }
}

public class PartnerAddress
{
    [JsonPropertyName("street")]
    public string Street { get; set; } = string.Empty;
    
    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;
    
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
    
    [JsonPropertyName("postal_code")]
    public string PostalCode { get; set; } = string.Empty;
    
    [JsonPropertyName("country")]
    public string Country { get; set; } = "Nigeria";
}

public static class PartnerModelExtensions
{
    /// <summary>
    /// Calculate commission for a sale amount
    /// </summary>
    public static decimal CalculateCommission(this PartnerModel partner, decimal saleAmount)
    {
        return saleAmount * (partner.CommissionRate / 100m);
    }
}