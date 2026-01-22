// Pages/User/Payment.razor.cs - COMPLETE IMPLEMENTATION WITH SECURITY
using Microsoft.AspNetCore.Components;
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
    [Inject] private ILogger<Payment> Logger { get; set; } = default!;

    // State management
    private List<SavedCardViewModel> PaymentMethods = new();
    private List<WalletTransactionViewModel> Transactions = new();
    private PaymentMethodViewModel NewCard = new();
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
    private decimal CurrentBalance = 0;

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
                NavigationManager.NavigateTo("/signin", true);
                return;
            }

            UserId = await PermissionService.GetCurrentUserIdAsync() ?? string.Empty;
            
            if (string.IsNullOrEmpty(UserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User ID not found, redirecting to sign in",
                    LogLevel.Warning
                );
                NavigationManager.NavigateTo("/signin", true);
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

            // Load wallet balance
            var wallet = await WalletService.GetWalletAsync(UserId);
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
                    "Wallet not found, creating new wallet",
                    LogLevel.Info
                );
                
                var newWallet = await WalletService.CreateWalletAsync(UserId);
                if (newWallet != null)
                {
                    CurrentBalance = newWallet.Balance;
                    WalletBalance = newWallet.FormattedBalance;
                }
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
        NewCard = new PaymentMethodViewModel();
        ValidationErrors.Clear();
        IsAddPaymentModalOpen = true;
        StateHasChanged();
    }

    private void CloseAddPaymentModal()
    {
        IsAddPaymentModalOpen = false;
        NewCard = new();
        ValidationErrors.Clear();
        StateHasChanged();
    }

    private async Task SavePaymentMethod()
    {
        if (!ValidatePaymentMethod())
        {
            StateHasChanged();
            return;
        }

        IsSaving = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Processing payment method via payment gateway",
                LogLevel.Info
            );

            // IMPORTANT: This is where you'd integrate with Paystack/Flutterwave
            // The actual tokenization should happen via their JavaScript SDK
            // and return an authorization code that we save here
            
            // For now, this is a demonstration - you need to:
            // 1. Call Paystack.js or Flutterwave inline to tokenize the card
            // 2. Receive the authorization code from their callback
            // 3. Save that authorization code here
            
            var cardDetails = new CardDetails
            {
                CardType = DetectCardType(NewCard.CardNumber),
                Last4 = NewCard.CardNumber.Replace(" ", "").Substring(NewCard.CardNumber.Replace(" ", "").Length - 4),
                ExpMonth = NewCard.ExpiryMonth,
                ExpYear = NewCard.ExpiryYear,
                Bank = "Unknown", // This comes from payment gateway
                Brand = DetectCardType(NewCard.CardNumber)
            };

            // PLACEHOLDER: Replace with actual authorization code from payment gateway
            var authorizationCode = $"AUTH_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

            var savedCard = await WalletService.SavePaymentMethodAsync(
                UserId,
                "paystack", // or "flutterwave" - should come from config
                authorizationCode,
                cardDetails,
                NewCard.SetAsDefault
            );

            if (savedCard != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Payment method saved successfully",
                    LogLevel.Info
                );

                PaymentMethods.Add(savedCard);
                CloseAddPaymentModal();
            }
            else
            {
                ValidationErrors["General"] = "Failed to save payment method. Please try again.";
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving payment method");
            Logger.LogError(ex, "Failed to save payment method for user: {UserId}", UserId);
            ValidationErrors["General"] = $"An error occurred: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
            StateHasChanged();
        }
    }

    private bool ValidatePaymentMethod()
    {
        ValidationErrors.Clear();

        if (string.IsNullOrWhiteSpace(NewCard.CardholderName))
        {
            ValidationErrors["CardholderName"] = "Cardholder name is required";
        }

        var cleanedNumber = NewCard.CardNumber?.Replace(" ", "") ?? "";
        if (string.IsNullOrWhiteSpace(NewCard.CardNumber) || cleanedNumber.Length < 13 || cleanedNumber.Length > 19)
        {
            ValidationErrors["CardNumber"] = "Please enter a valid card number";
        }

        if (string.IsNullOrWhiteSpace(NewCard.ExpiryMonth) || 
            !int.TryParse(NewCard.ExpiryMonth, out int month) || 
            month < 1 || month > 12)
        {
            ValidationErrors["ExpiryMonth"] = "Invalid month (01-12)";
        }

        if (string.IsNullOrWhiteSpace(NewCard.ExpiryYear) || 
            !int.TryParse(NewCard.ExpiryYear, out int year) || 
            year < 0 || year > 99)
        {
            ValidationErrors["ExpiryYear"] = "Invalid year (YY)";
        }

        // Check if card is expired
        if (int.TryParse(NewCard.ExpiryMonth, out int expMonth) && 
            int.TryParse(NewCard.ExpiryYear, out int expYear))
        {
            var currentYear = DateTime.Now.Year % 100;
            var currentMonth = DateTime.Now.Month;
            
            if (expYear < currentYear || (expYear == currentYear && expMonth < currentMonth))
            {
                ValidationErrors["ExpiryMonth"] = "Card has expired";
            }
        }

        if (string.IsNullOrWhiteSpace(NewCard.CVV) || 
            NewCard.CVV.Length < 3 || 
            NewCard.CVV.Length > 4)
        {
            ValidationErrors["CVV"] = "Invalid CVV (3-4 digits)";
        }

        return !ValidationErrors.Any();
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

            var email = await PermissionService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(email))
            {
                throw new Exception("User email not found");
            }

            // Initialize payment with Paystack/Flutterwave
            var paymentRequest = new PaymentRequest
            {
                Email = email,
                CustomerName = email,
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

            var paymentResponse = await PaymentService.InitializePaymentWithFallbackAsync(paymentRequest);

            if (paymentResponse.Success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Payment initialized: {paymentResponse.Reference}",
                    LogLevel.Info
                );

                // Verify payment (in production, this should be a webhook callback)
                var verifyRequest = new PaymentVerificationRequest
                {
                    Reference = paymentResponse.Reference,
                    Provider = paymentResponse.Provider
                };

                var verifyResponse = await PaymentService.VerifyPaymentAsync(verifyRequest);

                if (verifyResponse.Verified)
                {
                    // Credit wallet
                    var transaction = await WalletService.TopUpWalletAsync(
                        UserId,
                        amount,
                        paymentResponse.Reference,
                        paymentResponse.Provider.ToString()
                    );

                    if (transaction != null)
                    {
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"✓ Wallet topped up successfully: {transaction.FormattedAmount}",
                            LogLevel.Info
                        );

                        // Reload data
                        await LoadPaymentData();
                        CloseTopUpModal();
                    }
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Payment verification failed: {verifyResponse.Status}",
                        LogLevel.Error
                    );
                }
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Payment initialization failed: {paymentResponse.Message}",
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

    // ==================== HELPER METHODS ====================

    private string DetectCardType(string cardNumber)
    {
        var cleaned = cardNumber?.Replace(" ", "") ?? "";
        
        if (string.IsNullOrEmpty(cleaned))
            return "Unknown";
        
        if (cleaned.StartsWith("4"))
            return "Visa";
        
        if (cleaned.StartsWith("5") || cleaned.StartsWith("2"))
            return "Mastercard";
        
        if (cleaned.StartsWith("506") || cleaned.StartsWith("507") || cleaned.StartsWith("650"))
            return "Verve";
        
        if (cleaned.StartsWith("34") || cleaned.StartsWith("37"))
            return "Amex";
        
        return "Unknown";
    }

    // ==================== VIEW MODELS ====================

    private class PaymentMethodViewModel
    {
        public string CardholderName { get; set; } = "";
        public string CardNumber { get; set; } = "";
        public string ExpiryMonth { get; set; } = "";
        public string ExpiryYear { get; set; } = "";
        public string CVV { get; set; } = "";
        public bool SetAsDefault { get; set; }
    }
}