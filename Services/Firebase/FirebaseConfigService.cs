// Services/Firebase/FirebaseConfigService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;

namespace SubashaVentures.Services.Firebase
{
    public interface IFirebaseConfigService
    {
        Task InitializeAsync();
        Task<bool> IsInitializedAsync();
        string GetProjectId();
    }

    public class FirebaseConfigService : IFirebaseConfigService
    {
        private readonly IConfiguration _configuration;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<FirebaseConfigService> _logger;
        private bool _isInitialized = false;

        public FirebaseConfigService(
            IConfiguration configuration,
            IJSRuntime jsRuntime,
            ILogger<FirebaseConfigService> logger)
        {
            _configuration = configuration;
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogInformation("Firebase already initialized");
                return;
            }

            try
            {
                var config = new
                {
                    apiKey = _configuration["Firebase:ApiKey"],
                    authDomain = _configuration["Firebase:AuthDomain"],
                    projectId = _configuration["Firebase:ProjectId"],
                    storageBucket = _configuration["Firebase:StorageBucket"],
                    messagingSenderId = _configuration["Firebase:MessagingSenderId"],
                    appId = _configuration["Firebase:AppId"],
                    measurementId = _configuration["Firebase:MeasurementId"],
                    databaseURL = _configuration["Firebase:DatabaseURL"]
                };

                // Validate configuration
                if (string.IsNullOrEmpty(config.apiKey) || string.IsNullOrEmpty(config.projectId))
                {
                    throw new InvalidOperationException("Firebase configuration is incomplete");
                }

                // Initialize Firebase through JavaScript
                await _jsRuntime.InvokeVoidAsync("initializeFirebaseFromConfig", config);
                
                _isInitialized = true;
                _logger.LogInformation("Firebase initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firebase");
                throw;
            }
        }

        public async Task<bool> IsInitializedAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("isFirebaseInitialized");
            }
            catch
            {
                return false;
            }
        }

        public string GetProjectId()
        {
            return _configuration["Firebase:ProjectId"] ?? string.Empty;
        }
    }
}
