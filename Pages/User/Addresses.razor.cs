using Microsoft.AspNetCore.Components;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.User;

namespace SubashaVentures.Pages.User;

public partial class Addresses
{
    private List<AddressViewModel> AddressList = new();
    private Dictionary<string, bool> SelectedAddresses = new();
    private AddressViewModel CurrentAddress = new();
    private Dictionary<string, string> ValidationErrors = new();
    
    private DynamicModal? AddressModal;
    private ConfirmationPopup? DeleteConfirmPopup;
    
    private bool IsLoading = true;
    private bool IsModalOpen = false;
    private bool IsSaving = false;
    private bool IsEditMode = false;

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
        await LoadAddresses();
    }

    private async Task LoadAddresses()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            // TODO: Load from actual service
            await Task.Delay(500);
            
            // Mock data
            AddressList = new List<AddressViewModel>
            {
                new()
                {
                    Id = "1",
                    FullName = "John Doe",
                    PhoneNumber = "+234 801 234 5678",
                    AddressLine1 = "15 Allen Avenue",
                    AddressLine2 = "Ikeja",
                    City = "Lagos",
                    State = "Lagos",
                    PostalCode = "100001",
                    Country = "Nigeria",
                    IsDefault = true,
                    Type = AddressType.Both
                }
            };
            
            SelectedAddresses = AddressList.ToDictionary(a => a.Id, _ => false);
        }
        catch (Exception ex)
        {
            // Handle error
            Console.WriteLine($"Error loading addresses: {ex.Message}");
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
            Type = AddressType.Shipping
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
        if (!ValidateAddress())
            return;

        IsSaving = true;
        StateHasChanged();

        try
        {
            // TODO: Save to actual service
            await Task.Delay(1000);

            if (IsEditMode)
            {
                var existing = AddressList.FirstOrDefault(a => a.Id == CurrentAddress.Id);
                if (existing != null)
                {
                    var index = AddressList.IndexOf(existing);
                    AddressList[index] = CurrentAddress;
                }
            }
            else
            {
                CurrentAddress.Id = Guid.NewGuid().ToString();
                AddressList.Add(CurrentAddress);
                SelectedAddresses[CurrentAddress.Id] = false;
            }

            if (CurrentAddress.IsDefault)
            {
                foreach (var addr in AddressList.Where(a => a.Id != CurrentAddress.Id))
                {
                    addr.IsDefault = false;
                }
            }

            CloseModal();
        }
        catch (Exception ex)
        {
            // Handle error
            Console.WriteLine($"Error saving address: {ex.Message}");
        }
        finally
        {
            IsSaving = false;
            StateHasChanged();
        }
    }

    private bool ValidateAddress()
    {
        ValidationErrors.Clear();

        if (string.IsNullOrWhiteSpace(CurrentAddress.FullName))
            ValidationErrors["FullName"] = "Full name is required";

        if (string.IsNullOrWhiteSpace(CurrentAddress.PhoneNumber))
            ValidationErrors["PhoneNumber"] = "Phone number is required";

        if (string.IsNullOrWhiteSpace(CurrentAddress.AddressLine1))
            ValidationErrors["AddressLine1"] = "Address is required";

        if (string.IsNullOrWhiteSpace(CurrentAddress.City))
            ValidationErrors["City"] = "City is required";

        if (string.IsNullOrWhiteSpace(CurrentAddress.PostalCode))
            ValidationErrors["PostalCode"] = "Postal code is required";

        return !ValidationErrors.Any();
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
        try
        {
            var toDelete = SelectedAddresses.Where(x => x.Value).Select(x => x.Key).ToList();
            
            foreach (var id in toDelete)
            {
                var address = AddressList.FirstOrDefault(a => a.Id == id);
                if (address != null)
                {
                    AddressList.Remove(address);
                    SelectedAddresses.Remove(id);
                }
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting addresses: {ex.Message}");
        }
    }

    private async Task SetDefaultAddress(string addressId)
    {
        try
        {
            foreach (var addr in AddressList)
            {
                addr.IsDefault = addr.Id == addressId;
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting default address: {ex.Message}");
        }
    }
}
