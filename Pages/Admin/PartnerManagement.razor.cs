using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.Partners;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Popups;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class PartnerManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private IPartnerService PartnerService { get; set; } = default!;
    [Inject] private ILogger<PartnerManagement> Logger { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Component state
    private bool isLoading = true;
    private bool isPartnerModalOpen = false;
    private bool isEditMode = false;
    private bool isSaving = false;
    private bool showDeleteConfirmation = false;

    private string viewMode = "grid";
    private string searchQuery = "";
    private string selectedStatus = "";
    private string selectedVerification = "";
    private string sortBy = "newest";

    private int currentPage = 1;
    private int pageSize = 24;

    // Stats
    private int totalPartners = 0;
    private int activePartners = 0;
    private int pendingVerification = 0;
    private decimal totalPendingPayout = 0;

    // Data
    private List<PartnerModel> allPartners = new();
    private List<PartnerModel> filteredPartners = new();
    private List<PartnerModel> paginatedPartners = new();
    private List<Guid> selectedPartners = new();

    // Form state
    private PartnerFormData partnerForm = new();
    private Dictionary<string, string> validationErrors = new();

    // Delete confirmation
    private PartnerModel? partnerToDelete = null;
    private List<Guid>? partnersToDelete = null;

    // Component references
    private DynamicModal? partnerModal;
    private ConfirmationPopup? deleteConfirmationPopup;
    private NotificationComponent? notificationComponent;

    private int totalPages => (int)Math.Ceiling(filteredPartners.Count / (double)pageSize);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync("PartnerManagement initialized", LogLevel.Info);

            await LoadPartnersAsync();
            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "PartnerManagement initialization");
            ShowErrorNotification("Failed to initialize partner management");
            isLoading = false;
        }
    }

    private async Task LoadPartnersAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            allPartners = await PartnerService.GetAllPartnersAsync();
            totalPartners = allPartners.Count;

            ApplyFiltersAndSort();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {totalPartners} partners successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading partners");
            Logger.LogError(ex, "Failed to load partners");
            ShowErrorNotification("Failed to load partners");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void ApplyFiltersAndSort()
    {
        var filtered = allPartners.Where(p =>
            (string.IsNullOrEmpty(searchQuery) ||
             p.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.Email.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.UniquePartnerId.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(selectedStatus) || FilterByStatus(p, selectedStatus)) &&
            (string.IsNullOrEmpty(selectedVerification) || p.VerificationStatus == selectedVerification)
        ).ToList();

        filteredPartners = sortBy switch
        {
            "newest" => filtered.OrderByDescending(x => x.CreatedAt).ToList(),
            "oldest" => filtered.OrderBy(x => x.CreatedAt).ToList(),
            "name-az" => filtered.OrderBy(x => x.Name).ToList(),
            "name-za" => filtered.OrderByDescending(x => x.Name).ToList(),
            "commission-high" => filtered.OrderByDescending(x => x.CommissionRate).ToList(),
            "commission-low" => filtered.OrderBy(x => x.CommissionRate).ToList(),
            "payout-high" => filtered.OrderByDescending(x => x.PendingPayout).ToList(),
            _ => filtered
        };

        currentPage = 1;
        UpdatePaginatedPartners();
    }

    private bool FilterByStatus(PartnerModel partner, string status)
    {
        return status switch
        {
            "active" => partner.IsActive,
            "inactive" => !partner.IsActive,
            _ => true
        };
    }

    private void UpdatePaginatedPartners()
    {
        paginatedPartners = filteredPartners
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        StateHasChanged();
    }

    private void CalculateStats()
    {
        activePartners = allPartners.Count(p => p.IsActive);
        pendingVerification = allPartners.Count(p => p.VerificationStatus == "pending");
        totalPendingPayout = allPartners.Sum(p => p.PendingPayout);
    }

    private void HandleSearch()
    {
        ApplyFiltersAndSort();
    }

    private void HandleStatusFilter(ChangeEventArgs e)
    {
        selectedStatus = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleVerificationFilter(ChangeEventArgs e)
    {
        selectedVerification = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "newest";
        ApplyFiltersAndSort();
    }

    private void OpenCreatePartnerModal()
    {
        isEditMode = false;
        partnerForm = new PartnerFormData();
        validationErrors.Clear();
        isPartnerModalOpen = true;
        StateHasChanged();
    }

    private void OpenEditPartnerModal(PartnerModel partner)
    {
        isEditMode = true;
        partnerForm = MapToFormData(partner);
        validationErrors.Clear();
        isPartnerModalOpen = true;
        StateHasChanged();
    }

    private void ClosePartnerModal()
    {
        isPartnerModalOpen = false;
        partnerForm = new PartnerFormData();
        validationErrors.Clear();
        StateHasChanged();
    }

    private async Task HandleSavePartner()
    {
        if (!ValidatePartnerForm())
        {
            StateHasChanged();
            return;
        }

        try
        {
            isSaving = true;
            StateHasChanged();

            if (isEditMode)
            {
                var updateRequest = MapToUpdateRequest(partnerForm);
                var success = await PartnerService.UpdatePartnerAsync(partnerForm.Id, updateRequest);

                if (success)
                {
                    ShowSuccessNotification($"Partner '{partnerForm.Name}' updated successfully!");
                }
                else
                {
                    ShowErrorNotification("Failed to update partner");
                    return;
                }
            }
            else
            {
                var createRequest = MapToCreateRequest(partnerForm);
                var result = await PartnerService.CreatePartnerAsync(createRequest);

                if (result != null)
                {
                    ShowSuccessNotification($"Partner '{partnerForm.Name}' created successfully!");
                }
                else
                {
                    ShowErrorNotification("Failed to create partner");
                    return;
                }
            }

            ClosePartnerModal();
            await LoadPartnersAsync();
            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving partner");
            ShowErrorNotification($"Error: {ex.Message}");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private bool ValidatePartnerForm()
    {
        validationErrors.Clear();

        if (string.IsNullOrWhiteSpace(partnerForm.Name))
        {
            validationErrors["Name"] = "Partner name is required";
        }

        if (string.IsNullOrWhiteSpace(partnerForm.Email))
        {
            validationErrors["Email"] = "Email is required";
        }

        if (partnerForm.CommissionRate < 0 || partnerForm.CommissionRate > 100)
        {
            validationErrors["CommissionRate"] = "Commission rate must be between 0 and 100";
        }

        return !validationErrors.Any();
    }

    private void HandleDeletePartner(PartnerModel partner)
    {
        partnerToDelete = partner;
        partnersToDelete = null;
        showDeleteConfirmation = true;
        StateHasChanged();
    }

    private async Task ConfirmDeletePartner()
    {
        if (partnerToDelete != null)
        {
            try
            {
                var success = await PartnerService.DeletePartnerAsync(partnerToDelete.Id);
                
                if (success)
                {
                    ShowSuccessNotification($"Partner '{partnerToDelete.Name}' deleted successfully!");
                    await LoadPartnersAsync();
                    CalculateStats();
                }
                else
                {
                    ShowErrorNotification("Failed to delete partner");
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting partner");
                ShowErrorNotification($"Error deleting partner: {ex.Message}");
            }
            finally
            {
                showDeleteConfirmation = false;
                partnerToDelete = null;
                StateHasChanged();
            }
        }
    }

    private async Task HandleToggleActive(PartnerModel partner, bool isActive)
    {
        try
        {
            var updateRequest = new UpdatePartnerRequest { IsActive = isActive };
            var success = await PartnerService.UpdatePartnerAsync(partner.Id, updateRequest);

            if (success)
            {
                ShowSuccessNotification($"Partner {(isActive ? "activated" : "deactivated")}");
                await LoadPartnersAsync();
                CalculateStats();
            }
            else
            {
                ShowErrorNotification("Failed to update partner status");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling active status");
            ShowErrorNotification("Error updating partner status");
        }
    }

    private void HandleSelectionChanged(Guid partnerId, bool isSelected)
    {
        if (isSelected)
        {
            if (!selectedPartners.Contains(partnerId))
                selectedPartners.Add(partnerId);
        }
        else
        {
            selectedPartners.Remove(partnerId);
        }
        StateHasChanged();
    }

    private void HandleSelectAll(ChangeEventArgs e)
    {
        if (e.Value is bool isChecked)
        {
            if (isChecked)
            {
                selectedPartners = paginatedPartners.Select(p => p.Id).ToList();
            }
            else
            {
                selectedPartners.Clear();
            }
            StateHasChanged();
        }
    }

    private async Task HandleBulkActivate()
    {
        if (!selectedPartners.Any())
        {
            ShowWarningNotification("No partners selected");
            return;
        }

        try
        {
            foreach (var partnerId in selectedPartners)
            {
                var updateRequest = new UpdatePartnerRequest { IsActive = true };
                await PartnerService.UpdatePartnerAsync(partnerId, updateRequest);
            }
            
            ShowSuccessNotification($"{selectedPartners.Count} partners activated");
            selectedPartners.Clear();
            await LoadPartnersAsync();
            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk activate");
            ShowErrorNotification("Error activating partners");
        }
    }

    private async Task HandleBulkDeactivate()
    {
        if (!selectedPartners.Any())
        {
            ShowWarningNotification("No partners selected");
            return;
        }

        try
        {
            foreach (var partnerId in selectedPartners)
            {
                var updateRequest = new UpdatePartnerRequest { IsActive = false };
                await PartnerService.UpdatePartnerAsync(partnerId, updateRequest);
            }
            
            ShowSuccessNotification($"{selectedPartners.Count} partners deactivated");
            selectedPartners.Clear();
            await LoadPartnersAsync();
            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk deactivate");
            ShowErrorNotification("Error deactivating partners");
        }
    }

    private void HandleBulkDelete()
    {
        if (!selectedPartners.Any())
        {
            ShowWarningNotification("No partners selected");
            return;
        }

        partnersToDelete = new List<Guid>(selectedPartners);
        partnerToDelete = null;
        showDeleteConfirmation = true;
        StateHasChanged();
    }

    private async Task ConfirmBulkDelete()
    {
        if (partnersToDelete != null && partnersToDelete.Any())
        {
            try
            {
                foreach (var partnerId in partnersToDelete)
                {
                    await PartnerService.DeletePartnerAsync(partnerId);
                }
                
                ShowSuccessNotification($"{partnersToDelete.Count} partners deleted");
                selectedPartners.Clear();
                await LoadPartnersAsync();
                CalculateStats();
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk delete");
                ShowErrorNotification("Error deleting partners");
            }
            finally
            {
                showDeleteConfirmation = false;
                partnersToDelete = null;
                StateHasChanged();
            }
        }
    }

    private async Task HandleExport()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var exportData = filteredPartners.Select(p => new
            {
                ID = p.UniquePartnerId,
                Name = p.Name,
                Email = p.Email,
                Phone = p.PhoneNumber,
                Company = p.CompanyName,
                CommissionRate = p.CommissionRate,
                TotalSales = p.TotalSales,
                TotalCommission = p.TotalCommission,
                PendingPayout = p.PendingPayout,
                TotalProducts = p.TotalProducts,
                Status = p.IsActive ? "Active" : "Inactive",
                Verification = p.VerificationStatus,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList();

            var csv = new StringBuilder();
            
            csv.AppendLine("ID,Name,Email,Phone,Company,Commission Rate,Total Sales,Total Commission,Pending Payout,Total Products,Status,Verification,Created At");
            
            foreach (var item in exportData)
            {
                csv.AppendLine($"\"{item.ID}\"," +
                              $"\"{EscapeCsv(item.Name)}\"," +
                              $"\"{EscapeCsv(item.Email)}\"," +
                              $"\"{EscapeCsv(item.Phone)}\"," +
                              $"\"{EscapeCsv(item.Company)}\"," +
                              $"{item.CommissionRate}," +
                              $"{item.TotalSales}," +
                              $"{item.TotalCommission}," +
                              $"{item.PendingPayout}," +
                              $"{item.TotalProducts}," +
                              $"{item.Status}," +
                              $"{item.Verification}," +
                              $"{item.CreatedAt}");
            }

            var fileName = $"partners_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var base64 = Convert.ToBase64String(csvBytes);

            await JSRuntime.InvokeVoidAsync("downloadFile", fileName, base64, "text/csv");
            
            ShowSuccessNotification($"Exported {exportData.Count} partners successfully!");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Exporting partners");
            ShowErrorNotification($"Export failed: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        
        if (value.Contains("\""))
            value = value.Replace("\"", "\"\"");
        
        return value;
    }

    private void PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            UpdatePaginatedPartners();
        }
    }

    private void NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            UpdatePaginatedPartners();
        }
    }

    private void GoToPage(int page)
    {
        if (page >= 1 && page <= totalPages)
        {
            currentPage = page;
            UpdatePaginatedPartners();
        }
    }

    private void ShowSuccessNotification(string message)
    {
        notificationComponent?.ShowSuccess(message);
    }

    private void ShowErrorNotification(string message)
    {
        notificationComponent?.ShowError(message);
    }

    private void ShowWarningNotification(string message)
    {
        notificationComponent?.ShowWarning(message);
    }

    private PartnerFormData MapToFormData(PartnerModel partner)
    {
        return new PartnerFormData
        {
            Id = partner.Id,
            Name = partner.Name,
            Email = partner.Email,
            PhoneNumber = partner.PhoneNumber,
            CompanyName = partner.CompanyName,
            BusinessRegistrationNumber = partner.BusinessRegistrationNumber,
            CommissionRate = partner.CommissionRate,
            PaymentMethod = partner.PaymentMethod,
            BankAccountName = partner.BankDetails?.AccountName ?? "",
            BankAccountNumber = partner.BankDetails?.AccountNumber ?? "",
            BankName = partner.BankDetails?.BankName ?? "",
            BankCode = partner.BankDetails?.BankCode,
            Street = partner.Address?.Street ?? "",
            City = partner.Address?.City ?? "",
            State = partner.Address?.State ?? "",
            PostalCode = partner.Address?.PostalCode ?? "",
            Country = partner.Address?.Country ?? "Nigeria",
            ContactPerson = partner.ContactPerson,
            Notes = partner.Notes,
            IsActive = partner.IsActive,
            VerificationStatus = partner.VerificationStatus
        };
    }

    private CreatePartnerRequest MapToCreateRequest(PartnerFormData form)
    {
        return new CreatePartnerRequest
        {
            Name = form.Name,
            Email = form.Email,
            PhoneNumber = form.PhoneNumber,
            CompanyName = form.CompanyName,
            BusinessRegistrationNumber = form.BusinessRegistrationNumber,
            CommissionRate = form.CommissionRate,
            PaymentMethod = form.PaymentMethod,
            BankDetails = new BankDetails
            {
                AccountName = form.BankAccountName,
                AccountNumber = form.BankAccountNumber,
                BankName = form.BankName,
                BankCode = form.BankCode
            },
            Address = new PartnerAddress
            {
                Street = form.Street,
                City = form.City,
                State = form.State,
                PostalCode = form.PostalCode,
                Country = form.Country
            },
            ContactPerson = form.ContactPerson,
            Notes = form.Notes
        };
    }

    private UpdatePartnerRequest MapToUpdateRequest(PartnerFormData form)
    {
        return new UpdatePartnerRequest
        {
            Name = form.Name,
            Email = form.Email,
            PhoneNumber = form.PhoneNumber,
            CompanyName = form.CompanyName,
            BusinessRegistrationNumber = form.BusinessRegistrationNumber,
            CommissionRate = form.CommissionRate,
            PaymentMethod = form.PaymentMethod,
            BankDetails = new BankDetails
            {
                AccountName = form.BankAccountName,
                AccountNumber = form.BankAccountNumber,
                BankName = form.BankName,
                BankCode = form.BankCode
            },
            Address = new PartnerAddress
            {
                Street = form.Street,
                City = form.City,
                State = form.State,
                PostalCode = form.PostalCode,
                Country = form.Country
            },
            ContactPerson = form.ContactPerson,
            Notes = form.Notes,
            IsActive = form.IsActive,
            VerificationStatus = form.VerificationStatus
        };
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "PartnerManagement disposed successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disposing PartnerManagement");
        }
    }

    public class PartnerFormData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? CompanyName { get; set; }
        public string? BusinessRegistrationNumber { get; set; }
        
        public decimal CommissionRate { get; set; } = 50.00m;
        public string PaymentMethod { get; set; } = "bank_transfer";
        
        public string BankAccountName { get; set; } = "";
        public string BankAccountNumber { get; set; } = "";
        public string BankName { get; set; } = "";
        public string? BankCode { get; set; }
        
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string Country { get; set; } = "Nigeria";
        
        public string? ContactPerson { get; set; }
        public string? Notes { get; set; }
        
        public bool IsActive { get; set; } = true;
        public string VerificationStatus { get; set; } = "pending";
    }
}