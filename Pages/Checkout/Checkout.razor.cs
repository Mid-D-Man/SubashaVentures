// Pages/Checkout/Checkout.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Checkout;
using SubashaVentures.Domain.Cart;
using SubashaVentures.Domain.User;
using SubashaVentures.Domain.Order;
using SubashaVentures.Domain.Payment;
using SubashaVentures.Services.Checkout;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.Payment;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Checkout;

public partial class Checkout : ComponentBase
{
    [Parameter] public string Slug { get; set; } = string.Empty;
    
    // Query parameters for product-specific checkout
    [SupplyParameterFromQuery(Name = "productId")]
    public string? ProductId { get; set; }
    
    [SupplyParameterFromQuery(Name = "quantity")]
    public int? Quantity { get; set; }
    
    [SupplyParameterFromQuery(Name = "size")]
    public string? Size { get; set; }
    
    [SupplyParameterFromQuery(Name = "color")]
    public string? Color { get; set; }

    [Inject] private ICheckoutService CheckoutService { get; set; } = default!;
    [Inject] private ICartService CartService { get; set; } = default!;
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private IWalletService WalletService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<Checkout> Logger { get; set; } = default!;

    // State
    private CheckoutViewModel? Checkout;
    private List<AddressViewModel> UserAddresses = new();
    private List<ShippingMethodViewModel> ShippingMethods = new();
    private ShippingMethodViewModel? SelectedShippingMethodObj;
    
    private bool IsLoading = true;
    private bool IsLoadingShipping = false;
    private bool IsProcessing = false;
    private bool ShowAddressForm = false;
    private bool ShowSuccessModal = false;
    private bool ShowErrorModal = false;
    
    private int CurrentStep = 1;
    private string? CurrentUserId;
    private string? SelectedAddressId;
    private string SelectedShippingMethod = "";
    private string SelectedPaymentMethod = "Card";
    private bool SaveNewAddress = false;
    private decimal WalletBalance = 0;
    private string OrderNumber = "";
    private string ErrorMessage = "";
    
    // New address form
    private AddressViewModel NewAddress = new();
    
    // Modal references
    private DynamicModal? SuccessModal;
    private InfoPopup? ErrorPopup;
    
    // Computed properties
    private bool HasValidAddress => 
        !string.IsNullOrEmpty(SelectedAddressId) || 
        (!string.IsNullOrEmpty(NewAddress.FirstName) && 
         !string.IsNullOrEmpty(NewAddress.LastName) &&
         !string.IsNullOrEmpty(NewAddress.PhoneNumber) &&
         !string.IsNullOrEmpty(NewAddress.Email) &&
         !string.IsNullOrEmpty(NewAddress.AddressLine1) &&
         !string.IsNullOrEmpty(NewAddress.City) &&
         !string.IsNullOrEmpty(NewAddress.State) &&
         !string.IsNullOrEmpty(NewAddress.PostalCode));
    
    private bool IsPaymentMethodValid
    {
        get
        {
            if (SelectedPaymentMethod == "Wallet")
            {
                return WalletBalance >= Checkout?.Total;
            }
            return !string.IsNullOrEmpty(SelectedPaymentMethod);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IsLoading = true;

            // Ensure authenticated
            if (!await PermissionService.EnsureAuthenticatedAsync($"checkout/{Slug}"))
            {
                Navigation.NavigateTo("signin", true);
                return;
            }

            CurrentUserId = await PermissionService.GetCurrentUserIdAsync();
            
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User ID not found, redirecting to sign in",
                    LogLevel.Error
                );
                Navigation.NavigateTo("signin", true);
                return;
            }

            await LoadCheckoutData();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing checkout");
            Logger.LogError(ex, "Failed to initialize checkout");
            ErrorMessage = "Failed to load checkout. Please try again.";
            ShowErrorModal = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadCheckoutData()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading checkout data for user: {CurrentUserId}",
                LogLevel.Info
            );

            // Determine checkout type: product-specific or cart-based
            if (!string.IsNullOrEmpty(ProductId))
            {
                // Product-specific checkout
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Product checkout: {ProductId}, Qty: {Quantity}, Size: {Size}, Color: {Color}",
                    LogLevel.Info
                );
                
                Checkout = await CheckoutService.InitializeFromProductAsync(
                    ProductId,
                    Quantity ?? 1,
                    Size,
                    Color
                );
            }
            else
            {
                // Cart-based checkout
                await MID_HelperFunctions.DebugMessageAsync(
                    "Cart-based checkout",
                    LogLevel.Info
                );
                
                Checkout = await CheckoutService.InitializeFromCartAsync(CurrentUserId!);
            }

            if (Checkout == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Failed to initialize checkout",
                    LogLevel.Error
                );
                return;
            }

            // Load user data
            await Task.WhenAll(
                LoadUserAddresses(),
                LoadWalletBalance()
            );

            await MID_HelperFunctions.DebugMessageAsync(
                $"Checkout loaded: {Checkout.Items.Count} items, Total: ₦{Checkout.Total:N0}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading checkout data");
            throw;
        }
    }

    private async Task LoadUserAddresses()
    {
        try
        {
            UserAddresses = await UserService.GetUserAddressesAsync(CurrentUserId!);
            
            // Select default address if available
            var defaultAddress = UserAddresses.FirstOrDefault(a => a.IsDefault);
            if (defaultAddress != null)
            {
                SelectedAddressId = defaultAddress.Id;
                UpdateCheckoutAddress(defaultAddress);
            }
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {UserAddresses.Count} addresses",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading user addresses");
            UserAddresses = new List<AddressViewModel>();
        }
    }

    private async Task LoadWalletBalance()
    {
        try
        {
            var wallet = await WalletService.GetWalletAsync(CurrentUserId!);
            WalletBalance = wallet?.Balance ?? 0;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Wallet balance: ₦{WalletBalance:N0}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading wallet balance");
            WalletBalance = 0;
        }
    }

    // ==================== STEP NAVIGATION ====================

    private async Task GoToDeliveryStep()
    {
        if (!HasValidAddress)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Cannot proceed: Invalid address",
                LogLevel.Warning
            );
            return;
        }

        // Save address if needed
        if (!string.IsNullOrEmpty(SelectedAddressId))
        {
            var selectedAddress = UserAddresses.FirstOrDefault(a => a.Id == SelectedAddressId);
            if (selectedAddress != null)
            {
                UpdateCheckoutAddress(selectedAddress);
            }
        }
        else
        {
            // Create new address
            var newAddress = await SaveNewAddressIfNeeded();
            if (newAddress != null)
            {
                UpdateCheckoutAddress(newAddress);
            }
        }

        // Load shipping methods
        CurrentStep = 2;
        await LoadShippingMethods();
    }

    private void GoToShippingStep()
    {
        CurrentStep = 1;
    }

    private void GoToPaymentStep()
    {
        if (string.IsNullOrEmpty(SelectedShippingMethod))
        {
            return;
        }

        CurrentStep = 3;
    }

    private void GoToReviewStep()
    {
        if (!IsPaymentMethodValid)
        {
            return;
        }

        CurrentStep = 4;
    }

    // ==================== ADDRESS MANAGEMENT ====================

    private void SelectAddress(AddressViewModel address)
    {
        SelectedAddressId = address.Id;
        UpdateCheckoutAddress(address);
        StateHasChanged();
    }

    private void UpdateCheckoutAddress(AddressViewModel address)
    {
        if (Checkout == null) return;

        Checkout.ShippingAddress = address;
        
        await MID_HelperFunctions.DebugMessageAsync(
            $"Shipping address selected: {address.City}, {address.State}",
            LogLevel.Info
        );
    }

    private void ShowNewAddressForm()
    {
        ShowAddressForm = true;
        NewAddress = new AddressViewModel
        {
            Country = "Nigeria",
            UserId = CurrentUserId!
        };
    }

    private void HideNewAddressForm()
    {
        ShowAddressForm = false;
        NewAddress = new();
    }

    private async Task<AddressViewModel?> SaveNewAddressIfNeeded()
    {
        if (!SaveNewAddress) return NewAddress;

        try
        {
            var savedAddress = await UserService.AddUserAddressAsync(NewAddress);
            if (savedAddress != null)
            {
                UserAddresses.Add(savedAddress);
                SelectedAddressId = savedAddress.Id;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    "New address saved successfully",
                    LogLevel.Info
                );
                
                return savedAddress;
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving new address");
        }

        return NewAddress;
    }

    // ==================== SHIPPING METHODS ====================

    private async Task LoadShippingMethods()
    {
        if (Checkout == null) return;

        IsLoadingShipping = true;
        StateHasChanged();

        try
        {
            // Convert to CheckoutItemViewModel
            var checkoutItems = Checkout.Items.Select(i => new CheckoutItemViewModel
            {
                ProductId = i.ProductId,
                Name = i.Name,
                ImageUrl = i.ImageUrl,
                Price = i.Price,
                Quantity = i.Quantity,
                Size = i.Size,
                Color = i.Color,
                Sku = i.Sku
            }).ToList();

            ShippingMethods = await CheckoutService.GetShippingMethodsAsync(
                CurrentUserId!,
                checkoutItems
            );

            // Auto-select first method
            if (ShippingMethods.Any())
            {
                SelectShippingMethod(ShippingMethods.First());
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {ShippingMethods.Count} shipping methods",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading shipping methods");
            ShippingMethods = new List<ShippingMethodViewModel>();
        }
        finally
        {
            IsLoadingShipping = false;
            StateHasChanged();
        }
    }

    private void SelectShippingMethod(ShippingMethodViewModel method)
    {
        SelectedShippingMethod = method.Id;
        SelectedShippingMethodObj = method;
        
        if (Checkout != null)
        {
            Checkout.ShippingMethod = method.Name;
            Checkout.ShippingCost = method.Cost;
            Checkout.ShippingRateId = method.Id;
        }

        StateHasChanged();
        
        MID_HelperFunctions.DebugMessageAsync(
            $"Shipping method selected: {method.Name} (₦{method.Cost:N0})",
            LogLevel.Info
        );
    }

    // ==================== PAYMENT ====================

    private void SelectPaymentMethod(string method)
    {
        SelectedPaymentMethod = method;
        
        if (Checkout != null)
        {
            Checkout.PaymentMethod = method switch
            {
                "Card" => PaymentMethod.Card,
                "Wallet" => PaymentMethod.Wallet,
                "PayOnDelivery" => PaymentMethod.PayOnDelivery,
                _ => PaymentMethod.Card
            };
        }

        StateHasChanged();
    }

    private string GetPaymentMethodDisplay(string method)
    {
        return method switch
        {
            "Card" => "Credit/Debit Card (Paystack/Flutterwave)",
            "Wallet" => $"Wallet (Balance: {FormatCurrency(WalletBalance)})",
            "PayOnDelivery" => "Pay on Delivery",
            _ => method
        };
    }

    // ==================== ORDER PLACEMENT ====================

    private async Task PlaceOrder()
    {
        if (Checkout == null || IsProcessing) return;

        IsProcessing = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Placing order: {Checkout.Items.Count} items, Total: ₦{Checkout.Total:N0}",
                LogLevel.Info
            );

            // Validate checkout
            var validation = await CheckoutService.ValidateCheckoutAsync(Checkout);
            if (!validation.IsValid)
            {
                ErrorMessage = string.Join("\n", validation.Errors);
                ShowErrorModal = true;
                return;
            }

            // Handle payment based on method
            OrderPlacementResult? result = null;

            if (SelectedPaymentMethod == "Card")
            {
                // Card payment
                result = await ProcessCardPayment();
            }
            else if (SelectedPaymentMethod == "Wallet")
            {
                // Wallet payment
                result = await ProcessWalletPayment();
            }
            else if (SelectedPaymentMethod == "PayOnDelivery")
            {
                // Pay on delivery - just create order
                result = await CheckoutService.PlaceOrderAsync(Checkout);
            }

            if (result != null && result.Success)
            {
                OrderNumber = result.OrderNumber ?? "";
                
                // Remove from cart if cart-based checkout
                if (string.IsNullOrEmpty(ProductId))
                {
                    await ClearCartAfterOrder();
                }
                
                ShowSuccessModal = true;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Order placed successfully: {OrderNumber}",
                    LogLevel.Info
                );
            }
            else
            {
                ErrorMessage = result?.Message ?? "Failed to place order";
                ShowErrorModal = true;
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Placing order");
            Logger.LogError(ex, "Failed to place order");
            ErrorMessage = "An error occurred while placing your order. Please try again.";
            ShowErrorModal = true;
        }
        finally
        {
            IsProcessing = false;
            StateHasChanged();
        }
    }

    private async Task<OrderPlacementResult?> ProcessCardPayment()
    {
        try
        {
            var paymentRequest = new PaymentRequest
            {
                Email = Checkout!.ShippingAddress!.Email,
                CustomerName = $"{Checkout.ShippingAddress.FirstName} {Checkout.ShippingAddress.LastName}",
                PhoneNumber = Checkout.ShippingAddress.PhoneNumber,
                Amount = Checkout.Total,
                Currency = "NGN",
                Reference = PaymentService.GenerateReference(),
                Provider = PaymentProvider.Paystack,
                UserId = CurrentUserId,
                Description = "Order Payment",
                Metadata = new Dictionary<string, object>
                {
                    { "order_type", "ecommerce" },
                    { "items_count", Checkout.Items.Count }
                }
            };

            var paymentResponse = await PaymentService.InitializePaymentAsync(paymentRequest);

            if (paymentResponse.Success)
            {
                // Payment successful, create order
                return await CheckoutService.ProcessPaymentAndCreateOrderAsync(
                    Checkout,
                    paymentResponse.Reference
                );
            }

            return new OrderPlacementResult
            {
                Success = false,
                Message = paymentResponse.Message
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Processing card payment");
            throw;
        }
    }

    private async Task<OrderPlacementResult?> ProcessWalletPayment()
    {
        try
        {
            if (WalletBalance < Checkout!.Total)
            {
                return new OrderPlacementResult
                {
                    Success = false,
                    Message = "Insufficient wallet balance"
                };
            }

            // Process payment and create order
            return await CheckoutService.ProcessPaymentAndCreateOrderAsync(
                Checkout,
                $"WALLET-{Guid.NewGuid()}"
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Processing wallet payment");
            throw;
        }
    }

    private async Task ClearCartAfterOrder()
    {
        try
        {
            await CartService.ClearCartAsync(CurrentUserId!);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "Cart cleared after successful order",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Clearing cart after order");
            // Don't fail the order if cart clearing fails
        }
    }

    // ==================== NAVIGATION ====================

    private void NavigateToShop()
    {
        Navigation.NavigateTo("shop");
    }

    private void NavigateToProduct()
    {
        if (!string.IsNullOrEmpty(Slug))
        {
            Navigation.NavigateTo($"product/{Slug}");
        }
        else
        {
            Navigation.NavigateTo("shop");
        }
    }

    private void NavigateToOrders()
    {
        Navigation.NavigateTo("user/orders");
    }

    private void CloseErrorModal()
    {
        ShowErrorModal = false;
        ErrorMessage = "";
    }

    // ==================== UTILITIES ====================

    private string FormatCurrency(decimal amount)
    {
        return $"₦{amount:N0}";
    }
}
