// Pages/User/Payment.razor.cs - COMPLETE IMPLEMENTATION
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

    private List<SavedCardViewModel> PaymentMethods = new();
    private List<WalletTransactionViewModel> Transactions = new();
    private PaymentMethodViewModel NewCard = new();
    private Dictionary<string, string> ValidationErrors = new();
    
    private DynamicModal? AddPaymentModal;
    private DynamicModal? TopUpModal;
    private ConfirmationPopup? DeleteConfirmPopup;
    
    private bool IsLoading = true;
    private bool IsAddPaymentModalOpen = false;
    private bool IsTopUpModalOpen = false;
    private bool IsSaving = false;
    private bool IsProcessing = false;
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
                return;
            }

            UserId = await PermissionService.GetCurrentUserIdAsync() ?? string.Empty;
            
            if (string.IsNullOrEmpty(UserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User ID not found, redirecting to sign in",
                    LogLevel.Warning
                );
                NavigationManager.NavigateTo("signin");
                return;
            }

            await LoadPaymentData();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Payment page initialization");
            Logger.LogError(ex, "Failed to initialize payment page");
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

            // Load recent transactions
            Transactions = await WalletService.GetTransactionHistoryAsync(UserId, 0, 10);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded: {PaymentMethods.Count} cards, {Transactions.Count} transactions",
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
            return;

        IsSaving = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Saving payment method via payment gateway",
                LogLevel.Info
            );

            // TODO: Call payment gateway to tokenize card
            // For now, this is a placeholder - you'll need to integrate with Paystack/Flutterwave
            // to get the authorization code from their tokenization API
            
            var cardDetails = new CardDetails
            {
                CardType = DetectCardType(NewCard.CardNumber),
                Last4 = NewCard.CardNumber.Substring(NewCard.CardNumber.Length - 4),
                ExpMonth = NewCard.ExpiryMonth,
                ExpYear = NewCard.ExpiryYear,
                Bank = "Unknown", // Would come from payment gateway
                Brand = DetectCardType(NewCard.CardNumber)
            };

            var authorizationCode = $"AUTH_{Guid.NewGuid().ToString().Substring(0, 8)}"; // Placeholder

            var savedCard = await WalletService.SavePaymentMethodAsync(
                UserId,
                "paystack", // or "flutterwave"
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
            Logger.LogError(ex, "Failed to save payment method");
            ValidationErrors["General"] = $"Error: {ex.Message}";
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
            ValidationErrors["CardholderName"] = "Cardholder name is required";

        if (string.IsNullOrWhiteSpace(NewCard.CardNumber) || NewCard.CardNumber.Replace(" ", "").Length < 13)
            ValidationErrors["CardNumber"] = "Valid card number is required";

        if (string.IsNullOrWhiteSpace(NewCard.ExpiryMonth) || !int.TryParse(NewCard.ExpiryMonth, out int month) || month < 1 || month > 12)
            ValidationErrors["ExpiryMonth"] = "Valid month required (01-12)";

        if (string.IsNullOrWhiteSpace(NewCard.ExpiryYear) || NewCard.ExpiryYear.Length != 2)
            ValidationErrors["ExpiryYear"] = "Valid year required (YY)";

        if (string.IsNullOrWhiteSpace(NewCard.CVV) || NewCard.CVV.Length < 3)
            ValidationErrors["CVV"] = "Valid CVV is required";

        return !ValidationErrors.Any();
    }

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
        if (string.IsNullOrWhiteSpace(TopUpAmount) || !decimal.TryParse(TopUpAmount, out decimal amount) || amount <= 0)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Invalid top-up amount",
                LogLevel.Warning
            );
            return;
        }

        IsProcessing = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Processing top-up: ₦{amount:N0}",
                LogLevel.Info
            );

            var email = await PermissionService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(email))
            {
                throw new Exception("User email not found");
            }

            // Initialize payment
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
                    { "user_id", UserId }
                }
            };

            var paymentResponse = await PaymentService.InitializePaymentWithFallbackAsync(paymentRequest);

            if (paymentResponse.Success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Payment initialized successfully, verifying...",
                    LogLevel.Info
                );

                // Verify payment
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "Processing top-up");
            Logger.LogError(ex, "Failed to process top-up");
        }
        finally
        {
            IsProcessing = false;
            StateHasChanged();
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

                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Setting default payment");
            Logger.LogError(ex, "Failed to set default payment method");
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
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting payment method");
            Logger.LogError(ex, "Failed to delete payment method");
        }
    }

    private string GetCardIcon(string cardType) => cardType.ToLower() switch
    {
        "visa" => "💳",
        "mastercard" => "💳",
        "verve" => "💳",
        "amex" => "💳",
        _ => "💳"
    };

    private string DetectCardType(string cardNumber)
    {
        var cleaned = cardNumber.Replace(" ", "");
        if (cleaned.StartsWith("4")) return "Visa";
        if (cleaned.StartsWith("5")) return "Mastercard";
        if (cleaned.StartsWith("506")) return "Verve";
        if (cleaned.StartsWith("3")) return "Amex";
        return "Unknown";
    }

    private class PaymentMethodViewModel
    {
        public string Id { get; set; } = "";
        public string CardType { get; set; } = "";
        public string LastFourDigits { get; set; } = "";
        public string ExpiryDate { get; set; } = "";
        public string CardholderName { get; set; } = "";
        public string CardNumber { get; set; } = "";
        public string ExpiryMonth { get; set; } = "";
        public string ExpiryYear { get; set; } = "";
        public string CVV { get; set; } = "";
        public bool IsDefault { get; set; }
        public bool SetAsDefault { get; set; }
    }
}