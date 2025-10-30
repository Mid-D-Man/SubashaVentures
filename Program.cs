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
using Blazored.LocalStorage;
using Blazored.Toast;
using Supabase;
using Microsoft.JSInterop;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ==================== HTTP CLIENT ========== ==========
builder.Services.AddScoped(sp => new HttpClient 
{  
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromSeconds(30)
});

// ==================== LOGGING ====================
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("SubashaVentures", LogLevel.Debug);

// Register Mid_Logger as Singleton (it's thread-safe)
builder.Services.AddSingleton<IMid_Logger, Mid_Logger>();

// ==================== LOCAL STORAGE ====================
builder.Services.AddBlazoredLocalStorage(config =>
{
    config.JsonSerializerOptions.WriteIndented = false;
    config.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();

// ==================== TOAST NOTIFICATIONS ====================
builder.Services.AddBlazoredToast();

// ==================== NAVIGATION ====================
builder.Services.AddSingleton<INavigationService, NavigationService>();

// ==================== CONNECTIVITY ====================
builder.Services.AddScoped<ConnectivityService>();

// ==================== TIME SERVICES ====================
builder.Services.AddScoped<IServerTimeService, ServerTimeService>();

// ==================== FIREBASE SERVICES ====================
builder.Services.AddScoped<IFirebaseConfigService, FirebaseConfigService>();
builder.Services.AddScoped<IFirestoreService, FirestoreService>();

// ==================== SUPABASE SERVICES ====================
var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:AnonKey"];

if (!string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
{
    builder.Services.AddScoped<Supabase.Client>(_ =>
    {
        var options = new Supabase.SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false,
            SessionHandler = new DefaultSupabaseSessionHandler()
        };
        
        return new Supabase.Client(supabaseUrl, supabaseKey, options);
    });
}

builder.Services.AddScoped<ISupabaseConfigService, SupabaseConfigService>();
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
builder.Services.AddScoped<ISupabaseStorageService, SupabaseStorageService>();

// ==================== IMAGE SERVICES ====================
builder.Services.AddScoped<IImageCompressionService, ImageCompressionService>();

// ==================== PRODUCT SERVICES ====================
builder.Services.AddScoped<IProductService, ProductService>();

// ==================== BUILD AND INITIALIZE ====================
var host = builder.Build();

// ==================== INITIALIZE MID_LOGGER ====================
// CRITICAL: Initialize Mid_Logger FIRST before any service uses MID_HelperFunctions
try
{
    var midLogger = host.Services.GetRequiredService<IMid_Logger>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
    
    // Initialize Mid_Logger with ILogger and JSRuntime
    midLogger.Initialize(logger, jsRuntime);
    
    // Initialize MID_HelperFunctions with the logger
    MID_HelperFunctions.Initialize(midLogger);
    
    logger.LogInformation("Mid_Logger and MID_HelperFunctions initialized successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to initialize logging: {ex.Message}");
}

// ==================== INITIALIZE FIREBASE ====================
try
{
    var firebaseConfig = host.Services.GetRequiredService<IFirebaseConfigService>();
    await firebaseConfig.InitializeAsync();
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to initialize Firebase");
}

// ==================== RUN APPLICATION ====================
await host.RunAsync();
