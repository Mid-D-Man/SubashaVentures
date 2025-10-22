using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using Microsoft.Extensions.Configuration;

namespace SubashaVentures.Services.Supabase;

public class SupabaseConfigService : ISupabaseConfigService
{
    private readonly IConfiguration _configuration;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<SupabaseConfigService> _logger;
    private SupabaseConfig? _cachedConfig;
    private readonly object _lockObject = new object();
    
    private const string CONFIG_STORAGE_KEY = "supabase_config";

    public SupabaseConfigService(
        IConfiguration configuration,
        IBlazorAppLocalStorageService localStorage,
        ILogger<SupabaseConfigService> logger)
    {
        _configuration = configuration;
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task<SupabaseConfig> GetConfigAsync()
    {
        if (_cachedConfig != null)
            return _cachedConfig;

        lock (_lockObject)
        {
            if (_cachedConfig != null)
                return _cachedConfig;

            // Try to load from configuration first
            var configFromSettings = LoadFromConfiguration();
            if (configFromSettings != null && ValidateConfig(configFromSettings))
            {
                _cachedConfig = configFromSettings;
                return _cachedConfig;
            }
        }

        // Try to load from local storage
        try
        {
            var storedConfig = await _localStorage.GetItemAsync<SupabaseConfig>(CONFIG_STORAGE_KEY);
            if (storedConfig != null && ValidateConfig(storedConfig))
            {
                lock (_lockObject)
                {
                    _cachedConfig = storedConfig;
                }
                return storedConfig;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Supabase config from local storage");
        }

        // Return default config if nothing found
        var defaultConfig = GetDefaultConfig();
        lock (_lockObject)
        {
            _cachedConfig = defaultConfig;
        }
        return defaultConfig;
    }

    public async Task UpdateConfigAsync(SupabaseConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (!ValidateConfig(config))
            throw new InvalidOperationException("Invalid Supabase configuration");

        try
        {
            await _localStorage.SetItemAsync(CONFIG_STORAGE_KEY, config);
            
            lock (_lockObject)
            {
                _cachedConfig = config;
            }
            
            _logger.LogInformation("Supabase configuration updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Supabase configuration");
            throw;
        }
    }

    public async Task<bool> ValidateConfigAsync()
    {
        try
        {
            var config = await GetConfigAsync();
            return ValidateConfig(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Supabase configuration");
            return false;
        }
    }

    public string GetSupabaseUrl()
    {
        var config = GetConfigAsync().GetAwaiter().GetResult();
        return config.Url;
    }

    public string GetAnonKey()
    {
        var config = GetConfigAsync().GetAwaiter().GetResult();
        return config.AnonKey;
    }

    private SupabaseConfig? LoadFromConfiguration()
    {
        try
        {
            var url = _configuration["Supabase:Url"];
            var anonKey = _configuration["Supabase:AnonKey"];
            var serviceRoleKey = _configuration["Supabase:ServiceRoleKey"];

            if (MID_HelperFunctions.IsValidString(url) && MID_HelperFunctions.IsValidString(anonKey))
            {
                return new SupabaseConfig
                {
                    Url = url!,
                    AnonKey = anonKey!,
                    ServiceRoleKey = serviceRoleKey ?? string.Empty,
                    AutoRefreshToken = bool.TryParse(_configuration["Supabase:AutoRefreshToken"], out var autoRefresh) && autoRefresh,
                    PersistSession = bool.TryParse(_configuration["Supabase:PersistSession"], out var persistSession) && persistSession,
                    SessionTimeout = int.TryParse(_configuration["Supabase:SessionTimeout"], out var timeout) ? timeout : 3600
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Supabase configuration from settings");
        }

        return null;
    }

    private SupabaseConfig GetDefaultConfig()
    {
        return new SupabaseConfig
        {
            Url = string.Empty,
            AnonKey = string.Empty,
            ServiceRoleKey = string.Empty,
            AutoRefreshToken = true,
            PersistSession = true,
            SessionTimeout = 3600
        };
    }

    private bool ValidateConfig(SupabaseConfig config)
    {
        if (config == null)
            return false;

        if (!MID_HelperFunctions.IsValidString(config.Url))
        {
            _logger.LogWarning("Supabase URL is invalid");
            return false;
        }

        if (!MID_HelperFunctions.IsValidString(config.AnonKey))
        {
            _logger.LogWarning("Supabase anonymous key is invalid");
            return false;
        }

        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out _))
        {
            _logger.LogWarning("Supabase URL is not a valid URI");
            return false;
        }

        return true;
    }
}