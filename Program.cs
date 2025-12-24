// Program.cs - FIXED WITH CORRECT NAMESPACE
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SubashaVentures;
using SubashaVentures.Services.Navigation;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Time;
using SubashaVentures.Services.Connectivity;
using SubashaVentures.Services.Products;
using SubashaVentures.Utilities.Logging;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Brands;
using Blazored.LocalStorage;
using Blazored.Toast;
using Microsoft.JSInterop;
using SubashaVentures.Services.SupaBase;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Services.Auth;
using SubashaVentures.Services.Statistics;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.Authorization;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces; // ✅ CORRECT NAMESPACE
using Newtonsoft.Json;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient 
{  
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromSeconds(30)
});

builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("SubashaVentures", LogLevel.Debug);

builder.Services.AddSingleton<IMid_Logger, Mid_Logger>();

builder.Services.AddBlazoredLocalStorage(config =>
{
    config.JsonSerializerOptions.WriteIndented = false;
    config.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddBlazoredToast();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();
builder.Services.AddScoped<CustomSupabaseClaimsFactory>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("SuperiorAdminOnly", policy =>
        policy.RequireRole("superior_admin"));
    
    options.AddPolicy("AuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());
    
    options.AddPolicy("AnyRole", policy =>
        policy.RequireRole("superior_admin", "user"));
});

builder.Services.AddSingleton<INavigationService, NavigationService>();
builder.Services.AddScoped<ConnectivityService>();
builder.Services.AddScoped<IServerTimeService, ServerTimeService>();
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();
builder.Services.AddScoped<IImageCompressionService, ImageCompressionService>();
builder.Services.AddScoped<IImageCacheService, ImageCacheService>();

builder.Services.AddScoped<IFirebaseConfigService, FirebaseConfigService>();
builder.Services.AddScoped<IFirestoreService, FirestoreService>();

var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:AnonKey"];

if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
{
    throw new InvalidOperationException("Supabase URL and AnonKey must be configured");
}

// Build host first to access services
var host = builder.Build();

// ============================================================================
// ✅ CRITICAL FIX: Setup Supabase with Session Persistence
// ============================================================================
var localStorage = host.Services.GetRequiredService<ILocalStorageService>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

const string SESSION_KEY = "supabase.auth.token";

var sessionHandler = new SupabaseSessionHandler(localStorage, SESSION_KEY, logger);

var options = new SupabaseOptions
{
    AutoRefreshToken = true,
    AutoConnectRealtime = false,
    SessionHandler = sessionHandler // ✅ Set the custom handler
};

var supabaseClient = new Supabase.Client(supabaseUrl, supabaseKey, options);

// Register as singleton
builder.Services.AddSingleton(supabaseClient);

builder.Services.AddScoped<ISupabaseConfigService, SupabaseConfigService>();
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
builder.Services.AddScoped<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddScoped<ISupabaseDatabaseService, SupabaseDatabaseService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductOfTheDayService, ProductOfTheDayService>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<SubashaVentures.Services.Shop.ShopStateService>();

try
{
    var midLogger = host.Services.GetRequiredService<IMid_Logger>();
    var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
    
    midLogger.Initialize(logger, jsRuntime);
    MID_HelperFunctions.Initialize(midLogger);
    
    logger.LogInformation("✓ Mid_Logger initialized");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to initialize logging: {ex.Message}");
}

try
{
    var firebaseConfig = host.Services.GetRequiredService<IFirebaseConfigService>();
    await firebaseConfig.InitializeAsync();
    
    logger.LogInformation("✓ Firebase initialized");
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Failed to initialize Firebase");
}

// ✅ Initialize Supabase (this will call LoadSession automatically)
try
{
    await supabaseClient.InitializeAsync();
    logger.LogInformation("✓ Supabase client initialized with session persistence");
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Failed to initialize Supabase client");
}

await host.RunAsync();

// ============================================================================
// ✅ FIXED: Custom Session Handler with SYNCHRONOUS methods
// ============================================================================
public class SupabaseSessionHandler : IGotrueSessionPersistence<Session>
{
    private readonly ILocalStorageService _localStorage;
    private readonly string _sessionKey;
    private readonly ILogger _logger;

    public SupabaseSessionHandler(
        ILocalStorageService localStorage, 
        string sessionKey,
        ILogger logger)
    {
        _localStorage = localStorage;
        _sessionKey = sessionKey;
        _logger = logger;
    }

    // ✅ SYNCHRONOUS - LoadSession
    public Session? LoadSession()
    {
        try
        {
            // ⚠️ SYNC blocking call - this is required by the interface
            var storedSession = _localStorage.GetItemAsStringAsync(_sessionKey).GetAwaiter().GetResult();
            
            if (string.IsNullOrEmpty(storedSession))
                return null;

            var session = JsonConvert.DeserializeObject<Session>(storedSession);
            
            if (session != null && session.ExpiresAt() > DateTime.UtcNow)
            {
                _logger.LogInformation("✓ Session loaded from storage");
                return session;
            }
            
            _logger.LogInformation("ℹ️ Stored session expired");
            _localStorage.RemoveItemAsync(_sessionKey).GetAwaiter().GetResult();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session");
            return null;
        }
    }

    // ✅ SYNCHRONOUS - SaveSession
    public void SaveSession(Session session)
    {
        try
        {
            var serialized = JsonConvert.SerializeObject(session);
            // ⚠️ SYNC blocking call - this is required by the interface
            _localStorage.SetItemAsStringAsync(_sessionKey, serialized).GetAwaiter().GetResult();
            _logger.LogInformation("✓ Session saved to storage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session");
        }
    }

    // ✅ SYNCHRONOUS - DestroySession
    public void DestroySession()
    {
        try
        {
            // ⚠️ SYNC blocking call - this is required by the interface
            _localStorage.RemoveItemAsync(_sessionKey).GetAwaiter().GetResult();
            _logger.LogInformation("✓ Session destroyed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to destroy session");
        }
    }
}
