// Services/Payment/WalletService.cs
using SubashaVentures.Domain.Payment;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Payment;

public class WalletService : IWalletService
{
    private readonly Client _supabaseClient;
    private readonly ILogger<WalletService> _logger;
    private readonly ISupabaseDatabaseService _databaseService;

    public WalletService(
        Client supabaseClient,
        ILogger<WalletService> logger,
        ISupabaseDatabaseService databaseService)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
        _databaseService = databaseService;
    }

    // ==================== WALLET OPERATIONS ====================

    public async Task<WalletViewModel?> GetWalletAsync(string userId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting wallet for user: {userId}",
                LogLevel.Info
            );

            var wallet = await _supabaseClient
                .From<UserWalletModel>()
                .Where(w => w.UserId == userId)
                .Single();

            if (wallet == null)
            {
                // Auto-create wallet if it doesn't exist
                await MID_HelperFunctions.DebugMessageAsync(
                    "Wallet not found, creating new wallet",
                    LogLevel.Info
                );
                return await CreateWalletAsync(userId);
            }

            return WalletViewModel.FromModel(wallet);
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
            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating wallet for user: {userId}",
                LogLevel.Info
            );

            var wallet = new UserWalletModel
            {
                UserId = userId,
                Balance = 0,
                Currency = "NGN",
                IsLocked = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            var created = await _supabaseClient
                .From<UserWalletModel>()
                .Insert(wallet);

            if (created != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Wallet created successfully",
                    LogLevel.Info
                );
                return WalletViewModel.FromModel(created);
            }

            return null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating wallet");
            _logger.LogError(ex, "Failed to create wallet for user: {UserId}", userId);
            return null;
        }
    }

    public async Task<WalletTransactionViewModel?> TopUpWalletAsync(
        string userId,
        decimal amount,
        string paymentReference,
        string provider)
    {
        try
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Top-up amount must be greater than zero");
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Top-up request: User={userId}, Amount=₦{amount}, Provider={provider}",
                LogLevel.Info
            );

            // Get current wallet
            var wallet = await _supabaseClient
                .From<UserWalletModel>()
                .Where(w => w.UserId == userId)
                .Single();

            if (wallet == null)
            {
                throw new Exception("Wallet not found");
            }

            if (wallet.IsLocked)
            {
                throw new Exception("Wallet is locked. Please contact support.");
            }

            var balanceBefore = wallet.Balance;
            var balanceAfter = balanceBefore + amount;

            // Create transaction record
            var transaction = new WalletTransactionModel
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Type = "topup",
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                Reference = $"TOPUP-{Guid.NewGuid().ToString().Substring(0, 8)}",
                PaymentProvider = provider,
                PaymentReference = paymentReference,
                Description = $"Wallet top-up via {provider}",
                Metadata = new Dictionary<string, object>
                {
                    { "provider", provider },
                    { "payment_reference", paymentReference }
                },
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            var createdTransaction = await _supabaseClient
                .From<WalletTransactionModel>()
                .Insert(transaction);

            // Update wallet balance
            wallet.Balance = balanceAfter;
            wallet.UpdatedAt = DateTime.UtcNow;
            wallet.UpdatedBy = userId;

            await _supabaseClient
                .From<UserWalletModel>()
                .Update(wallet);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Top-up successful: Balance {balanceBefore:C} → {balanceAfter:C}",
                LogLevel.Info
            );

            return createdTransaction != null 
                ? WalletTransactionViewModel.FromModel(createdTransaction) 
                : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Top-up wallet");
            _logger.LogError(ex, "Failed to top-up wallet for user: {UserId}", userId);
            throw;
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
            if (amount <= 0)
            {
                throw new ArgumentException("Deduction amount must be greater than zero");
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Deduction request: User={userId}, Amount=₦{amount}",
                LogLevel.Info
            );

            // Get current wallet
            var wallet = await _supabaseClient
                .From<UserWalletModel>()
                .Where(w => w.UserId == userId)
                .Single();

            if (wallet == null)
            {
                throw new Exception("Wallet not found");
            }

            if (wallet.IsLocked)
            {
                throw new Exception("Wallet is locked. Please contact support.");
            }

            if (wallet.Balance < amount)
            {
                throw new Exception($"Insufficient balance. Available: ₦{wallet.Balance:N0}, Required: ₦{amount:N0}");
            }

            var balanceBefore = wallet.Balance;
            var balanceAfter = balanceBefore - amount;

            // Create transaction record
            var metadata = new Dictionary<string, object>
            {
                { "description", description }
            };
            
            if (!string.IsNullOrEmpty(orderId))
            {
                metadata.Add("order_id", orderId);
            }

            var transaction = new WalletTransactionModel
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Type = "purchase",
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                Reference = $"PURCHASE-{Guid.NewGuid().ToString().Substring(0, 8)}",
                Description = description,
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            var createdTransaction = await _supabaseClient
                .From<WalletTransactionModel>()
                .Insert(transaction);

            // Update wallet balance
            wallet.Balance = balanceAfter;
            wallet.UpdatedAt = DateTime.UtcNow;
            wallet.UpdatedBy = userId;

            await _supabaseClient
                .From<UserWalletModel>()
                .Update(wallet);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Deduction successful: Balance {balanceBefore:C} → {balanceAfter:C}",
                LogLevel.Info
            );

            return createdTransaction != null 
                ? WalletTransactionViewModel.FromModel(createdTransaction) 
                : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deduct from wallet");
            _logger.LogError(ex, "Failed to deduct from wallet for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<WalletTransactionViewModel?> RefundToWalletAsync(
        string userId,
        decimal amount,
        string description,
        string originalReference)
    {
        try
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Refund amount must be greater than zero");
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Refund request: User={userId}, Amount=₦{amount}",
                LogLevel.Info
            );

            // Get current wallet
            var wallet = await _supabaseClient
                .From<UserWalletModel>()
                .Where(w => w.UserId == userId)
                .Single();

            if (wallet == null)
            {
                throw new Exception("Wallet not found");
            }

            var balanceBefore = wallet.Balance;
            var balanceAfter = balanceBefore + amount;

            // Create transaction record
            var transaction = new WalletTransactionModel
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Type = "refund",
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                Reference = $"REFUND-{Guid.NewGuid().ToString().Substring(0, 8)}",
                Description = description,
                Metadata = new Dictionary<string, object>
                {
                    { "original_reference", originalReference },
                    { "refund_reason", description }
                },
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            var createdTransaction = await _supabaseClient
                .From<WalletTransactionModel>()
                .Insert(transaction);

            // Update wallet balance
            wallet.Balance = balanceAfter;
            wallet.UpdatedAt = DateTime.UtcNow;
            wallet.UpdatedBy = "system";

            await _supabaseClient
                .From<UserWalletModel>()
                .Update(wallet);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Refund successful: Balance {balanceBefore:C} → {balanceAfter:C}",
                LogLevel.Info
            );

            return createdTransaction != null 
                ? WalletTransactionViewModel.FromModel(createdTransaction) 
                : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Refund to wallet");
            _logger.LogError(ex, "Failed to refund to wallet for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> HasSufficientBalanceAsync(string userId, decimal amount)
    {
        try
        {
            var wallet = await _supabaseClient
                .From<UserWalletModel>()
                .Where(w => w.UserId == userId)
                .Single();

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
            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting transaction history for user: {userId}",
                LogLevel.Info
            );

            var transactions = await _supabaseClient
                .From<WalletTransactionModel>()
                .Where(t => t.UserId == userId)
                .Order(t => t.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                .Range(skip, skip + take - 1)
                .Get();

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
            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting saved cards for user: {userId}",
                LogLevel.Info
            );

            var cards = await _supabaseClient
                .From<UserPaymentMethodModel>()
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .Order(c => c.IsDefault, Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            return cards.Models
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
        CardDetails cardDetails,
        bool setAsDefault = false)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Saving payment method for user: {userId}, Provider: {provider}",
                LogLevel.Info
            );

            // If setting as default, remove default from others
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

            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Payment method saved successfully",
                LogLevel.Info
            );

            return created != null 
                ? SavedCardViewModel.FromModel(created) 
                : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving payment method");
            _logger.LogError(ex, "Failed to save payment method for user: {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> SetDefaultPaymentMethodAsync(string userId, string paymentMethodId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Setting default payment method: {paymentMethodId}",
                LogLevel.Info
            );

            // Remove default from all cards
            var allCards = await _supabaseClient
                .From<UserPaymentMethodModel>()
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .Get();

            foreach (var card in allCards.Models)
            {
                card.IsDefault = card.Id == paymentMethodId;
                card.UpdatedAt = DateTime.UtcNow;
                card.UpdatedBy = userId;
                await _supabaseClient.From<UserPaymentMethodModel>().Update(card);
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
            _logger.LogError(ex, "Failed to set default payment method");
            return false;
        }
    }

    public async Task<bool> DeletePaymentMethodAsync(string userId, string paymentMethodId)
    {
        try
        {
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
                return false;
            }

            // Soft delete
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
