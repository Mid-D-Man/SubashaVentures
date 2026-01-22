// Pages/User/Payment.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Services.Payment;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Domain.Payment;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class Payment
{
    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private IWalletService WalletService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<Payment> Logger { get; set; } = default!;

    // State management
    private List<SavedCardViewModel> PaymentMethods = new();
    private List<WalletTransactionViewModel> Transactions = new();
    private Dictionary<string, string> ValidationErrors = new();
    
    // Modal references
    private DynamicModal? AddPaymentModal;
    private DynamicModal? TopUpModal;
    private ConfirmationPopup? DeleteConfirmPopup;
    
    // UI state
    private bool IsLoading = true;
    private bool IsAddPaymentModalOpen = false;
    private bool IsTopUpModalOpen = false;
    private bool IsSaving = false;
    private bool IsProcessing = false;
    
    // Data
    private string? PaymentToDelete;
    private string TopUpAmount = "";
    private string WalletBalance = "₦0";
    private string UserId = string.Empty;
    private string UserEmail = string.Empty;
    private decimal CurrentBalance = 0;
    private bool SetAsDefault = false; // For card saving checkbox

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Check authentication
            if (!await PermissionService.EnsureAuthenticatedAsync())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User not authenticated, redirecting to sign in",
                    LogLevel.Warning
                );
                NavigationManager.NavigateTo("signin", true);
                return;
            }

            UserId = await PermissionService.GetCurrentUserIdAsync() ?? string.Empty;
            UserEmail = await PermissionService.GetCurrentUserEmailAsync() ?? string.Empty;
            
            if (string.IsNullOrEmpty(UserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User ID not found, redirecting to sign in",
                    LogLevel.Warning
                );
                NavigationManager.NavigateTo("signin", true);
                return;
            }

            await LoadPaymentData();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Payment page initialization");
            Logger.LogError(ex, "Failed to initialize payment page for user: {UserId}", UserId);
        }
    }

    private async Task LoadPaymentData()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading payment data for user: {UserId}",
                LogLevel.Info
            );

            // Ensure wallet exists (will create if needed via edge function)
            var wallet = await WalletService.EnsureWalletExistsAsync(UserId);
            
            if (wallet != null)
            {
                CurrentBalance = wallet.Balance;
                WalletBalance = wallet.FormattedBalance;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Wallet loaded: {WalletBalance}",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "⚠️ Failed to ensure wallet exists",
                    LogLevel.Warning
                );
                
                // Set defaults
                CurrentBalance = 0;
                WalletBalance = "₦0";
            }

            // Load saved cards
            PaymentMethods = await WalletService.GetSavedCardsAsync(UserId);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {PaymentMethods.Count} saved payment methods",
                LogLevel.Info
            );

            // Load recent transactions
            Transactions = await WalletService.GetTransactionHistoryAsync(UserId, 0, 10);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {Transactions.Count} recent transactions",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading payment data");
            Logger.LogError(ex, "Failed to load payment data for user: {UserId}", UserId);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    // ==================== PAYMENT METHOD MANAGEMENT ====================

    private void OpenAddPaymentModal()
    {
        SetAsDefault = false;
        ValidationErrors.Clear();
        IsAddPaymentModalOpen = true;
        StateHasChanged();
    }

    private void CloseAddPaymentModal()
    {
        IsAddPaymentModalOpen = false;
        SetAsDefault = false;
        ValidationErrors.Clear();
        StateHasChanged();
    }

    private async Task SavePaymentMethod()
{
    IsSaving = true;
    StateHasChanged();

    try
    {
        await MID_HelperFunctions.DebugMessageAsync(
            "Opening Paystack to collect card details securely",
            LogLevel.Info
        );

        // Step 1: Open Paystack modal for user to enter card
        // This will charge ₦50 and return a transaction reference
        var tokenResult = await JSRuntime.InvokeAsync<PaystackTokenResult>(
            "paymentHandler.tokenizeCardForSaving",
            UserEmail,
            PaymentService.GetConfiguration().Paystack.PublicKey
        );

        if (!tokenResult.Success)
        {
            if (tokenResult.Cancelled)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Card saving cancelled by user",
                    LogLevel.Info
                );
                CloseAddPaymentModal();
                return;
            }

            ValidationErrors["General"] = tokenResult.Message ?? "Card tokenization failed";
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"✓ Card charged successfully: {tokenResult.Reference}",
            LogLevel.Info
        );

        // Step 2: Get authorization code from backend by verifying the transaction
        await MID_HelperFunctions.DebugMessageAsync(
            "Retrieving authorization code from transaction...",
            LogLevel.Info
        );

        var authCode = await GetAuthorizationCodeFromTransactionAsync(tokenResult.Reference);

        if (string.IsNullOrEmpty(authCode))
        {
            ValidationErrors["General"] = "Failed to retrieve card authorization. Please try again.";
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"✓ Authorization code retrieved: {authCode}",
            LogLevel.Info
        );

        // Step 3: Save payment method with authorization code
        var savedCard = await WalletService.SavePaymentMethodAsync(
            UserId,
            "paystack",
            authCode,
            UserEmail,
            SetAsDefault
        );

        if (savedCard != null)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Payment method saved successfully",
                LogLevel.Info
            );

            PaymentMethods.Add(savedCard);
            CloseAddPaymentModal();
            
            // Reload data to get updated info
            await LoadPaymentData();
        }
        else
        {
            ValidationErrors["General"] = "Failed to save payment method. Please try again.";
        }
    }
    catch (JSException jsEx)
    {
        await MID_HelperFunctions.LogExceptionAsync(jsEx, "JavaScript error during card tokenization");
        Logger.LogError(jsEx, "JavaScript error during card tokenization");
        
        if (jsEx.Message.Contains("cancelled") || jsEx.Message.Contains("canceled"))
        {
            CloseAddPaymentModal();
        }
        else
        {
            ValidationErrors["General"] = "An error occurred while processing your card. Please try again.";
        }
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Saving payment method");
        Logger.LogError(ex, "Failed to save payment method for user: {UserId}", UserId);
        
        ValidationErrors["General"] = ex.Message.Contains("reusable") 
            ? ex.Message 
            : "An error occurred while saving your card. Please try again.";
    }
    finally
    {
        IsSaving = false;
        StateHasChanged();
    }
}

/// <summary>
/// Get authorization code from Paystack transaction via edge function
/// </summary>
private async Task<string?> GetAuthorizationCodeFromTransactionAsync(string reference)
{
    try
    {
        var session = await JSRuntime.InvokeAsync<object>("sessionStorage.getItem", "supabase.auth.token");
        if (session == null)
        {
            throw new Exception("No active session");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, 
            "https://wbwmovtewytjibxutssk.supabase.co/functions/v1/get-card-authorization");
        
        // Add auth headers
        var authToken = session.ToString();
        request.Headers.Add("Authorization", $"Bearer {authToken}");
        request.Headers.Add("apikey", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indid21vdnRld3l0amlieHV0c3NrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjA0MjE4MzEsImV4cCI6MjA3NTk5NzgzMX0.dEYU3uKAeBJLnkyApUhh48nNqP6baGky8yhNNrM1NNg");
        
        var payload = new
        {
            reference,
            email = UserEmail
        };

        request.Content = System.Net.Http.Json.JsonContent.Create(payload);

        var httpClient = new HttpClient();
        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Failed to get authorization code: {content}",
                LogLevel.Error
            );
            return null;
        }

        var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);

        if (result != null && result.TryGetValue("authorizationCode", out var authCode))
        {
            return authCode.ToString();
        }

        return null;
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Getting authorization code from transaction");
        return null;
    }
}

    private async Task SetDefaultPayment(string paymentId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Setting default payment method: {paymentId}",
                LogLevel.Info
            );

            var success = await WalletService.SetDefaultPaymentMethodAsync(UserId, paymentId);

            if (success)
            {
                // Update local state
                foreach (var method in PaymentMethods)
                {
                    method.IsDefault = method.Id == paymentId;
                }

                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Default payment method updated",
                    LogLevel.Info
                );

                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Setting default payment");
            Logger.LogError(ex, "Failed to set default payment method: {PaymentId}", paymentId);
        }
    }

    private void ConfirmDeletePayment(string paymentId)
    {
        PaymentToDelete = paymentId;
        DeleteConfirmPopup?.Open();
    }

    private async Task ConfirmDeletePaymentMethod()
    {
        if (string.IsNullOrEmpty(PaymentToDelete))
            return;

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Deleting payment method: {PaymentToDelete}",
                LogLevel.Info
            );

            var success = await WalletService.DeletePaymentMethodAsync(UserId, PaymentToDelete);

            if (success)
            {
                PaymentMethods.RemoveAll(p => p.Id == PaymentToDelete);
                PaymentToDelete = null;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Payment method deleted successfully",
                    LogLevel.Info
                );

                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting payment method");
            Logger.LogError(ex, "Failed to delete payment method: {PaymentId}", PaymentToDelete);
        }
    }

    // ==================== WALLET TOP-UP ====================

    private void ShowTopUpModal()
    {
        TopUpAmount = "";
        IsTopUpModalOpen = true;
        StateHasChanged();
    }

    private void CloseTopUpModal()
    {
        IsTopUpModalOpen = false;
        TopUpAmount = "";
        StateHasChanged();
    }

    private async Task ProcessTopUp()
{
    if (string.IsNullOrWhiteSpace(TopUpAmount) || 
        !decimal.TryParse(TopUpAmount, out decimal amount) || 
        amount < 1000)
    {
        await MID_HelperFunctions.DebugMessageAsync(
            "Invalid top-up amount (minimum ₦1,000)",
            LogLevel.Warning
        );
        return;
    }

    IsProcessing = true;
    StateHasChanged();

    try
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"Initiating wallet top-up: ₦{amount:N0}",
            LogLevel.Info
        );

        if (string.IsNullOrEmpty(UserEmail))
        {
            throw new Exception("User email not found");
        }

        // Initialize payment with Paystack/Flutterwave
        var paymentRequest = new PaymentRequest
        {
            Email = UserEmail,
            CustomerName = UserEmail,
            Amount = amount,
            Currency = "NGN",
            Reference = PaymentService.GenerateReference(),
            Provider = PaymentProvider.Paystack,
            Description = "Wallet Top-Up",
            UserId = UserId,
            Metadata = new Dictionary<string, object>
            {
                { "type", "wallet_topup" },
                { "user_id", UserId },
                { "amount", amount }
            }
        };

        var paymentResponse = await PaymentService.InitializePaymentAsync(paymentRequest);

        if (paymentResponse.Success)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Payment completed: {paymentResponse.Reference}",
                LogLevel.Info
            );

            CloseTopUpModal();
            
            // Manually verify and credit wallet
            await MID_HelperFunctions.DebugMessageAsync(
                "Verifying payment and crediting wallet...",
                LogLevel.Info
            );

            var verified = await WalletService.VerifyAndCreditWalletAsync(
                paymentResponse.Reference,
                "paystack"
            );

            if (verified)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Wallet credited successfully!",
                    LogLevel.Info
                );
                
                // Reload wallet data
                await LoadPaymentData();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "⚠️ Payment verification pending. Please check your wallet in a few moments.",
                    LogLevel.Warning
                );
            }
        }
        else
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Payment failed: {paymentResponse.Message}",
                LogLevel.Error
            );
        }
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Processing wallet top-up");
        Logger.LogError(ex, "Failed to process wallet top-up for user: {UserId}", UserId);
    }
    finally
    {
        IsProcessing = false;
        StateHasChanged();
    }
}

    // ==================== NAVIGATION ====================

    private void ShowTransactionHistory()
    {
        NavigationManager.NavigateTo("user/wallet/transactions");
    }

    private void ShowAllTransactions()
    {
        NavigationManager.NavigateTo("user/wallet/transactions");
    }

    // ==================== VIEW MODELS ====================

    /// <summary>
    /// Response from Paystack tokenization JavaScript call
    /// </summary>
    /// <summary>
    /// Response from Paystack tokenization JavaScript call
    /// NOW: Just returns the transaction reference, not the authorization code
    /// </summary>
    private class PaystackTokenResult
    {
        public bool Success { get; set; }
        public string? Reference { get; set; }
        public string? TransactionId { get; set; }
        public string? Message { get; set; }
        public bool Cancelled { get; set; }
    }
}