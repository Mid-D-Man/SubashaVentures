using Microsoft.AspNetCore.Components;

namespace SubashaVentures.Pages.Checkout;

public partial class Checkout : ComponentBase
{
    // Step management
    private int currentStep = 1;
    
    // Step 1: Shipping Information
    private string firstName = "";
    private string lastName = "";
    private string phoneNumber = "";
    private string addressLine1 = "";
    private string addressLine2 = "";
    private string city = "";
    private string state = "";
    private string postalCode = "";
    private bool saveAddress = false;
    
    // Validation errors
    private string firstNameError = "";
    private string lastNameError = "";
    private string phoneError = "";
    private string addressError = "";
    private string cityError = "";
    private string stateError = "";
    private string postalError = "";
    
    // Step 2: Payment Method
    private string selectedPaymentMethod = "card";
    
    // Step 3: Order details
    private int orderItemsCount = 3;
    private bool isProcessing = false;
    
    // Order summary
    private decimal subtotal = 77000; // ₦77,000
    private decimal shippingCost = 2000; // ₦2,000
    private decimal discount = 0;
    private decimal orderTotal => subtotal + shippingCost - discount;

    protected override async Task OnInitializedAsync()
    {
        await LoadCheckoutData();
    }

    private async Task LoadCheckoutData()
    {
        // Load cart items, user addresses, etc.
        // In real app: await CheckoutService.InitializeCheckout();
        
        await Task.Delay(100);
        
        // Pre-fill if user has saved address
        // if (hasDefaultAddress) { ... }
    }

    // Step Navigation
    private async Task GoToPaymentStep()
    {
        if (!ValidateShippingInfo())
        {
            return;
        }
        
        currentStep = 2;
        await Task.CompletedTask;
    }

    private void GoToShippingStep()
    {
        currentStep = 1;
    }

    private async Task GoToReviewStep()
    {
        if (string.IsNullOrEmpty(selectedPaymentMethod))
        {
            return;
        }
        
        currentStep = 3;
        await Task.CompletedTask;
    }

    private void SelectPaymentMethod(string method)
    {
        selectedPaymentMethod = method;
    }

    // Validation
    private bool ValidateShippingInfo()
    {
        ClearErrors();
        bool isValid = true;
        
        if (string.IsNullOrWhiteSpace(firstName))
        {
            firstNameError = "First name is required";
            isValid = false;
        }
        
        if (string.IsNullOrWhiteSpace(lastName))
        {
            lastNameError = "Last name is required";
            isValid = false;
        }
        
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            phoneError = "Phone number is required";
            isValid = false;
        }
        else if (!IsValidNigerianPhone(phoneNumber))
        {
            phoneError = "Please enter a valid Nigerian phone number";
            isValid = false;
        }
        
        if (string.IsNullOrWhiteSpace(addressLine1))
        {
            addressError = "Address is required";
            isValid = false;
        }
        
        if (string.IsNullOrWhiteSpace(city))
        {
            cityError = "City is required";
            isValid = false;
        }
        
        if (string.IsNullOrWhiteSpace(state))
        {
            stateError = "State is required";
            isValid = false;
        }
        
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            postalError = "Postal code is required";
            isValid = false;
        }
        
        return isValid;
    }

    private void ClearErrors()
    {
        firstNameError = "";
        lastNameError = "";
        phoneError = "";
        addressError = "";
        cityError = "";
        stateError = "";
        postalError = "";
    }

    private bool IsValidNigerianPhone(string phone)
    {
        // Remove common formatting characters
        var cleaned = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        
        // Nigerian phone numbers: 11 digits starting with 0, or 10 digits without 0
        // Formats: 08012345678 or 8012345678 or +2348012345678
        if (cleaned.StartsWith("+234"))
        {
            cleaned = cleaned.Substring(4);
        }
        else if (cleaned.StartsWith("234"))
        {
            cleaned = cleaned.Substring(3);
        }
        else if (cleaned.StartsWith("0"))
        {
            cleaned = cleaned.Substring(1);
        }
        
        return cleaned.Length == 10 && cleaned.All(char.IsDigit);
    }

    private string GetPaymentMethodName(string method)
    {
        return method switch
        {
            "card" => "Credit/Debit Card",
            "transfer" => "Bank Transfer",
            "pod" => "Pay on Delivery",
            _ => "Unknown"
        };
    }

    private async Task PlaceOrder()
    {
        if (isProcessing) return;
        
        isProcessing = true;
        StateHasChanged();
        
        try
        {
            // Simulate order processing
            await Task.Delay(2000);
            
            // In real app:
            // var order = new CreateOrderDto
            // {
            //     ShippingAddress = new AddressDto { ... },
            //     PaymentMethod = selectedPaymentMethod,
            //     Items = cartItems,
            //     Total = orderTotal
            // };
            // var result = await OrderService.CreateOrder(order);
            
            Console.WriteLine("Order placed successfully!");
            Console.WriteLine($"Payment Method: {selectedPaymentMethod}");
            Console.WriteLine($"Total: ₦{orderTotal:N0}");
            Console.WriteLine($"Shipping to: {firstName} {lastName}, {city}, {state}");
            
            // Navigate to order confirmation page
            // NavigationManager.NavigateTo($"/order-confirmation/{orderId}");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error placing order: {ex.Message}");
            // Show error message to user
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }
}