using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Partners;

public class PartnerService : IPartnerService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ILogger<PartnerService> _logger;

    public PartnerService(
        ISupabaseDatabaseService database,
        ILogger<PartnerService> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<PartnerModel?> CreatePartnerAsync(CreatePartnerRequest request)
    {
        try
        {
            var now = DateTime.UtcNow;
            var uniqueId = GenerateUniquePartnerId();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating partner: {request.Name} ({request.Email})",
                LogLevel.Info
            );

            var partnerModel = new PartnerModel
            {
                Id = Guid.NewGuid(),
                UniquePartnerId = uniqueId,
                
                Name = request.Name,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                CompanyName = request.CompanyName,
                BusinessRegistrationNumber = request.BusinessRegistrationNumber,
                
                CommissionRate = request.CommissionRate,
                PaymentMethod = request.PaymentMethod,
                BankDetails = request.BankDetails,
                
                Address = request.Address,
                ContactPerson = request.ContactPerson,
                Notes = request.Notes,
                
                TotalProducts = 0,
                TotalSales = 0,
                TotalCommission = 0,
                PendingPayout = 0,
                
                IsActive = true,
                VerificationStatus = "pending",
                VerificationDocuments = new List<string>(),
                Metadata = new Dictionary<string, object>(),
                
                CreatedAt = now,
                CreatedBy = "system",
                IsDeleted = false
            };

            var result = await _database.InsertAsync(partnerModel);

            if (result == null || !result.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Partner creation failed",
                    LogLevel.Error
                );
                return null;
            }

            var createdPartner = result.First();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Partner created: {createdPartner.UniquePartnerId}",
                LogLevel.Info
            );

            return createdPartner;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Creating partner: {request.Name}");
            _logger.LogError(ex, "Failed to create partner: {Name}", request.Name);
            return null;
        }
    }

    public async Task<bool> UpdatePartnerAsync(Guid partnerId, UpdatePartnerRequest request)
    {
        try
        {
            var existingPartner = await GetPartnerByIdAsync(partnerId);
            if (existingPartner == null)
            {
                _logger.LogWarning("Partner not found: {PartnerId}", partnerId);
                return false;
            }

            if (request.Name != null) existingPartner.Name = request.Name;
            if (request.Email != null) existingPartner.Email = request.Email;
            if (request.PhoneNumber != null) existingPartner.PhoneNumber = request.PhoneNumber;
            if (request.CompanyName != null) existingPartner.CompanyName = request.CompanyName;
            if (request.BusinessRegistrationNumber != null) 
                existingPartner.BusinessRegistrationNumber = request.BusinessRegistrationNumber;
            
            if (request.CommissionRate.HasValue) existingPartner.CommissionRate = request.CommissionRate.Value;
            if (request.PaymentMethod != null) existingPartner.PaymentMethod = request.PaymentMethod;
            if (request.BankDetails != null) existingPartner.BankDetails = request.BankDetails;
            
            if (request.Address != null) existingPartner.Address = request.Address;
            if (request.ContactPerson != null) existingPartner.ContactPerson = request.ContactPerson;
            if (request.Notes != null) existingPartner.Notes = request.Notes;
            
            if (request.IsActive.HasValue) existingPartner.IsActive = request.IsActive.Value;
            if (request.VerificationStatus != null) existingPartner.VerificationStatus = request.VerificationStatus;

            existingPartner.UpdatedAt = DateTime.UtcNow;
            existingPartner.UpdatedBy = "system";

            var result = await _database.UpdateAsync(existingPartner);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating partner: {partnerId}");
            _logger.LogError(ex, "Failed to update partner: {PartnerId}", partnerId);
            return false;
        }
    }

    public async Task<bool> DeletePartnerAsync(Guid partnerId)
    {
        try
        {
            var partner = await GetPartnerByIdAsync(partnerId);
            if (partner == null) return false;

            partner.IsDeleted = true;
            partner.DeletedAt = DateTime.UtcNow;
            partner.DeletedBy = "system";
            partner.IsActive = false;

            var result = await _database.UpdateAsync(partner);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Deleting partner: {partnerId}");
            return false;
        }
    }

    public async Task<PartnerModel?> GetPartnerByIdAsync(Guid partnerId)
    {
        try
        {
            var partners = await _database.GetWithFilterAsync<PartnerModel>(
                "id",
                Constants.Operator.Equals,
                partnerId
            );
            
            return partners.FirstOrDefault(p => !p.IsDeleted);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting partner: {partnerId}");
            return null;
        }
    }

    public async Task<PartnerModel?> GetPartnerByUniqueIdAsync(string uniquePartnerId)
    {
        try
        {
            var partners = await _database.GetWithFilterAsync<PartnerModel>(
                "unique_partner_id",
                Constants.Operator.Equals,
                uniquePartnerId
            );
            
            return partners.FirstOrDefault(p => !p.IsDeleted);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting partner by ID: {uniquePartnerId}");
            return null;
        }
    }

    public async Task<List<PartnerModel>> GetAllPartnersAsync()
    {
        try
        {
            var partners = await _database.GetAllAsync<PartnerModel>();
            return partners.Where(p => !p.IsDeleted).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting all partners");
            return new List<PartnerModel>();
        }
    }

    public async Task<List<PartnerModel>> GetActivePartnersAsync()
    {
        try
        {
            var partners = await _database.GetAllAsync<PartnerModel>();
            return partners.Where(p => !p.IsDeleted && p.IsActive).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting active partners");
            return new List<PartnerModel>();
        }
    }

    public async Task<decimal> CalculatePartnerCommissionAsync(Guid partnerId, decimal saleAmount)
    {
        try
        {
            var partner = await GetPartnerByIdAsync(partnerId);
            if (partner == null) return 0;

            return partner.CalculateCommission(saleAmount);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Calculating commission: {partnerId}");
            return 0;
        }
    }

    public async Task<bool> UpdatePartnerMetricsAsync(Guid partnerId, decimal saleAmount, decimal commission)
    {
        try
        {
            var partner = await GetPartnerByIdAsync(partnerId);
            if (partner == null) return false;

            partner.TotalSales += saleAmount;
            partner.TotalCommission += commission;
            partner.PendingPayout += commission;
            partner.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(partner);
            
            if (result != null && result.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Partner metrics updated: {partner.UniquePartnerId} - Sale: ₦{saleAmount:N0}, Commission: ₦{commission:N0}",
                    LogLevel.Info
                );
            }

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating partner metrics: {partnerId}");
            return false;
        }
    }

    public async Task<bool> RecordPartnerPayoutAsync(Guid partnerId, decimal amount)
    {
        try
        {
            var partner = await GetPartnerByIdAsync(partnerId);
            if (partner == null) return false;

            if (partner.PendingPayout < amount)
            {
                _logger.LogWarning(
                    "Insufficient pending payout for partner {PartnerId}: {Pending} < {Amount}",
                    partnerId, partner.PendingPayout, amount
                );
                return false;
            }

            partner.PendingPayout -= amount;
            partner.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(partner);
            
            if (result != null && result.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Payout recorded: {partner.UniquePartnerId} - ₦{amount:N0}",
                    LogLevel.Info
                );
            }

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Recording payout: {partnerId}");
            return false;
        }
    }

    public async Task<List<PartnerModel>> GetPartnersNeedingPayoutAsync(decimal minimumPayout = 10000m)
    {
        try
        {
            var partners = await GetActivePartnersAsync();
            return partners
                .Where(p => p.PendingPayout >= minimumPayout && p.VerificationStatus == "verified")
                .OrderByDescending(p => p.PendingPayout)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting partners needing payout");
            return new List<PartnerModel>();
        }
    }

    public async Task<bool> UpdateVerificationStatusAsync(Guid partnerId, string status)
    {
        try
        {
            var partner = await GetPartnerByIdAsync(partnerId);
            if (partner == null) return false;

            partner.VerificationStatus = status;
            partner.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(partner);
            
            if (result != null && result.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Verification status updated: {partner.UniquePartnerId} -> {status}",
                    LogLevel.Info
                );
            }

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating verification status: {partnerId}");
            return false;
        }
    }

    public async Task<bool> AddVerificationDocumentAsync(Guid partnerId, string documentUrl)
    {
        try
        {
            var partner = await GetPartnerByIdAsync(partnerId);
            if (partner == null) return false;

            partner.VerificationDocuments.Add(documentUrl);
            partner.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(partner);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Adding verification document: {partnerId}");
            return false;
        }
    }

    public string GenerateUniquePartnerId()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
        return $"PTR-{timestamp}-{random}";
    }
}