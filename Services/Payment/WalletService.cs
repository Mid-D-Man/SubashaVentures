// Services/Payment/WalletService.cs - UPDATED WITH NULL CHECKS
using SubashaVentures.Domain.Payment;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Payment;

public class WalletService : IWalletService
{
    private readonly Client _supabaseClient;
    private readonly ISupabaseEdgeFunctionService _edgeFunctions;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        Client supabaseClient,
        ISupabaseEdgeFunctionService edgeFunctions,
        ILogger<WalletService> logger)
    {
        _supabaseClient = supabaseClient;
        _edgeFunctions = edgeFunctions;
        _logger = logger;
    }

    // ==================== WALLET OPERATIONS ====================

    public async Task<WalletViewModel?> GetWalletAsync(string userId)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ GetWalletAsync called with null/empty userId",
                    LogLevel.Error
                );
                return null;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting wallet for user: {userId}",
                LogLevel.Info
            );

            var wallet = await _supabaseClient
                .From<UserWalletModel>()
                .Where(w => w.UserId == userId)
                .Single();

            if (wallet != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Wallet found: Balance = ₦{wallet.Balance:N0}",
                    LogLevel.Info
                );
                return WalletViewModel.FromModel(wallet);
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "Wallet not found",
                LogLevel.Info
            );
            return null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting wallet");
            _logger.LogError(ex, "Failed to get wallet for user: {UserId}", userId);
            return null;
        }
    }

    public async Task<WalletViewModel?> CreateWalletAsync(string userId)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ CreateWalletAsync called with null/empty userId",
                    LogLevel.Error
                );
                return null;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating wallet via edge function for user: {userId}",
                LogLevel.Info
            );

            var result = await _edgeFunctions.CreateWalletAsync(userId);

            if (result.Success && result.Data != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Wallet created successfully via edge function",
                    LogLevel.Info
                );

                return new WalletViewModel
                {
                    UserId = result.Data.UserId,
                    Balance = result.Data.Balance,
                    Currency = result.Data.Currency,
                    IsLocked = result.Data.IsLocked,
                    CreatedAt = result.Data.CreatedAt
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Failed to create wallet: {result.Message}",
                LogLevel.Error
            );
            
            return null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating wallet via edge function");
            _logger.LogError(ex, "Failed to create wallet for user: {UserId}", userId);
            return null;
        }
    }

    public async Task<WalletViewModel?> EnsureWalletExistsAsync(string userId)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ EnsureWalletExistsAsync called with null/empty userId",
                    LogLevel.Error
                );
                return null;
            }

            var wallet = await GetWalletAsync(userId);
            
            if (wallet != null)
            {
                return wallet;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "Wallet not found, creating new wallet",
                LogLevel.Info
            );
            
            return await CreateWalletAsync(userId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Ensuring wallet exists");
            _logger.LogError(ex, "Failed to ensure wallet exists for user: {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> VerifyAndCreditWalletAsync(string reference, string provider)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(reference))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ VerifyAndCreditWalletAsync called with null/empty reference",
                    LogLevel.Error
                );
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Manually verifying payment: {reference}",
                LogLevel.Info
            );

            var result = await _edgeFunctions.VerifyAndCreditWalletAsync(reference, provider);

            if (result.Success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Payment verified and wallet credited",
                    LogLevel.Info
                );
                return true;
            }

            if (result.AlreadyProcessed)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "ℹ️ Payment already processed",
                    LogLevel.Info
                );
                return true;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Verification failed: {result.Message}",
                LogLevel.Error
            );

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Manual payment verification");
            _logger.LogError(ex, "Failed to verify and credit wallet");
            return false;
        }
    }

    public async Task<WalletTransactionViewModel?> DeductFromWalletAsync(
        string userId,
        decimal amount,
        string description,
        string? orderId = null)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            if (amount <= 0)
            {
                throw new ArgumentException("Deduction amount must be greater than zero", nameof(amount));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description cannot be null or empty", nameof(description));
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Deduction request: User={userId}, Amount=₦{amount:N0}",
                LogLevel.Info
            );

            var result = await _edgeFunctions.DeductFromWalletAsync(userId, amount, description, orderId);

            if (result.Success && result.Data != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Wallet deducted successfully",
                    LogLevel.Info
                );

                return await GetTransactionByReferenceAsync(result.Data.Reference);
            }

            throw new Exception(result.Message);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deduct from wallet");
            _logger.LogError(ex, "Failed to deduct from wallet for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> HasSufficientBalanceAsync(string userId, decimal amount)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var wallet = await GetWalletAsync(userId);
            return wallet != null && wallet.Balance >= amount && !wallet.IsLocked;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking balance");
            _logger.LogError(ex, "Failed to check balance for user: {UserId}", userId);
            return false;
        }
    }

    // ==================== TRANSACTION HISTORY ====================

    public async Task<List<WalletTransactionViewModel>> GetTransactionHistoryAsync(
        string userId,
        int skip = 0,
        int take = 20)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ GetTransactionHistoryAsync called with null/empty userId",
                    LogLevel.Error
                );
                return new List<WalletTransactionViewModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting transaction history for user: {userId}",
                LogLevel.Info
            );

            var transactions = await _supabaseClient
                .From<WalletTransactionModel>()
                .Where(t => t.UserId == userId)
                .Order(t => t.CreatedAt, Constants.Ordering.Descending)
                .Range(skip, skip + take - 1)
                .Get();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {transactions.Models.Count} transactions",
                LogLevel.Info
            );

            return transactions.Models
                .Select(WalletTransactionViewModel.FromModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting transaction history");
            _logger.LogError(ex, "Failed to get transaction history for user: {UserId}", userId);
            return new List<WalletTransactionViewModel>();
        }
    }

    public async Task<WalletTransactionViewModel?> GetTransactionByReferenceAsync(string reference)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            var transaction = await _supabaseClient
                .From<WalletTransactionModel>()
                .Where(t => t.Reference == reference)
                .Single();

            return transaction != null 
                ? WalletTransactionViewModel.FromModel(transaction) 
                : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting transaction by reference");
            _logger.LogError(ex, "Failed to get transaction by reference: {Reference}", reference);
            return null;
        }
    }

    // ==================== SAVED CARDS ====================

    public async Task<List<SavedCardViewModel>> GetSavedCardsAsync(string userId)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ GetSavedCardsAsync called with null/empty userId",
                    LogLevel.Error
                );
                return new List<SavedCardViewModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting saved cards for user: {userId}",
                LogLevel.Info
            );

            var cards = await _supabaseClient
                .From<UserPaymentMethodModel>()
                .Where(c => c.UserId == userId && c.IsDeleted == false)
                .Order(c => c.IsDefault, Constants.Ordering.Descending)
                .Order(c => c.CreatedAt, Constants.Ordering.Descending)
                .Get();

            var cardsList = cards?.Models ?? new List<UserPaymentMethodModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {cardsList.Count} saved cards",
                LogLevel.Info
            );

            return cardsList
                .Select(SavedCardViewModel.FromModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting saved cards");
            _logger.LogError(ex, "Failed to get saved cards for user: {UserId}", userId);
            return new List<SavedCardViewModel>();
        }
    }

    public async Task<SavedCardViewModel?> SavePaymentMethodAsync(
        string userId,
        string provider,
        string authorizationCode,
        string email,
        bool setAsDefault = false)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ArgumentException("Provider cannot be null or empty", nameof(provider));
            }

            if (string.IsNullOrWhiteSpace(authorizationCode))
            {
                throw new ArgumentException("Authorization code cannot be null or empty", nameof(authorizationCode));
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email cannot be null or empty", nameof(email));
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Verifying and saving payment method: User={userId}, Provider={provider}",
                LogLevel.Info
            );

            var verificationResult = await _edgeFunctions.VerifyCardTokenAsync(
                userId,
                provider,
                authorizationCode,
                email
            );

            if (!verificationResult.Success || verificationResult.Data == null)
            {
                throw new Exception(verificationResult.Message);
            }

            if (!verificationResult.Data.Verified)
            {
                throw new Exception("Card verification failed");
            }

            var cardDetails = verificationResult.Data.CardDetails;

            if (!cardDetails.Reusable)
            {
                throw new Exception("This card cannot be saved for future use");
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Card verified successfully, saving to database",
                LogLevel.Info
            );

            if (setAsDefault)
            {
                var existingCards = await _supabaseClient
                    .From<UserPaymentMethodModel>()
                    .Where(c => c.UserId == userId && !c.IsDeleted)
                    .Get();

                foreach (var card in existingCards.Models.Where(c => c.IsDefault))
                {
                    card.IsDefault = false;
                    card.UpdatedAt = DateTime.UtcNow;
                    card.UpdatedBy = userId;
                    await _supabaseClient.From<UserPaymentMethodModel>().Update(card);
                }
            }

            var paymentMethod = new UserPaymentMethodModel
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Provider = provider,
                AuthorizationCode = authorizationCode,
                CardType = cardDetails.CardType,
                CardLast4 = cardDetails.Last4,
                CardExpMonth = cardDetails.ExpMonth,
                CardExpYear = cardDetails.ExpYear,
                CardBank = cardDetails.Bank,
                CardBrand = cardDetails.Brand,
                IsDefault = setAsDefault,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            var created = await _supabaseClient
                .From<UserPaymentMethodModel>()
                .Insert(paymentMethod);

            if (created?.Model != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Payment method saved successfully",
                    LogLevel.Info
                );

                return SavedCardViewModel.FromModel(created.Model);
            }

            return null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving payment method");
            _logger.LogError(ex, "Failed to save payment method for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> SetDefaultPaymentMethodAsync(string userId, string paymentMethodId)
    {
        try
        {
            // ✅ VALIDATE INPUT - THIS IS THE KEY FIX!
            if (string.IsNullOrWhiteSpace(userId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ SetDefaultPaymentMethodAsync called with null/empty userId",
                    LogLevel.Error
                );
                _logger.LogError("Cannot set default payment method: userId is null or empty");
                return false;
            }

            if (string.IsNullOrWhiteSpace(paymentMethodId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ SetDefaultPaymentMethodAsync called with null/empty paymentMethodId",
                    LogLevel.Error
                );
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Setting default payment method: User={userId}, PaymentMethod={paymentMethodId}",
                LogLevel.Info
            );

            var allCards = await _supabaseClient
                .From<UserPaymentMethodModel>()
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .Get();

            if (allCards?.Models == null || !allCards.Models.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No payment methods found for user",
                    LogLevel.Warning
                );
                return false;
            }

            bool paymentMethodFound = false;
            foreach (var card in allCards.Models)
            {
                bool shouldBeDefault = card.Id == paymentMethodId;
                if (shouldBeDefault)
                {
                    paymentMethodFound = true;
                }

                card.IsDefault = shouldBeDefault;
                card.UpdatedAt = DateTime.UtcNow;
                card.UpdatedBy = userId;
                await _supabaseClient.From<UserPaymentMethodModel>().Update(card);
            }

            if (!paymentMethodFound)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Payment method {paymentMethodId} not found for user {userId}",
                    LogLevel.Warning
                );
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Default payment method updated",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Setting default payment method");
            _logger.LogError(ex, "Failed to set default payment method: User={UserId}, PaymentMethod={PaymentMethodId}", 
                userId, paymentMethodId);
            return false;
        }
    }

    public async Task<bool> DeletePaymentMethodAsync(string userId, string paymentMethodId)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ DeletePaymentMethodAsync called with null/empty userId",
                    LogLevel.Error
                );
                return false;
            }

            if (string.IsNullOrWhiteSpace(paymentMethodId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ DeletePaymentMethodAsync called with null/empty paymentMethodId",
                    LogLevel.Error
                );
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Deleting payment method: {paymentMethodId}",
                LogLevel.Info
            );

            var card = await _supabaseClient
                .From<UserPaymentMethodModel>()
                .Where(c => c.Id == paymentMethodId && c.UserId == userId)
                .Single();

            if (card == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Payment method not found",
                    LogLevel.Warning
                );
                return false;
            }

            card.IsDeleted = true;
            card.DeletedAt = DateTime.UtcNow;
            card.DeletedBy = userId;
            card.IsActive = false;

            await _supabaseClient
                .From<UserPaymentMethodModel>()
                .Update(card);

            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Payment method deleted",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting payment method");
            _logger.LogError(ex, "Failed to delete payment method");
            return false;
        }
    }

    public async Task<SavedCardViewModel?> GetDefaultPaymentMethodAsync(string userId)
    {
        try
        {
            // ✅ VALIDATE INPUT
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var card = await _supabaseClient
                .From<UserPaymentMethodModel>()
                .Where(c => c.UserId == userId && c.IsDefault && !c.IsDeleted)
                .Single();

            return card != null 
                ? SavedCardViewModel.FromModel(card) 
                : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting default payment method");
            _logger.LogError(ex, "Failed to get default payment method for user: {UserId}", userId);
            return null;
        }
    }
}