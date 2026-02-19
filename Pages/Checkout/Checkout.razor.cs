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
using SubashaVentures.Services.Addresses;
using SubashaVentures.Services.Payment;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Geolocation;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Services.Time;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.Miscellaneous;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Utilities.HelperScripts;
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
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private IServerTimeService ServerTimeService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<Checkout> Logger { get; set; } = default!;

    // ==================== STATE ====================

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

    // ==================== SVG PROPERTIES ====================

    internal string CartSvg { get; private set; } = string.Empty;
    internal string StepCheckSvg { get; private set; } = string.Empty;
    internal string AddressSvg { get; private set; } = string.Empty;
    internal string PhoneSvg { get; private set; } = string.Empty;
    internal string MailSvg { get; private set; } = string.Empty;
    internal string AddSvg { get; private set; } = string.Empty;
    internal string CloseSvg { get; private set; } = string.Empty;
    internal string LocationSvg { get; private set; } = string.Empty;
    internal string BackArrowSvg { get; private set; } = string.Empty;
    internal string DeliverySvg { get; private set; } = string.Empty;
    internal string TimeSvg { get; private set; } = string.Empty;
    internal string PaymentSvg { get; private set; } = string.Empty;
    internal string CardSvg { get; private set; } = string.Empty;
    internal string WalletSvg { get; private set; } = string.Empty;
    internal string WarningSvg { get; private set; } = string.Empty;
    internal string CashSvg { get; private set; } = string.Empty;
    internal string ReviewSvg { get; private set; } = string.Empty;
    internal string LockSvg { get; private set; } = string.Empty;
    internal string SuccessSvg { get; private set; } = string.Empty;
    internal string StandardShippingSvg { get; private set; } = string.Empty;
    internal string ExpressShippingSvg { get; private set; } = string.Empty;

    // ==================== COMPUTED ====================

    internal bool HasValidAddress =>
        !string.IsNullOrEmpty(SelectedAddressId) ||
        (!string.IsNullOrEmpty(NewAddress.FullName) &&
         !string.IsNullOrEmpty(NewAddress.PhoneNumber) &&
         !string.IsNullOrEmpty(NewAddress.AddressLine1) &&
         !string.IsNullOrEmpty(NewAddress.City) &&
         !string.IsNullOrEmpty(NewAddress.State) &&
         !string.IsNullOrEmpty(NewAddress.PostalCode));

    internal bool IsPaymentMethodValid
    {
        get
        {
            if (SelectedPaymentMethod == "Wallet")
                return WalletBalance >= (CheckoutModel?.Total ?? 0);
            return !string.IsNullOrEmpty(SelectedPaymentMethod);
        }
    }

    // ==================== DATA ====================

    internal readonly List<string> NigerianStates = new()
    {
        "Abia", "Adamawa", "Akwa Ibom", "Anambra", "Bauchi", "Bayelsa", "Benue",
        "Borno", "Cross River", "Delta", "Ebonyi", "Edo", "Ekiti", "Enugu",
        "FCT - Abuja", "Gombe", "Imo", "Jigawa", "Kaduna", "Kano", "Katsina",
        "Kebbi", "Kogi", "Kwara", "Lagos", "Nasarawa", "Niger", "Ogun", "Ondo",
        "Osun", "Oyo", "Plateau", "Rivers", "Sokoto", "Taraba", "Yobe", "Zamfara"
    };

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

    // ==================== LIFECYCLE ====================

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IsLoading = true;

            await LoadSvgsAsync();

            if (!await PermissionService.EnsureAuthenticatedAsync($"checkout/{Slug}"))
            {
                Navigation.NavigateTo("signin", true);
                return;
            }

            if (!await PermissionService.CanCheckoutAsync())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User account is not eligible for checkout",
                    LogLevel.Warning
                );
                Navigation.NavigateTo("signin", true);
                return;
            }

            CurrentUserId = await PermissionService.GetCurrentUserIdAsync();

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Navigation.NavigateTo("signin", true);
                return;
            }

            var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
            var queryParams = ParseQueryString(uri.Query);

            ProductId = queryParams.ContainsKey("productId") ? queryParams["productId"] : null;
            Quantity = queryParams.ContainsKey("quantity") && int.TryParse(queryParams["quantity"], out var qty) ? qty : null;
            Size = queryParams.ContainsKey("size") ? queryParams["size"] : null;
            Color = queryParams.ContainsKey("color") ? queryParams["color"] : null;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Checkout init - Slug: {Slug}, ProductId: {ProductId ?? "NULL"}, Quantity: {Quantity?.ToString() ?? "NULL"}",
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

    // ==================== SVG LOADING ====================

    private async Task LoadSvgsAsync()
    {
        try
        {
            CartSvg = await VisualElements.GetSvgAsync(SvgType.Cart, 64, 64);

            StepCheckSvg = VisualElements.GenerateSvg(
                "<path d='M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z' fill='currentColor'/>",
                16, 16, "0 0 24 24"
            );

            AddressSvg = await VisualElements.GetSvgAsync(SvgType.Address, 28, 28);

            PhoneSvg = VisualElements.GenerateSvg(
                "<path d='M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z' fill='currentColor'/>",
                14, 14, "0 0 24 24"
            );

            MailSvg = await VisualElements.GetSvgAsync(SvgType.Mail, 14, 14);

            AddSvg = VisualElements.GenerateSvg(
                "<path d='M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z' fill='currentColor'/>",
                18, 18, "0 0 24 24"
            );

            CloseSvg = await VisualElements.GetSvgAsync(SvgType.Close, 18, 18);

            LocationSvg = VisualElements.GenerateSvg(
                "<path d='M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z' fill='currentColor'/>",
                22, 22, "0 0 24 24"
            );

            BackArrowSvg = VisualElements.GenerateSvg(
                "<path d='M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z' fill='currentColor'/>",
                18, 18, "0 0 24 24"
            );

            DeliverySvg = await VisualElements.GetSvgAsync(SvgType.TrackOrders, 28, 28);

            TimeSvg = VisualElements.GenerateSvg(
                "<path d='M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67V7z' fill='currentColor'/>",
                14, 14, "0 0 24 24"
            );

            PaymentSvg = await VisualElements.GetSvgAsync(SvgType.Payment, 28, 28);

            CardSvg = VisualElements.GenerateSvg(
                "<path d='M20 4H4c-1.11 0-2 .89-2 2v12c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z' fill='currentColor'/>",
                24, 24, "0 0 24 24"
            );

            WalletSvg = VisualElements.GenerateSvg(
                "<path d='M21 18v1c0 1.1-.9 2-2 2H5c-1.11 0-2-.9-2-2V5c0-1.1.89-2 2-2h14c1.1 0 2 .9 2 2v1h-9c-1.11 0-2 .9-2 2v8c0 1.1.89 2 2 2h9zm-9-2h10V8H12v8zm4-2.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z' fill='currentColor'/>",
                24, 24, "0 0 24 24"
            );

            WarningSvg = await VisualElements.GetSvgAsync(SvgType.Warning, 16, 16);

            CashSvg = VisualElements.GenerateSvg(
                "<path d='M11.8 10.9c-2.27-.59-3-1.2-3-2.15 0-1.09 1.01-1.85 2.7-1.85 1.78 0 2.44.85 2.5 2.1h2.21c-.07-1.72-1.12-3.3-3.21-3.81V3h-3v2.16c-1.94.42-3.5 1.68-3.5 3.61 0 2.31 1.91 3.46 4.7 4.13 2.5.6 3 1.48 3 2.41 0 .69-.49 1.79-2.7 1.79-2.06 0-2.87-.92-2.98-2.1h-2.2c.12 2.19 1.76 3.42 3.68 3.83V21h3v-2.15c1.95-.37 3.5-1.5 3.5-3.55 0-2.84-2.43-3.81-4.7-4.4z' fill='currentColor'/>",
                24, 24, "0 0 24 24"
            );

            ReviewSvg = await VisualElements.GetSvgAsync(SvgType.Records, 28, 28);

            LockSvg = VisualElements.GenerateSvg(
                "<path d='M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z' fill='currentColor'/>",
                18, 18, "0 0 24 24"
            );

            SuccessSvg = await VisualElements.GetSvgAsync(SvgType.CheckMark, 64, 64);

            StandardShippingSvg = VisualElements.GenerateSvg(
                "<path d='M20 8h-3V4H3c-1.1 0-2 .9-2 2v11h2c0 1.66 1.34 3 3 3s3-1.34 3-3h6c0 1.66 1.34 3 3 3s3-1.34 3-3h2v-5l-3-4zM6 18.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm13.5-9l1.96 2.5H17V9.5h2.5zm-1.5 9c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z' fill='currentColor'/>",
                32, 32, "0 0 24 24"
            );

            ExpressShippingSvg = VisualElements.GenerateSvg(
                "<path d='M21 3L3 10.53v.98l6.84 2.65L12.48 21h.98L21 3z' fill='currentColor'/>",
                32, 32, "0 0 24 24"
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading checkout SVGs");
        }
    }

    internal string GetShippingMethodSvg(string methodName)
    {
        var lower = methodName.ToLowerInvariant();
        if (lower.Contains("express") || lower.Contains("fast") || lower.Contains("overnight"))
            return ExpressShippingSvg;
        return StandardShippingSvg;
    }

    // ==================== DATA LOADING ====================

    private async Task LoadCheckoutData()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading checkout data for user: {CurrentUserId}",
                LogLevel.Info
            );

            if (!string.IsNullOrEmpty(ProductId))
            {
                CheckoutModel = await CheckoutService.InitializeFromProductAsync(
                    ProductId,
                    Quantity ?? 1,
                    Size,
                    Color
                );
            }
            else
            {
                CheckoutModel = await CheckoutService.InitializeFromCartAsync(CurrentUserId!);
            }

            if (CheckoutModel == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "CheckoutModel is null - initialization failed",
                    LogLevel.Error
                );
                return;
            }

            await Task.WhenAll(
                LoadUserAddresses(),
                LoadWalletBalance()
            );

            await MID_HelperFunctions.DebugMessageAsync(
                $"Checkout loaded: {CheckoutModel.Items.Count} items",
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
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading wallet balance");
            WalletBalance = 0;
        }
    }

    // ==================== AUTO-FILL ADDRESS ====================

    internal async Task AutoFillCheckoutAddress()
    {
        IsAutoFilling = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Auto-filling checkout address from IP and user profile",
                LogLevel.Info
            );

            var locationData = await GeolocationService.GetLocationFromIPAsync();

            if (locationData == null)
            {
                await JSRuntime.InvokeVoidAsync("alert",
                    "Unable to detect your location. Please fill in your address manually.");
                return;
            }

            NewAddress.City = locationData.City;
            NewAddress.State = locationData.State;
            NewAddress.Country = locationData.Country;

            if (!string.IsNullOrEmpty(locationData.City) && string.IsNullOrEmpty(NewAddress.AddressLine1))
                NewAddress.AddressLine1 = $"{locationData.City} Area";

            if (string.IsNullOrEmpty(NewAddress.PostalCode))
            {
                if (!string.IsNullOrEmpty(locationData.PostalCode))
                    NewAddress.PostalCode = locationData.PostalCode;
                else if (StatePostalCodes.TryGetValue(NewAddress.State, out var postalCode))
                    NewAddress.PostalCode = postalCode;
            }

            if (!string.IsNullOrEmpty(CurrentUserId))
            {
                try
                {
                    var user = await UserService.GetUserByIdAsync(CurrentUserId);

                    if (user != null)
                    {
                        if (string.IsNullOrEmpty(NewAddress.Email) && !string.IsNullOrEmpty(user.Email))
                            NewAddress.Email = user.Email;

                        var fullName = $"{user.FirstName} {user.LastName}".Trim();
                        if (string.IsNullOrEmpty(NewAddress.FullName) && !string.IsNullOrEmpty(fullName))
                            NewAddress.FullName = fullName;

                        if (string.IsNullOrEmpty(NewAddress.PhoneNumber) && !string.IsNullOrEmpty(user.PhoneNumber))
                            NewAddress.PhoneNumber = user.PhoneNumber;
                    }
                }
                catch (Exception userEx)
                {
                    await MID_HelperFunctions.LogExceptionAsync(userEx, "Getting user profile for auto-fill");
                }
            }

            var missingFields = new List<string>();
            if (string.IsNullOrEmpty(NewAddress.FullName)) missingFields.Add("name");
            if (string.IsNullOrEmpty(NewAddress.PhoneNumber)) missingFields.Add("phone number");

            var message = missingFields.Any()
                ? $"Location detected: {locationData.City}, {locationData.State}. Please complete your {string.Join(" and ", missingFields)}."
                : $"Address filled from your location: {locationData.City}, {locationData.State}. Please review before continuing.";

            await JSRuntime.InvokeVoidAsync("alert", message);

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Auto-filling checkout address");
            Logger.LogError(ex, "Error during checkout address auto-fill");
            await JSRuntime.InvokeVoidAsync("alert",
                "Failed to detect location. Please enter your address manually.");
        }
        finally
        {
            IsAutoFilling = false;
            StateHasChanged();
        }
    }

    // ==================== STEP NAVIGATION ====================

    internal async Task GoToDeliveryStep()
    {
        if (!HasValidAddress)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Cannot proceed: invalid address",
                LogLevel.Warning
            );
            return;
        }

        if (!string.IsNullOrEmpty(SelectedAddressId))
        {
            var selectedAddress = UserAddresses.FirstOrDefault(a => a.Id == SelectedAddressId);
            if (selectedAddress != null)
                UpdateCheckoutAddress(selectedAddress);
        }
        else
        {
            var newAddress = await SaveNewAddressIfNeeded();
            if (newAddress != null)
                UpdateCheckoutAddress(newAddress);
        }

        CurrentStep = 2;
        await LoadShippingMethods();
    }

    internal void GoToShippingStep() => CurrentStep = 1;

    internal void GoToPaymentStep()
    {
        if (!string.IsNullOrEmpty(SelectedShippingMethod))
            CurrentStep = 3;
    }

    internal void GoToReviewStep()
    {
        if (IsPaymentMethodValid)
            CurrentStep = 4;
    }

    // ==================== ADDRESS MANAGEMENT ====================

    internal void SelectAddress(AddressViewModel address)
    {
        SelectedAddressId = address.Id;
        UpdateCheckoutAddress(address);
        StateHasChanged();
    }

    private void UpdateCheckoutAddress(AddressViewModel address)
    {
        if (CheckoutModel == null) return;
        CheckoutModel.ShippingAddress = address;
    }

    internal void ShowNewAddressForm()
    {
        ShowAddressForm = true;
        NewAddress = new AddressViewModel
        {
            Country = "Nigeria",
            Type = AddressType.Shipping
        };
    }

    internal void HideNewAddressForm()
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

    // ==================== SHIPPING ====================

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

            ShippingMethods = await CheckoutService.GetShippingMethodsAsync(
                CurrentUserId!,
                checkoutItems
            );

            if (ShippingMethods.Any())
                SelectShippingMethod(ShippingMethods.First());
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

    internal void SelectShippingMethod(ShippingMethodViewModel method)
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
    }

    // ==================== PAYMENT ====================

    internal void SelectPaymentMethod(string method)
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

    internal string GetPaymentMethodDisplay(string method)
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

    internal async Task PlaceOrder()
    {
        if (CheckoutModel == null || IsProcessing) return;

        IsProcessing = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Placing order - Items: {CheckoutModel.Items.Count}, Total: {FormatCurrency(CheckoutModel.Total)}",
                LogLevel.Info
            );

            var isAuthenticated = await PermissionService.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                Navigation.NavigateTo("signin", true);
                return;
            }

            var isAccountActive = await PermissionService.IsAccountActiveAsync();
            if (!isAccountActive)
            {
                ErrorMessage = "Your account is not active. Please contact support.";
                ShowErrorModal = true;
                return;
            }

            var serverTime = await ServerTimeService.GetCurrentServerTimeAsync();
            await MID_HelperFunctions.DebugMessageAsync(
                $"Server time at order placement: {serverTime:yyyy-MM-dd HH:mm:ss UTC}",
                LogLevel.Info
            );

            await ServerTimeService.ForceSyncAsync();

            var validation = await CheckoutService.ValidateCheckoutAsync(CheckoutModel);
            if (!validation.IsValid)
            {
                ErrorMessage = string.Join("\n", validation.Errors);
                ShowErrorModal = true;
                return;
            }

            OrderPlacementResult? result = null;

            if (SelectedPaymentMethod == "Card")
                result = await ProcessCardPayment();
            else if (SelectedPaymentMethod == "Wallet")
                result = await ProcessWalletPayment();
            else if (SelectedPaymentMethod == "PayOnDelivery")
                result = await CheckoutService.PlaceOrderAsync(CheckoutModel, CurrentUserId!);

            if (result != null && result.Success)
            {
                OrderNumber = result.OrderNumber ?? "";
                await ClearCartAfterOrder();
                ShowSuccessModal = true;

                await MID_HelperFunctions.DebugMessageAsync(
                    $"Order placed successfully: {OrderNumber}",
                    LogLevel.Info
                );
            }
            else
            {
                ErrorMessage = result?.Message ?? "Failed to place order";
                ShowErrorModal = true;

                await MID_HelperFunctions.DebugMessageAsync(
                    $"Order failed: {ErrorMessage}",
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
                Email = user?.Email ?? string.Empty,
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
                await CartService.ClearCartAsync(CurrentUserId!);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Clearing cart after order");
        }
    }

    // ==================== NAVIGATION ====================

    internal void NavigateToShop() => Navigation.NavigateTo("shop");

    internal void NavigateToProduct()
    {
        if (!string.IsNullOrEmpty(Slug))
            Navigation.NavigateTo($"product/{Slug}");
        else
            Navigation.NavigateTo("shop");
    }

    internal void NavigateToOrders() => Navigation.NavigateTo("user/orders");

    internal void CloseErrorModal()
    {
        ShowErrorModal = false;
        ErrorMessage = "";
    }

    // ==================== UTILITIES ====================

    internal string FormatCurrency(decimal amount) => $"₦{amount:N0}";

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
}
