// Services/Products/ProductInteractionService.cs - CORRECTED VERSION

using System.Net.Http.Headers;
using System.Net.Http.Json;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Products;

/// <summary>
/// Product interaction service - ONLY handles View and Click events
/// Cart, Wishlist, and Purchase are handled by database triggers
/// </summary>
public class ProductInteractionService : IProductInteractionService, IDisposable
{
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly HttpClient _httpClient;
    private readonly ISupabaseAuthService _authService;
    private readonly ILogger<ProductInteractionService> _logger;
    
    private const string PENDING_INTERACTIONS_KEY = "pending_product_interactions";
    private const string EDGE_FUNCTION_URL = "https://wbwmovtewytjibxutssk.supabase.co/functions/v1/update-product-analytics";
    
    // SIZE-BASED TRIGGERS ONLY
    private const int MAX_BATCH_SIZE = 75; // Flush when reaching this many interactions
    private const int MAX_STORED_INTERACTIONS = 500; // Hard limit to prevent unlimited growth
    private const int MAX_RETRY_ATTEMPTS = 3;
    
    private List<ProductInteraction> _pendingInteractions = new();
    private int _failedAttempts = 0;
    private bool _isFlushingPending = false;

    public ProductInteractionService(
        IBlazorAppLocalStorageService localStorage,
        HttpClient httpClient,
        ISupabaseAuthService authService,
        ILogger<ProductInteractionService> logger)
    {
        _localStorage = localStorage;
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
        
        // Load pending interactions from storage on init
        _ = LoadPendingInteractionsAsync();
    }

    public async Task TrackViewAsync(int productId, string userId)
    {
        await AddInteractionAsync(new ProductInteraction
        {
            ProductId = productId,
            UserId = userId,
            Type = InteractionType.View,
            Timestamp = DateTime.UtcNow
        }, flushImmediately: false);
    }

    public async Task TrackClickAsync(int productId, string userId)
    {
        await AddInteractionAsync(new ProductInteraction
        {
            ProductId = productId,
            UserId = userId,
            Type = InteractionType.Click,
            Timestamp = DateTime.UtcNow
        }, flushImmediately: false);
    }

    // REMOVED: TrackAddToCartAsync - handled by cart table trigger
    // REMOVED: TrackPurchaseAsync - handled by orders table trigger
    // REMOVED: TrackWishlistAsync - handled by wishlist table trigger

    public async Task FlushPendingInteractionsAsync()
    {
        if (_isFlushingPending)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⏸️ Flush already in progress, skipping",
                LogLevel.Debug
            );
            return;
        }

        if (!_pendingInteractions.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "📭 No pending interactions to flush",
                LogLevel.Debug
            );
            return;
        }

        _isFlushingPending = true;

        try
        {
            // Check if user is authenticated
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            
            if (!isAuthenticated)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"⏸️ User not authenticated, keeping {_pendingInteractions.Count} interactions in local storage",
                    LogLevel.Info
                );
                _isFlushingPending = false;
                return;
            }

            // Get auth token
            var session = await _authService.GetCurrentSessionAsync();
            if (session == null || string.IsNullOrEmpty(session.AccessToken))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "⚠️ No valid session found, cannot flush interactions",
                    LogLevel.Warning
                );
                _isFlushingPending = false;
                return;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"🚀 Flushing {_pendingInteractions.Count} pending interactions (View/Click only)",
                LogLevel.Info
            );

            var batch = new ProductInteractionBatch
            {
                Interactions = new List<ProductInteraction>(_pendingInteractions),
                BatchTimestamp = DateTime.UtcNow
            };

            // Set authorization header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", session.AccessToken);

            // Call edge function
            var response = await _httpClient.PostAsJsonAsync(EDGE_FUNCTION_URL, batch);

            if (response.IsSuccessStatusCode)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Successfully flushed {batch.Interactions.Count} interactions",
                    LogLevel.Info
                );

                // Clear pending interactions
                _pendingInteractions.Clear();
                _failedAttempts = 0; // Reset retry counter
                await SavePendingInteractionsAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                
                _failedAttempts++;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Failed to flush interactions (attempt {_failedAttempts}/{MAX_RETRY_ATTEMPTS}): {response.StatusCode} - {error}",
                    LogLevel.Error
                );

                // If max retries exceeded, discard oldest interactions to prevent unlimited growth
                if (_failedAttempts >= MAX_RETRY_ATTEMPTS)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"⚠️ Max retry attempts reached, discarding oldest interactions",
                        LogLevel.Warning
                    );
                    
                    // Keep only most recent half of interactions
                    var keepCount = _pendingInteractions.Count / 2;
                    _pendingInteractions = _pendingInteractions
                        .OrderByDescending(i => i.Timestamp)
                        .Take(keepCount)
                        .ToList();
                    
                    _failedAttempts = 0; // Reset counter
                    await SavePendingInteractionsAsync();
                }

                _logger.LogError("Edge function returned {StatusCode}: {Error}", 
                    response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _failedAttempts++;
            
            await MID_HelperFunctions.LogExceptionAsync(ex, "Flushing product interactions");
            _logger.LogError(ex, "Failed to flush product interactions (attempt {Attempt}/{Max})", 
                _failedAttempts, MAX_RETRY_ATTEMPTS);
            
            // If max retries exceeded, discard oldest interactions
            if (_failedAttempts >= MAX_RETRY_ATTEMPTS)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"⚠️ Max retry attempts reached due to exception, discarding oldest interactions",
                    LogLevel.Warning
                );
                
                var keepCount = _pendingInteractions.Count / 2;
                _pendingInteractions = _pendingInteractions
                    .OrderByDescending(i => i.Timestamp)
                    .Take(keepCount)
                    .ToList();
                
                _failedAttempts = 0;
                await SavePendingInteractionsAsync();
            }
        }
        finally
        {
            _isFlushingPending = false;
        }
    }

    public int GetPendingInteractionCount()
    {
        return _pendingInteractions.Count;
    }

    public void StartAutoFlush()
    {
        // NO-OP: No longer using timer-based auto-flush
        _logger.LogInformation("ℹ️ Auto-flush is disabled - using size-based triggers only");
    }

    public void StopAutoFlush()
    {
        // NO-OP: No timer to stop
        _logger.LogInformation("ℹ️ No auto-flush timer to stop");
    }

    // Private helper methods

    private async Task AddInteractionAsync(ProductInteraction interaction, bool flushImmediately = false)
    {
        try
        {
            _pendingInteractions.Add(interaction);

            await MID_HelperFunctions.DebugMessageAsync(
                $"📝 Tracked {interaction.Type} for product {interaction.ProductId} (pending: {_pendingInteractions.Count})",
                LogLevel.Debug
            );

            // Enforce max storage limit
            if (_pendingInteractions.Count > MAX_STORED_INTERACTIONS)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"⚠️ Max interactions limit ({MAX_STORED_INTERACTIONS}) exceeded, removing oldest",
                    LogLevel.Warning
                );
                
                _pendingInteractions = _pendingInteractions
                    .OrderByDescending(i => i.Timestamp)
                    .Take(MAX_STORED_INTERACTIONS)
                    .ToList();
            }

            // Save to localStorage for persistence
            await SavePendingInteractionsAsync();

            // SIZE-BASED FLUSH: Auto-flush if batch size exceeded
            if (_pendingInteractions.Count >= MAX_BATCH_SIZE)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📦 Max batch size ({MAX_BATCH_SIZE}) reached, triggering flush",
                    LogLevel.Info
                );
                
                await FlushPendingInteractionsAsync();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding product interaction");
            _logger.LogError(ex, "Failed to add product interaction");
        }
    }

    private async Task SavePendingInteractionsAsync()
    {
        try
        {
            await _localStorage.SetItemAsync(PENDING_INTERACTIONS_KEY, _pendingInteractions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save pending interactions to storage");
        }
    }

    private async Task LoadPendingInteractionsAsync()
    {
        try
        {
            var stored = await _localStorage.GetItemAsync<List<ProductInteraction>>(PENDING_INTERACTIONS_KEY);
            
            if (stored != null && stored.Any())
            {
                // Enforce max limit on load as well
                _pendingInteractions = stored
                    .OrderByDescending(i => i.Timestamp)
                    .Take(MAX_STORED_INTERACTIONS)
                    .ToList();
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📥 Loaded {_pendingInteractions.Count} pending interactions from storage",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pending interactions from storage");
        }
    }

    public void Dispose()
    {
        // Nothing to dispose - no timer
    }
}
