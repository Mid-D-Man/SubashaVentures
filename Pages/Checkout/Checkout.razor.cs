// Pages/Checkout/Checkout.razor.cs - COMPLETE WITH GEOLOCATION
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Checkout;
using SubashaVentures.Domain.Cart;
using SubashaVentures.Domain.User;
using SubashaVentures.Domain.Order;
using SubashaVentures.Domain.Payment;
using SubashaVentures.Services.Checkout;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Addresses;
using SubashaVentures.Services.Payment;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Geolocation;
using SubashaVentures.Services.Users;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.Miscellaneous;
using SubashaVentures.Utilities.HelperScripts;
using System.Diagnostics;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Checkout;

public partial class Checkout : ComponentBase
{
    [Parameter] public string Slug { get; set; } = string.Empty;
    
    private string? ProductId { get; set; }
    private int? Quantity { get; set; }
    private string? Size { get; set; }
    private string? Color { get; set; }

    [Inject] private ICheckoutService CheckoutService { get; set; } = default!;
    [Inject] private ICartService CartService { get; set; } = default!;
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IAddressService AddressService { get; set; } = default!;
    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private IWalletService WalletService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IGeolocationService GeolocationService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<Checkout> Logger { get; set; } = default!;

    // State
    private CheckoutViewModel? CheckoutModel;
    private List<AddressViewModel> UserAddresses = new();
    private List<ShippingMethodViewModel> ShippingMethods = new();
    private ShippingMethodViewModel? SelectedShippingMethodObj;
    
    private bool IsLoading = true;
    private bool IsLoadingShipping = false;
    private bool IsProcessing = false;
    private bool ShowAddressForm = false;
    private bool ShowSuccessModal = false;
    private bool ShowErrorModal = false;
    private bool IsAutoFilling = false;
    
    private int CurrentStep = 1;
    private string? CurrentUserId;
    private string? SelectedAddressId;
    private string SelectedShippingMethod = "";
    private string SelectedPaymentMethod = "Card";
    private bool SaveNewAddress = false;
    private decimal WalletBalance = 0;
    private string OrderNumber = "";
    private string ErrorMessage = "";
    
    private AddressViewModel NewAddress = new();
    
    private DynamicModal? SuccessModal;
    private InfoPopup? ErrorPopup;
    
    private bool HasValidAddress => 
        !string.IsNullOrEmpty(SelectedAddressId) || 
        (!string.IsNullOrEmpty(NewAddress.FullName) &&
         !string.IsNullOrEmpty(NewAddress.PhoneNumber) &&
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
                return WalletBalance >= CheckoutModel?.Total;
            }
            return !string.IsNullOrEmpty(SelectedPaymentMethod);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IsLoading = true;

            var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
            var queryParams = ParseQueryString(uri.Query);
            
            ProductId = queryParams.ContainsKey("productId") ? queryParams["productId"] : null;
            Quantity = queryParams.ContainsKey("quantity") && int.TryParse(queryParams["quantity"], out var qty) ? qty : null;
            Size = queryParams.ContainsKey("size") ? queryParams["size"] : null;
            Color = queryParams.ContainsKey("color") ? queryParams["color"] : null;

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 CHECKOUT INIT - Slug: {Slug}, ProductId: {ProductId ?? "NULL"}, Quantity: {Quantity?.ToString() ?? "NULL"}, Size: {Size ?? "NULL"}, Color: {Color ?? "NULL"}",
                LogLevel.Info
            );

            if (!await PermissionService.EnsureAuthenticatedAsync($"checkout/{Slug}"))
            {
                Navigation.NavigateTo("signin", true);
                return;
            }

            CurrentUserId = await PermissionService.GetCurrentUserIdAsync();
            
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ User ID not found, redirecting to sign in",
                    LogLevel.Error
                );
                Navigation.NavigateTo("signin", true);
                return;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ User authenticated: {CurrentUserId}",
                LogLevel.Info
            );

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

    private Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        
        if (string.IsNullOrEmpty(query))
            return result;
        
        if (query.StartsWith("?"))
            query = query.Substring(1);
        
        var pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            if (parts.Length == 2)
            {
                var key = Uri.UnescapeDataString(parts[0]);
                var value = Uri.UnescapeDataString(parts[1]);
                result[key] = value;
            }
        }
        
        return result;
    }

    private async Task LoadCheckoutData()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔄 Loading checkout data for user: {CurrentUserId}",
                LogLevel.Info
            );

            if (!string.IsNullOrEmpty(ProductId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📦 PRODUCT CHECKOUT - ProductId: {ProductId}, Qty: {Quantity}, Size: {Size}, Color: {Color}",
                    LogLevel.Info
                );
                
                CheckoutModel = await CheckoutService.InitializeFromProductAsync(
                    ProductId,
                    Quantity ?? 1,
                    Size,
                    Color
                );

                if (CheckoutModel != null && CheckoutModel.Items.Any())
                {
                    var item = CheckoutModel.Items.First();
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ CHECKOUT LOADED - Name: {item.Name}, Image: {item.ImageUrl}, Price: ₦{item.Price:N0}, Size: {item.Size}, Color: {item.Color}",
                        LogLevel.Info
                    );
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Failed to initialize checkout from product",
                        LogLevel.Error
                    );
                }
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "🛒 CART CHECKOUT - Loading from user's cart",
                    LogLevel.Info
                );
                
                CheckoutModel = await CheckoutService.InitializeFromCartAsync(CurrentUserId!);

                if (CheckoutModel != null && CheckoutModel.Items.Any())
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ CART CHECKOUT LOADED - {CheckoutModel.Items.Count} items, Total: ₦{CheckoutModel.Total:N0}",
                        LogLevel.Info
                    );

                    foreach (var item in CheckoutModel.Items)
                    {
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"  📦 Item: {item.Name}, Image: {item.ImageUrl}, Qty: {item.Quantity}, Price: ₦{item.Price:N0}",
                            LogLevel.Info
                        );
                    }
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Failed to initialize checkout from cart or cart is empty",
                        LogLevel.Warning
                    );
                }
            }

            if (CheckoutModel == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ CheckoutModel is null - initialization failed",
                    LogLevel.Error
                );
                return;
            }

            await Task.WhenAll(
                LoadUserAddresses(),
                LoadWalletBalance()
            );

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Checkout loaded successfully: {CheckoutModel.Items.Count} items, Total: ₦{CheckoutModel.Total:N0}",
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
            UserAddresses = await AddressService.GetUserAddressesAsync(CurrentUserId!);
            
            var defaultAddress = UserAddresses.FirstOrDefault(a => a.IsDefault);
            if (defaultAddress != null)
            {
                SelectedAddressId = defaultAddress.Id;
                UpdateCheckoutAddress(defaultAddress);
            }
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded {UserAddresses.Count} addresses",
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
                $"💰 Wallet balance: ₦{WalletBalance:N0}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading wallet balance");
            WalletBalance = 0;
        }
    }

    // ==================== AUTO-FILL ADDRESS ====================

    private async Task AutoFillAddress()
    {
        IsAutoFilling = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🌍 Auto-filling address from location",
                LogLevel.Info
            );

            // Get location from IP
            var addressComponents = await GeolocationService.GetLocationFromIPAsync();

            if (addressComponents == null)
            {
                await JSRuntime.InvokeVoidAsync("alert",
                    "Unable to detect your location automatically. Please enter your address manually.");
                return;
            }

            // Fill in the detected information
            NewAddress.AddressLine1 = addressComponents.AddressLine1;
            NewAddress.City = addressComponents.City;
            NewAddress.State = addressComponents.State;
            NewAddress.PostalCode = addressComponents.PostalCode;
            NewAddress.Country = addressComponents.Country;

            // Get user info for name, phone, and email
            if (!string.IsNullOrEmpty(CurrentUserId))
            {
                var user = await UserService.GetUserByIdAsync(CurrentUserId);
                if (user != null)
                {
                    // Auto-fill name if available
                    if (string.IsNullOrEmpty(NewAddress.FullName))
                    {
                        NewAddress.FullName = $"{user.FirstName} {user.LastName}".Trim();
                    }

                    // Auto-fill phone if available
                    if (string.IsNullOrEmpty(NewAddress.PhoneNumber) && !string.IsNullOrEmpty(user.PhoneNumber))
                    {
                        NewAddress.PhoneNumber = user.PhoneNumber;
                    }

                    // Auto-fill email if available
                    if (string.IsNullOrEmpty(NewAddress.Email) && !string.IsNullOrEmpty(user.Email))
                    {
                        NewAddress.Email = user.Email;
                    }
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Address auto-filled: {NewAddress.City}, {NewAddress.State}",
                LogLevel.Info
            );

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Auto-filling address");
            await JSRuntime.InvokeVoidAsync("alert",
                "An error occurred while detecting your location. Please enter your address manually.");
        }
        finally
        {
            IsAutoFilling = false;
            StateHasChanged();
        }
    }

    // ==================== STEP NAVIGATION ====================

    private async Task GoToDeliveryStep()
    {
        if (!HasValidAddress)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "❌ Cannot proceed: Invalid address",
                LogLevel.Warning
            );
            return;
        }

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
            var newAddress = await SaveNewAddressIfNeeded();
            if (newAddress != null)
            {
                UpdateCheckoutAddress(newAddress);
            }
        }

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
        if (CheckoutModel == null) return;

        CheckoutModel.ShippingAddress = address;
        
        MID_HelperFunctions.DebugMessageAsync(
            $"📍 Shipping address selected: {address.City}, {address.State}",
            LogLevel.Info
        );
    }

    private void ShowNewAddressForm()
    {
        ShowAddressForm = true;
        NewAddress = new AddressViewModel
        {
            Country = "Nigeria",
            Type = AddressType.Shipping
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
            var success = await AddressService.AddAddressAsync(CurrentUserId!, NewAddress);
            
            if (success)
            {
                await LoadUserAddresses();
                
                var savedAddress = UserAddresses.FirstOrDefault(a => 
                    a.FullName == NewAddress.FullName && 
                    a.PhoneNumber == NewAddress.PhoneNumber);
                
                if (savedAddress != null)
                {
                    SelectedAddressId = savedAddress.Id;
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        "✅ New address saved successfully",
                        LogLevel.Info
                    );
                    
                    return savedAddress;
                }
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
        if (CheckoutModel == null) return;

        IsLoadingShipping = true;
        StateHasChanged();

        try
        {
            var checkoutItems = CheckoutModel.Items.Select(i => new CheckoutItemViewModel
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

            await MID_HelperFunctions.DebugMessageAsync(
                $"📦 Loading shipping methods for {checkoutItems.Count} items",
                LogLevel.Info
            );

            ShippingMethods = await CheckoutService.GetShippingMethodsAsync(
                CurrentUserId!,
                checkoutItems
            );

            if (ShippingMethods.Any())
            {
                SelectShippingMethod(ShippingMethods.First());
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded {ShippingMethods.Count} shipping methods",
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
        
        if (CheckoutModel != null)
        {
            CheckoutModel.ShippingMethod = method.Name;
            CheckoutModel.ShippingCost = method.Cost;
            CheckoutModel.ShippingRateId = method.Id;
        }

        StateHasChanged();
        
        MID_HelperFunctions.DebugMessageAsync(
            $"🚚 Shipping method selected: {method.Name} (₦{method.Cost:N0})",
            LogLevel.Info
        );
    }

    // ==================== PAYMENT ====================

    private void SelectPaymentMethod(string method)
    {
        SelectedPaymentMethod = method;
        
        if (CheckoutModel != null)
        {
            CheckoutModel.PaymentMethod = method switch
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
        if (CheckoutModel == null || IsProcessing) return;

        IsProcessing = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"📦 PLACING ORDER - Items: {CheckoutModel.Items.Count}, Total: ₦{CheckoutModel.Total:N0}",
                LogLevel.Info
            );

            foreach (var item in CheckoutModel.Items)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"  📦 Ordering: {item.Name}, Image: {item.ImageUrl}, Qty: {item.Quantity}, Price: ₦{item.Price:N0}, Size: {item.Size}, Color: {item.Color}",
                    LogLevel.Info
                );
            }

            var validation = await CheckoutService.ValidateCheckoutAsync(CheckoutModel);
            if (!validation.IsValid)
            {
                ErrorMessage = string.Join("\n", validation.Errors);
                ShowErrorModal = true;
                return;
            }

            OrderPlacementResult? result = null;

            if (SelectedPaymentMethod == "Card")
            {
                result = await ProcessCardPayment();
            }
            else if (SelectedPaymentMethod == "Wallet")
            {
                result = await ProcessWalletPayment();
            }
            else if (SelectedPaymentMethod == "PayOnDelivery")
            {
                result = await CheckoutService.PlaceOrderAsync(CheckoutModel, CurrentUserId!);
            }

            if (result != null && result.Success)
            {
                OrderNumber = result.OrderNumber ?? "";
                
                await ClearCartAfterOrder();
                
                ShowSuccessModal = true;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ ORDER PLACED SUCCESSFULLY: {OrderNumber}",
                    LogLevel.Info
                );
            }
            else
            {
                ErrorMessage = result?.Message ?? "Failed to place order";
                ShowErrorModal = true;

                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ ORDER FAILED: {ErrorMessage}",
                    LogLevel.Error
                );
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
            var user = await UserService.GetUserByIdAsync(CurrentUserId!);
            var paymentRequest = new PaymentRequest
            {
                Email = user.Email,
                CustomerName = CheckoutModel!.ShippingAddress!.FullName,
                PhoneNumber = CheckoutModel.ShippingAddress.PhoneNumber,
                Amount = CheckoutModel.Total,
                Currency = "NGN",
                Reference = PaymentService.GenerateReference(),
                Provider = PaymentProvider.Paystack,
                UserId = CurrentUserId,
                Description = "Order Payment",
                Metadata = new Dictionary<string, object>
                {
                    { "order_type", "ecommerce" },
                    { "items_count", CheckoutModel.Items.Count }
                }
            };

            var paymentResponse = await PaymentService.InitializePaymentAsync(paymentRequest);

            if (paymentResponse.Success)
            {
                return await CheckoutService.ProcessPaymentAndCreateOrderAsync(
                    CurrentUserId!,
                    CheckoutModel,
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
            if (WalletBalance < CheckoutModel!.Total)
            {
                return new OrderPlacementResult
                {
                    Success = false,
                    Message = "Insufficient wallet balance"
                };
            }

            return await CheckoutService.ProcessPaymentAndCreateOrderAsync(
                CurrentUserId!,
                CheckoutModel,
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
            if (string.IsNullOrEmpty(ProductId))
            {
                await CartService.ClearCartAsync(CurrentUserId!);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    "🗑️ Cart cleared after successful cart-based order",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Buy Now order complete - cart not affected",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Clearing cart after order");
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
