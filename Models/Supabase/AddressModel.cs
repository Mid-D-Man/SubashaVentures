// Models/Supabase/AddressModel.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace SubashaVentures.Models.Supabase;

[Table("addresses")]
public class AddressModel : BaseModel
{
    [PrimaryKey("user_id", false)]
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("items")]
    [JsonPropertyName("items")]
    public List<AddressItem> Items { get; set; } = new();

    [Column("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Individual address item stored in JSONB array
/// </summary>
public class AddressItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("address_line1")]
    public string AddressLine1 { get; set; } = string.Empty;

    [JsonPropertyName("address_line2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = "Lagos";

    [JsonPropertyName("state")]
    public string State { get; set; } = "Lagos";

    [JsonPropertyName("postal_code")]
    public string PostalCode { get; set; } = "100001";

    [JsonPropertyName("country")]
    public string Country { get; set; } = "Nigeria";

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Shipping"; // Shipping, Billing, Both

    [JsonPropertyName("added_at")]
    public DateTime AddedAt { get; set; }
}
