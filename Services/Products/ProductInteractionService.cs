// Services/Products/ProductInteractionService.cs
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using System.Timers;
using Timer = System.Timers.Timer;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Products;

public class ProductInteractionService : IProductInteractionService, IDisposable
{
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductInteractionService> _logger;
    
    private const string PENDING_INTERACTIONS_KEY = "pending_product_interactions";
    private const string EDGE_FUNCTION_URL = "https://wbwmovtewytjibxutssk.supabase.co/functions/v1/update-product-analytics";
    private const int AUTO_FLUSH_INTERVAL_MS = 30000; // 30 seconds
    private const int MAX_BATCH_SIZE = 50; // Flush if batch exceeds this
    
    private List<ProductInteraction> _pendingInteractions = new();
    private Timer? _autoFlushTimer;
    private bool _isFlushingpending = false;

    public ProductInteractionService(
        IBlazorAppLocalStorageService localStorage,
        HttpClient httpClient,
        ILogger<ProductInteractionService> logger)
    {
        _localStorage = localStorage;
        _httpClient = httpClient;
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
        });
    }

    public async Task TrackClickAsync(int productId, string userId)
    {
        await AddInteractionAsync(new ProductInteraction
        {
            ProductId = productId,
            UserId = userId,
            Type = InteractionType.Click,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task TrackAddToCartAsync(int productId, string userId)
    {
        await AddInteractionAsync(new ProductInteraction
        {
            ProductId = productId,
            UserId = userId,
            Type = InteractionType.AddToCart,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task TrackPurchaseAsync(int productId, string userId, decimal amount, int quantity)
    {
        await AddInteractionAsync(new ProductInteraction
        {
            ProductId = productId,
            UserId = userId,
            Type = InteractionType.Purchase,
            Timestamp = DateTime.UtcNow,
            Amount = amount,
            Quantity = quantity
        });
    }

    public async Task TrackWishlistAsync(int productId, string userId)
    {
        await AddInteractionAsync(new ProductInteraction
        {
            ProductId = productId,
            UserId = userId,
            Type = InteractionType.Wishlist,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task FlushPendingInteractionsAsync()
    {
        if (_isFlushingpending)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Flush already in progress, skipping",
                LogLevel.Debug
            );
            return;
        }

        if (!_pendingInteractions.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "No pending interactions to flush",
                LogLevel.Debug
            );
            return;
        }

        _isFlushingpending = true;

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Flushing {_pendingInteractions.Count} pending interactions",
                LogLevel.Info
            );

            var batch = new ProductInteractionBatch
            {
                Interactions = new List<ProductInteraction>(_pendingInteractions),
                BatchTimestamp = DateTime.UtcNow
            };

            // Call edge function
            var response = await _httpClient.PostAsJsonAsync(EDGE_FUNCTION_URL, batch);

            if (response.IsSuccessStatusCode)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Successfully flushed {batch.Interactions.Count} interactions",
                    LogLevel.Info
                );

                // Clear pending interactions
                _pendingInteractions.Clear();
                await SavePendingInteractionsAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Failed to flush interactions: {response.StatusCode} - {error}",
                    LogLevel.Error
                );

                // Keep pending interactions for retry
                _logger.LogError("Edge function returned {StatusCode}: {Error}", 
                    response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Flushing product interactions");
            _logger.LogError(ex, "Failed to flush product interactions");
            
            // Keep pending interactions for retry
        }
        finally
        {
            _isFlushingpending = false;
        }
    }

    public int GetPendingInteractionCount()
    {
        return _pendingInteractions.Count;
    }

    public void StartAutoFlush()
    {
        if (_autoFlushTimer != null)
        {
            _logger.LogWarning("Auto-flush timer already running");
            return;
        }

        _autoFlushTimer = new Timer(AUTO_FLUSH_INTERVAL_MS);
        _autoFlushTimer.Elapsed += async (sender, e) => await OnAutoFlushTimer();
        _autoFlushTimer.AutoReset = true;
        _autoFlushTimer.Start();

        _logger.LogInformation("✓ Auto-flush timer started (interval: {Interval}ms)", AUTO_FLUSH_INTERVAL_MS);
    }

    public void StopAutoFlush()
    {
        if (_autoFlushTimer != null)
        {
            _autoFlushTimer.Stop();
            _autoFlushTimer.Dispose();
            _autoFlushTimer = null;
            _logger.LogInformation("Auto-flush timer stopped");
        }
    }

    // Private helper methods

    private async Task AddInteractionAsync(ProductInteraction interaction)
    {
        try
        {
            _pendingInteractions.Add(interaction);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Tracked {interaction.Type} for product {interaction.ProductId} (pending: {_pendingInteractions.Count})",
                LogLevel.Debug
            );

            // Save to localStorage for persistence
            await SavePendingInteractionsAsync();

            // Auto-flush if batch size exceeded
            if (_pendingInteractions.Count >= MAX_BATCH_SIZE)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Max batch size ({MAX_BATCH_SIZE}) reached, triggering flush",
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
                _pendingInteractions = stored;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Loaded {_pendingInteractions.Count} pending interactions from storage",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pending interactions from storage");
        }
    }

    private async Task OnAutoFlushTimer()
    {
        if (_pendingInteractions.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Auto-flush triggered",
                LogLevel.Info
            );
            
            await FlushPendingInteractionsAsync();
        }
    }

    public void Dispose()
    {
        StopAutoFlush();
    }
}
