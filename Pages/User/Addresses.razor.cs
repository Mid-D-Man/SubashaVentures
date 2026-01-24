// Pages/User/Addresses.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Addresses;
using System.Security.Claims;

namespace SubashaVentures.Pages.User;

public partial class Addresses
{
    [Inject] private IAddressService AddressService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private List<AddressViewModel> AddressList = new();
    private Dictionary<string, bool> SelectedAddresses = new();
    private AddressViewModel CurrentAddress = new();
    private List<string> ValidationErrors = new();
    
    private DynamicModal? AddressModal;
    private ConfirmationPopup? DeleteConfirmPopup;
    
    private bool IsLoading = true;
    private bool IsModalOpen = false;
    private bool IsSaving = false;
    private bool IsEditMode = false;
    private string? CurrentUserId;

    private readonly List<string> NigerianStates = new()
    {
        "Abia", "Adamawa", "Akwa Ibom", "Anambra", "Bauchi", "Bayelsa", "Benue", 
        "Borno", "Cross River", "Delta", "Ebonyi", "Edo", "Ekiti", "Enugu", 
        "FCT - Abuja", "Gombe", "Imo", "Jigawa", "Kaduna", "Kano", "Katsina", 
        "Kebbi", "Kogi", "Kwara", "Lagos", "Nasarawa", "Niger", "Ogun", "Ondo", 
        "Osun", "Oyo", "Plateau", "Rivers", "Sokoto", "Taraba", "Yobe", "Zamfara"
    };

    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserId();
        await LoadAddresses();
    }

    private async Task GetCurrentUserId()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            CurrentUserId = authState.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? authState.User?.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Console.WriteLine("❌ User not authenticated");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error getting user ID: {ex.Message}");
        }
    }

    private async Task LoadAddresses()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Console.WriteLine("❌ Cannot load addresses: User not authenticated");
                return;
            }

            AddressList = await AddressService.GetUserAddressesAsync(CurrentUserId);
            SelectedAddresses = AddressList.ToDictionary(a => a.Id, _ => false);

            Console.WriteLine($"✅ Loaded {AddressList.Count} addresses");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading addresses: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private void OpenAddAddressModal()
    {
        IsEditMode = false;
        CurrentAddress = new AddressViewModel
        {
            Country = "Nigeria",
            Type = AddressType.Shipping,
            City = "Lagos",
            State = "Lagos",
            PostalCode = "100001"
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
            PhoneNumber = address.PhoneNumber,
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

    private async Task SaveAddress()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            Console.WriteLine("❌ Cannot save: User not authenticated");
            return;
        }

        // Validate using service
        var validationResult = AddressService.ValidateAddress(CurrentAddress);
        if (!validationResult.Result.IsValid)
        {
            ValidationErrors = validationResult.Result.Errors;
            StateHasChanged();
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
                Console.WriteLine(success ? "✅ Address updated" : "❌ Failed to update address");
            }
            else
            {
                success = await AddressService.AddAddressAsync(CurrentUserId, CurrentAddress);
                Console.WriteLine(success ? "✅ Address added" : "❌ Failed to add address");
            }

            if (success)
            {
                await LoadAddresses();
                CloseModal();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error saving address: {ex.Message}");
        }
        finally
        {
            IsSaving = false;
            StateHasChanged();
        }
    }

    private void CloseModal()
    {
        IsModalOpen = false;
        CurrentAddress = new();
        ValidationErrors.Clear();
        StateHasChanged();
    }

    private void DeleteSelectedAddresses()
    {
        DeleteConfirmPopup?.Open();
    }

    private async Task ConfirmDeleteAddresses()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            Console.WriteLine("❌ Cannot delete: User not authenticated");
            return;
        }

        try
        {
            var toDelete = SelectedAddresses.Where(x => x.Value).Select(x => x.Key).ToList();
            
            foreach (var addressId in toDelete)
            {
                var success = await AddressService.DeleteAddressAsync(CurrentUserId, addressId);
                if (success)
                {
                    Console.WriteLine($"✅ Deleted address: {addressId}");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to delete address: {addressId}");
                }
            }

            await LoadAddresses();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error deleting addresses: {ex.Message}");
        }
    }

    private async Task SetDefaultAddress(string addressId)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            Console.WriteLine("❌ Cannot set default: User not authenticated");
            return;
        }

        try
        {
            var success = await AddressService.SetDefaultAddressAsync(CurrentUserId, addressId);
            
            if (success)
            {
                Console.WriteLine($"✅ Set default address: {addressId}");
                await LoadAddresses();
            }
            else
            {
                Console.WriteLine($"❌ Failed to set default address: {addressId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error setting default address: {ex.Message}");
        }
    }
}
