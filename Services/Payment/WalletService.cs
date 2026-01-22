// Services/Payment/WalletService.cs
using SubashaVentures.Domain.Payment;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using System.Net.Http.Json;
using System.Text.Json;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Payment;

public class WalletService : IWalletService
{
    private readonly Client _supabaseClient;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WalletService> _logger;
    private readonly string _supabaseUrl;
    private readonly string _supabaseAnonKey;
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public WalletService(
        Client supabaseClient,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WalletService> logger)
    {
        _supabaseClient = supabaseClient;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        
        _supabaseUrl = configuration["Supabase:Url"] ?? "https://wbwmovtewytjibxutssk.supabase.co";
        _supabaseAnonKey = configuration["Supabase:AnonKey"] ?? string.Empty;
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
            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating wallet via edge function for user: {userId}",
                LogLevel.Info
            );

            // Get current auth token
            var session = _supabaseClient.Auth.CurrentSession;
            if (session == null)
            {
                throw new Exception("User not authenticated");
            }

            // Call edge function to create wallet
            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{_supabaseUrl}/functions/v1/create-wallet");
            
            request.Headers.Add("Authorization", $"Bearer {session.AccessToken}");
            request.Headers.Add("apikey", _supabaseAnonKey);
            
            request.Content = JsonContent.Create(new { userId });

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Edge function response: {response.StatusCode}",
                LogLevel.Debug
            );

            if (!response.IsSuccessStatusCode)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Edge function error: {content}",
                    LogLevel.Error
                );
                throw new Exception($"Failed to create wallet: {content}");
            }

            var result = JsonSerializer.Deserialize<WalletCreationResponse>(content, _jsonOptions);

            if (result?.Success == true && result.Wallet != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Wallet created successfully via edge function",
                    LogLevel.Info
                );

                return new WalletViewModel
                {
                    UserId = result.Wallet.UserId,
                    Balance = result.Wallet.Balance,
                    Currency = result.Wallet.Currency,
                    IsLocked = result.Wallet.IsLocked,
                    CreatedAt = result.Wallet.CreatedAt
                };
            }

            throw new Exception(result?.Message ?? "Unknown error creating wallet");
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
            // Try to get existing wallet
            var wallet = await GetWalletAsync(userId);
            
            if (wallet != null)
            {
                return wallet;
            }

            // Create new wallet if doesn't exist
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

            // This will be handled by the verify-and-credit-wallet edge function
            // after payment verification
            await MID_HelperFunctions.DebugMessageAsync(
                "Top-up will be processed after payment verification",
                LogLevel.Info
            );

            return null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Top-up wallet");
            _logger.LogError(ex, "Failed to top-up wallet for user: {UserId}", userId);
            throw;
        }
    }
/// <summary>
/// Manually verify and credit wallet after payment completion
/// Calls the verify-and-credit-wallet edge function
/// </summary>
public async Task<bool> VerifyAndCreditWalletAsync(string reference, string provider)
{
    try
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"Manually verifying payment: {reference}",
            LogLevel.Info
        );

        // Get current auth token
        var session = _supabaseClient.Auth.CurrentSession;
        if (session == null)
        {
            throw new Exception("User not authenticated");
        }

        // Call edge function to verify and credit wallet
        var request = new HttpRequestMessage(HttpMethod.Post, 
            $"{_supabaseUrl}/functions/v1/verify-and-credit-wallet");
        
        request.Headers.Add("Authorization", $"Bearer {session.AccessToken}");
        request.Headers.Add("apikey", _supabaseAnonKey);
        
        var payload = new
        {
            reference,
            provider
        };

        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Verification failed: {content}",
                LogLevel.Error
            );
            return false;
        }

        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(content, _jsonOptions);

        if (result?["success"]?.ToString()?.ToLower() == "true")
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Payment verified and wallet credited",
                LogLevel.Info
            );
            return true;
        }

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
            if (amount <= 0)
            {
                throw new ArgumentException("Deduction amount must be greater than zero");
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Deduction request: User={userId}, Amount=₦{amount}",
                LogLevel.Info
            );

            // Get current auth token
            var session = _supabaseClient.Auth.CurrentSession;
            if (session == null)
            {
                throw new Exception("User not authenticated");
            }

            // Call edge function to deduct from wallet
            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{_supabaseUrl}/functions/v1/deduct-from-wallet");
            
            request.Headers.Add("Authorization", $"Bearer {session.AccessToken}");
            request.Headers.Add("apikey", _supabaseAnonKey);
            
            var payload = new
            {
                userId,
                amount,
                description,
                orderId,
                metadata = new Dictionary<string, object>
                {
                    { "description", description }
                }
            };

            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorData = JsonSerializer.Deserialize<Dictionary<string, object>>(content, _jsonOptions);
                throw new Exception(errorData?["message"]?.ToString() ?? "Failed to deduct from wallet");
            }

            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(content, _jsonOptions);

            if (result?["success"]?.ToString()?.ToLower() == "true")
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Wallet deducted successfully",
                    LogLevel.Info
                );

                // Fetch the transaction details
                var txRef = result["reference"]?.ToString();
                if (!string.IsNullOrEmpty(txRef))
                {
                    return await GetTransactionByReferenceAsync(txRef);
                }
            }

            return null;
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

            // Build query with chained filters to avoid NullReferenceException
            var query = _supabaseClient
                .From<UserPaymentMethodModel>()
                .Filter("user_id", Constants.Operator.Equals, userId)
                .Filter("is_deleted", Constants.Operator.Equals, false)
                .Order("is_default", Constants.Ordering.Descending)
                .Order("created_at", Constants.Ordering.Descending);

            var response = await query.Get();

            // Handle empty results gracefully
            var cards = response?.Models ?? new List<UserPaymentMethodModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {cards.Count} saved cards",
                LogLevel.Info
            );

            return cards
                .Select(SavedCardViewModel.FromModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting saved cards");
            _logger.LogError(ex, "Failed to get saved cards for user: {UserId}", userId);
        
            // Return empty list instead of throwing
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
            await MID_HelperFunctions.DebugMessageAsync(
                $"Verifying and saving payment method: User={userId}, Provider={provider}",
                LogLevel.Info
            );

            // Get current auth token
            var session = _supabaseClient.Auth.CurrentSession;
            if (session == null)
            {
                throw new Exception("User not authenticated");
            }

            // Call edge function to verify card token with payment gateway
            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{_supabaseUrl}/functions/v1/verify-card-token");
            
            request.Headers.Add("Authorization", $"Bearer {session.AccessToken}");
            request.Headers.Add("apikey", _supabaseAnonKey);
            
            var payload = new
            {
                userId,
                provider,
                authorizationCode,
                email
            };

            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Card verification response: {response.StatusCode}",
                LogLevel.Debug
            );

            if (!response.IsSuccessStatusCode)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Card verification failed: {content}",
                    LogLevel.Error
                );
                throw new Exception($"Card verification failed: {content}");
            }

            var verificationResult = JsonSerializer.Deserialize<CardVerificationResponse>(
                content, 
                _jsonOptions
            );

            if (verificationResult?.Success != true || verificationResult.CardDetails == null)
            {
                throw new Exception(verificationResult?.Message ?? "Card verification failed");
            }

            if (!verificationResult.CardDetails.Reusable)
            {
                throw new Exception("This card cannot be saved for future use");
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Card verified successfully, saving to database",
                LogLevel.Info
            );

            // If setting as default, remove default from others first
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

            // Save payment method to database
            var paymentMethod = new UserPaymentMethodModel
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Provider = provider,
                AuthorizationCode = authorizationCode,
                CardType = verificationResult.CardDetails.CardType,
                CardLast4 = verificationResult.CardDetails.Last4,
                CardExpMonth = verificationResult.CardDetails.ExpMonth,
                CardExpYear = verificationResult.CardDetails.ExpYear,
                CardBank = verificationResult.CardDetails.Bank,
                CardBrand = verificationResult.CardDetails.Brand,
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