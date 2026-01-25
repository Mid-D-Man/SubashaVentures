// Models/Supabase/AddressModel.cs - UPDATED WITH EMAIL FIELD
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Address model - stores user addresses as JSONB array
/// UPDATED: Added email field to AddressItem
/// Maps to addresses table
/// </summary>
[Table("addresses")]
public class AddressModel : BaseModel
{
    [PrimaryKey("user_id", false)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [Column("items")]
    public List<AddressItem> Items { get; set; } = new();
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Individual address item stored in JSONB
/// UPDATED: Added Email field
/// </summary>
public class AddressItem
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonProperty("full_name")]
    public string FullName { get; set; } = string.Empty;
    
    [JsonProperty("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional email field for this specific address
    /// Falls back to user's primary email if not provided
    /// </summary>
    [JsonProperty("email")]
    public string? Email { get; set; }
    
    [JsonProperty("address_line1")]
    public string AddressLine1 { get; set; } = string.Empty;
    
    [JsonProperty("address_line2")]
    public string? AddressLine2 { get; set; }
    
    [JsonProperty("city")]
    public string City { get; set; } = string.Empty;
    
    [JsonProperty("state")]
    public string State { get; set; } = string.Empty;
    
    [JsonProperty("postal_code")]
    public string PostalCode { get; set; } = string.Empty;
    
    [JsonProperty("country")]
    public string Country { get; set; } = "Nigeria";
    
    [JsonProperty("is_default")]
    public bool IsDefault { get; set; }
    
    [JsonProperty("type")]
    public string Type { get; set; } = "Shipping"; // Shipping, Billing, Both
    
    [JsonProperty("added_at")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
