using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Services.Partners;

public interface IPartnerService
{
    // Core CRUD
    Task<PartnerModel?> CreatePartnerAsync(CreatePartnerRequest request);
    Task<bool> UpdatePartnerAsync(Guid partnerId, UpdatePartnerRequest request);
    Task<bool> DeletePartnerAsync(Guid partnerId);
    Task<PartnerModel?> GetPartnerByIdAsync(Guid partnerId);
    Task<PartnerModel?> GetPartnerByUniqueIdAsync(string uniquePartnerId);
    Task<List<PartnerModel>> GetAllPartnersAsync();
    Task<List<PartnerModel>> GetActivePartnersAsync();
    
    // Commission & Financials
    Task<decimal> CalculatePartnerCommissionAsync(Guid partnerId, decimal saleAmount);
    Task<bool> UpdatePartnerMetricsAsync(Guid partnerId, decimal saleAmount, decimal commission);
    Task<bool> RecordPartnerPayoutAsync(Guid partnerId, decimal amount);
    Task<List<PartnerModel>> GetPartnersNeedingPayoutAsync(decimal minimumPayout = 10000m);
    
    // Verification
    Task<bool> UpdateVerificationStatusAsync(Guid partnerId, string status);
    Task<bool> AddVerificationDocumentAsync(Guid partnerId, string documentUrl);
    
    // Utilities
    string GenerateUniquePartnerId();
}

public class CreatePartnerRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
    
    public decimal CommissionRate { get; set; } = 50.00m;
    public string PaymentMethod { get; set; } = "bank_transfer";
    public BankDetails? BankDetails { get; set; }
    
    public PartnerAddress? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
}

public class UpdatePartnerRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
    
    public decimal? CommissionRate { get; set; }
    public string? PaymentMethod { get; set; }
    public BankDetails? BankDetails { get; set; }
    
    public PartnerAddress? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
    
    public bool? IsActive { get; set; }
    public string? VerificationStatus { get; set; }
}