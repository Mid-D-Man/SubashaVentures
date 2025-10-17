// Services/SupaBase/SupabaseConfigService.cs
using Microsoft.Extensions.Configuration;
using Supabase;
using Supabase.Gotrue;

namespace SubashaVentures.Services.SupaBase
{
    public interface ISupabaseConfigService
    {
        Task<Client> GetClientAsync();
        bool IsConfigured { get; }
    }

    public class SupabaseConfigService : ISupabaseConfigService
    {
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;
        private Client? _client;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly ILogger<SupabaseConfigService> _logger;

        public bool IsConfigured { get; private set; }

        public SupabaseConfigService(IConfiguration configuration, ILogger<SupabaseConfigService> logger)
        {
            _logger = logger;
            _supabaseUrl = configuration["Supabase:Url"] ?? 
                          throw new ArgumentNullException("Supabase:Url configuration is missing");
            _supabaseKey = configuration["Supabase:AnonKey"] ?? 
                          throw new ArgumentNullException("Supabase:AnonKey configuration is missing");
            
            IsConfigured = !string.IsNullOrEmpty(_supabaseUrl) && !string.IsNullOrEmpty(_supabaseKey);
        }

        public async Task<Client> GetClientAsync()
        {
            if (_client != null)
                return _client;

            await _initLock.WaitAsync();
            try
            {
                if (_client == null)
                {
                    var options = new SupabaseOptions
                    {
                        AutoRefreshToken = true,
                        AutoConnectRealtime = true,
                        SessionHandler = new SupabaseSessionHandler()
                    };

                    _client = new Client(_supabaseUrl, _supabaseKey, options);
                    await _client.InitializeAsync();
                    
                    _logger.LogInformation("Supabase client initialized successfully");
                }

                return _client;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Supabase client");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }
    }
}