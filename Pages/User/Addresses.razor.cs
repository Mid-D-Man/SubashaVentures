// Pages/User/Addresses.razor.cs - COMPLETE WITH ALL LOGIC
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.User;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Services.Addresses;
using SubashaVentures.Services.Geolocation;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Utilities.HelperScripts;
using System.Security.Claims;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class Addresses
{
    [Inject] private IAddressService AddressService { get; set; } = default!;
    [Inject] private IGeolocationService GeolocationService { get; set; } = default!;
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ILogger<Addresses> Logger { get; set; } = default!;

    // Data
    private List<AddressViewModel> AddressList = new();
    private Dictionary<string, bool> SelectedAddresses = new();
    private AddressViewModel CurrentAddress = new();
    private List<string> ValidationErrors = new();
    
    // Component references
    private DynamicModal? AddressModal;
    private ConfirmationPopup? DeleteConfirmPopup;
    private InfoPopup? AutoFillInfoPopup;
    
    // State flags
    private bool IsLoading = true;
    private bool IsModalOpen = false;
    private bool IsSaving = false;
    private bool IsEditMode = false;
    private bool IsAutoFilling = false;
    private string? CurrentUserId;

    // Auto-fill popup properties
    private string AutoFillPopupTitle = "";
    private string AutoFillPopupMessage = "";
    private InfoPopup.InfoPopupIcon AutoFillPopupIcon = InfoPopup.InfoPopupIcon.Info;

    // SVG icons
    private string addressIcon = string.Empty;
    private string addressIconLarge = string.Empty;
    private string phoneIcon = string.Empty;
    private string mailIcon = string.Empty;
    private string starIcon = string.Empty;
    private string editIcon = string.Empty;
    private string lockIcon = string.Empty;
    private string globeIcon = string.Empty;
    private string warningIcon = string.Empty;

    // Nigerian states
    private readonly List<string> NigerianStates = new()
    {
        "Abia", "Adamawa", "Akwa Ibom", "Anambra", "Bauchi", "Bayelsa", "Benue", 
        "Borno", "Cross River", "Delta", "Ebonyi", "Edo", "Ekiti", "Enugu", 
        "FCT - Abuja", "Gombe", "Imo", "Jigawa", "Kaduna", "Kano", "Katsina", 
        "Kebbi", "Kogi", "Kwara", "Lagos", "Nasarawa", "Niger", "Ogun", "Ondo", 
        "Osun", "Oyo", "Plateau", "Rivers", "Sokoto", "Taraba", "Yobe", "Zamfara"
    };

    // Postal code mapping for major Nigerian cities/states
    private readonly Dictionary<string, string> StatePostalCodes = new()
    {
        { "Lagos", "100001" },
        { "FCT - Abuja", "900001" },
        { "Kano", "700001" },
        { "Rivers", "500001" },
        { "Oyo", "200001" },
        { "Delta", "320001" },
        { "Ogun", "110001" },
        { "Kaduna", "800001" },
        { "Edo", "300001" },
        { "Imo", "460001" },
        { "Enugu", "400001" },
        { "Anambra", "420001" },
        { "Akwa Ibom", "520001" },
        { "Abia", "440001" },
        { "Plateau", "930001" },
        { "Cross River", "540001" },
        { "Osun", "230001" },
        { "Ondo", "340001" },
        { "Kwara", "240001" },
        { "Benue", "970001" }
    };

    // ==================== INITIALIZATION ====================

    protected override async Task OnInitializedAsync()
    {
        await LoadSvgIcons();
        await GetCurrentUserId();
        await LoadAddresses();
    }

    private async Task LoadSvgIcons()
    {
        try
        {
            addressIcon = await VisualElements.GetCustomSvgAsync(
                SvgType.Address, width: 16, height: 16, fillColor: "currentColor"
            );

            addressIconLarge = await VisualElements.GetCustomSvgAsync(
                SvgType.Address, width: 64, height: 64, fillColor: "var(--primary-color)"
            );

            phoneIcon = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M6.62 10.79c1.44 2.83 3.76 5.15 6.59 6.59l2.2-2.2c.28-.28.67-.36 1.02-.25 1.12.37 2.32.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z'/>",
                16, 16, "0 0 24 24"
            );

            mailIcon = await VisualElements.GetCustomSvgAsync(
                SvgType.Mail, width: 16, height: 16, fillColor: "currentColor"
            );

            starIcon = await VisualElements.GetCustomSvgAsync(
                SvgType.Star, width: 16, height: 16, fillColor: "currentColor"
            );

            editIcon = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z'/>",
                18, 18, "0 0 24 24"
            );

            lockIcon = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zM9 6c0-1.66 1.34-3 3-3s3 1.34 3 3v2H9V6zm9 14H6V10h12v10zm-6-3c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2z'/>",
                20, 20, "0 0 24 24"
            );

            globeIcon = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z'/>",
                20, 20, "0 0 24 24"
            );

            warningIcon = await VisualElements.GetCustomSvgAsync(
                SvgType.Warning, width: 16, height: 16, fillColor: "currentColor"
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading SVG icons");
            Logger.LogError(ex, "Error loading SVG icons");
        }
    }

    private async Task GetCurrentUserId()
    {
        try
        {
            CurrentUserId = await PermissionService.GetCurrentUserIdAsync();

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ User not authenticated",
                    LogLevel.Warning
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ User authenticated: {CurrentUserId}",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting user ID");
            Logger.LogError(ex, "Error getting user ID");
        }
    }

    // ==================== LOAD ADDRESSES ====================

    private async Task LoadAddresses()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Cannot load addresses: User not authenticated",
                    LogLevel.Warning
                );
                return;
            }

            AddressList = await AddressService.GetUserAddressesAsync(CurrentUserId);
            SelectedAddresses = AddressList.ToDictionary(a => a.Id, _ => false);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded {AddressList.Count} addresses",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading addresses");
            Logger.LogError(ex, "Error loading addresses");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    // ==================== MODAL OPERATIONS ====================

    private void OpenAddAddressModal()
    {
        IsEditMode = false;
        CurrentAddress = new AddressViewModel
        {
            Country = "Nigeria",
            Type = AddressType.Shipping,
            PhoneNumber = "+234"  // Start with Nigeria prefix
        };
        ValidationErrors.Clear();
        IsModalOpen = true;
        StateHasChanged();
    }

    private void EditAddress(AddressViewModel address)
    {
        IsEditMode = true;
        CurrentAddress = new AddressViewModel
        {
            Id = address.Id,
            FullName = address.FullName,
            PhoneNumber = NormalizePhoneNumber(address.PhoneNumber),
            Email = address.Email,
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode,
            Country = address.Country,
            IsDefault = address.IsDefault,
            Type = address.Type
        };
        ValidationErrors.Clear();
        IsModalOpen = true;
        StateHasChanged();
    }

    private void CloseModal()
    {
        IsModalOpen = false;
        CurrentAddress = new();
        ValidationErrors.Clear();
        StateHasChanged();
    }

    // ==================== PHONE NUMBER HANDLING ====================

    private string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return "+234";

        // Remove all non-digit characters except +
        var cleaned = new string(phoneNumber.Where(c => char.IsDigit(c) || c == '+').ToArray());

        // Ensure it starts with +234
        if (!cleaned.StartsWith("+234"))
        {
            // If it starts with 234, add +
            if (cleaned.StartsWith("234"))
            {
                cleaned = "+" + cleaned;
            }
            // If it starts with 0, replace with +234
            else if (cleaned.StartsWith("0"))
            {
                cleaned = "+234" + cleaned.Substring(1);
            }
            // Otherwise, add +234 prefix
            else
            {
                cleaned = "+234" + cleaned;
            }
        }

        // Limit to +234 plus 10 digits (total 14 characters)
        if (cleaned.Length > 14)
        {
            cleaned = cleaned.Substring(0, 14);
        }

        return cleaned;
    }

    private void HandlePhoneNumberChange(string value)
    {
        CurrentAddress.PhoneNumber = NormalizePhoneNumber(value);
        StateHasChanged();
    }

    // ==================== AUTO-FILL ADDRESS ====================

    private async Task AutoFillAddress()
    {
        IsAutoFilling = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🌍 Starting auto-fill address process (GPS preferred)",
                LogLevel.Info
            );

            // Get location (tries GPS first, falls back to IP)
            var locationData = await GeolocationService.GetLocationAsync();

            if (locationData == null)
            {
                await ShowAutoFillError(
                    "Unable to detect your location automatically. Please enter your address manually."
                );
                return;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Location detected: {locationData.City}, {locationData.State}",
                LogLevel.Info
            );

            // Get user information
            if (!string.IsNullOrEmpty(CurrentUserId))
            {
                var user = await UserService.GetUserByIdAsync(CurrentUserId);
                
                if (user != null)
                {
                    // Auto-fill name if not already set
                    if (string.IsNullOrEmpty(CurrentAddress.FullName))
                    {
                        CurrentAddress.FullName = $"{user.FirstName} {user.LastName}".Trim();
                        
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"✅ Name filled: {CurrentAddress.FullName}",
                            LogLevel.Info
                        );
                    }

                    // Auto-fill phone if not already set
                    if (string.IsNullOrEmpty(CurrentAddress.PhoneNumber) || CurrentAddress.PhoneNumber == "+234")
                    {
                        if (!string.IsNullOrEmpty(user.PhoneNumber))
                        {
                            CurrentAddress.PhoneNumber = NormalizePhoneNumber(user.PhoneNumber);
                            
                            await MID_HelperFunctions.DebugMessageAsync(
                                $"✅ Phone filled: {CurrentAddress.PhoneNumber}",
                                LogLevel.Info
                            );
                        }
                    }

                    // Auto-fill email if not already set
                    if (string.IsNullOrEmpty(CurrentAddress.Email) && 
                        !string.IsNullOrEmpty(user.Email))
                    {
                        CurrentAddress.Email = user.Email;
                        
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"✅ Email filled: {CurrentAddress.Email}",
                            LogLevel.Info
                        );
                    }
                }
            }

            // Fill location data if available
            if (!string.IsNullOrEmpty(locationData.City))
            {
                CurrentAddress.City = locationData.City;
            }

            if (!string.IsNullOrEmpty(locationData.State))
            {
                CurrentAddress.State = locationData.State;
            }

            CurrentAddress.Country = locationData.Country;
            
            // Set address line 1 if we have city data
            if (!string.IsNullOrEmpty(locationData.City) && 
                string.IsNullOrEmpty(CurrentAddress.AddressLine1))
            {
                CurrentAddress.AddressLine1 = locationData.AddressLine1;
            }

            // Try to set postal code
            if (string.IsNullOrEmpty(CurrentAddress.PostalCode))
            {
                if (!string.IsNullOrEmpty(locationData.PostalCode))
                {
                    CurrentAddress.PostalCode = locationData.PostalCode;
                }
                else if (!string.IsNullOrEmpty(CurrentAddress.State) && 
                         StatePostalCodes.TryGetValue(CurrentAddress.State, out var postalCode))
                {
                    CurrentAddress.PostalCode = postalCode;
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Address auto-fill completed successfully",
                LogLevel.Info
            );

            // Show success message
            var locationInfo = !string.IsNullOrEmpty(locationData.City) 
                ? $"{locationData.City}, {locationData.State}" 
                : locationData.FormattedAddress;

            await ShowAutoFillSuccess(
                $"Address auto-filled based on your location: {locationInfo}. Please review and complete any missing details."
            );

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Auto-filling address");
            Logger.LogError(ex, "Error during address auto-fill");
            
            await ShowAutoFillError(
                "An error occurred while detecting your location. Please enter your address manually."
            );
        }
        finally
        {
            IsAutoFilling = false;
            StateHasChanged();
        }
    }

    private async Task ShowAutoFillSuccess(string message)
    {
        AutoFillPopupTitle = "Location Detected";
        AutoFillPopupMessage = message;
        AutoFillPopupIcon = InfoPopup.InfoPopupIcon.Success;
        AutoFillInfoPopup?.Show();
        
        await Task.CompletedTask;
    }

    private async Task ShowAutoFillError(string message)
    {
        AutoFillPopupTitle = "Auto-Fill Failed";
        AutoFillPopupMessage = message;
        AutoFillPopupIcon = InfoPopup.InfoPopupIcon.Warning;
        AutoFillInfoPopup?.Show();
        
        await Task.CompletedTask;
    }

    private void CloseAutoFillPopup()
    {
        AutoFillInfoPopup?.Close();
    }

    // ==================== SAVE ADDRESS ====================

    private async Task SaveAddress()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "❌ Cannot save: User not authenticated",
                LogLevel.Warning
            );
            return;
        }

        // Validate using service
        var validationResult = await AddressService.ValidateAddress(CurrentAddress);
        if (!validationResult.IsValid)
        {
            ValidationErrors = validationResult.Errors;
            StateHasChanged();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"❌ Address validation failed: {string.Join(", ", ValidationErrors)}",
                LogLevel.Warning
            );
            return;
        }

        IsSaving = true;
        StateHasChanged();

        try
        {
            bool success;

            if (IsEditMode)
            {
                success = await AddressService.UpdateAddressAsync(CurrentUserId, CurrentAddress);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    success ? "✅ Address updated successfully" : "❌ Failed to update address",
                    success ? LogLevel.Info : LogLevel.Error
                );
            }
            else
            {
                success = await AddressService.AddAddressAsync(CurrentUserId, CurrentAddress);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    success ? "✅ Address added successfully" : "❌ Failed to add address",
                    success ? LogLevel.Info : LogLevel.Error
                );
            }

            if (success)
            {
                await LoadAddresses();
                CloseModal();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving address");
            Logger.LogError(ex, "Error saving address");
        }
        finally
        {
            IsSaving = false;
            StateHasChanged();
        }
    }

    // ==================== DELETE ADDRESSES ====================

    private void DeleteSelectedAddresses()
    {
        DeleteConfirmPopup?.Open();
    }

    private async Task ConfirmDeleteAddresses()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "❌ Cannot delete: User not authenticated",
                LogLevel.Warning
            );
            return;
        }

        try
        {
            var toDelete = SelectedAddresses.Where(x => x.Value).Select(x => x.Key).ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🗑️ Deleting {toDelete.Count} addresses",
                LogLevel.Info
            );
            
            foreach (var addressId in toDelete)
            {
                var success = await AddressService.DeleteAddressAsync(CurrentUserId, addressId);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    success 
                        ? $"✅ Deleted address: {addressId}" 
                        : $"❌ Failed to delete address: {addressId}",
                    success ? LogLevel.Info : LogLevel.Error
                );
            }

            await LoadAddresses();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting addresses");
            Logger.LogError(ex, "Error deleting addresses");
        }
    }

    private async Task SetDefaultAddress(string addressId)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "❌ Cannot set default: User not authenticated",
                LogLevel.Warning
            );
            return;
        }

        try
        {
            var success = await AddressService.SetDefaultAddressAsync(CurrentUserId, addressId);
            
            await MID_HelperFunctions.DebugMessageAsync(
                success 
                    ? $"✅ Set default address: {addressId}" 
                    : $"❌ Failed to set default address: {addressId}",
                success ? LogLevel.Info : LogLevel.Error
            );

            if (success)
            {
                await LoadAddresses();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Setting default address");
            Logger.LogError(ex, "Error setting default address");
        }
    }
}
