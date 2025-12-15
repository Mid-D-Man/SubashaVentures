
// Program.cs - SECURITY FIXED
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
using SubashaVentures.Services.Statistics;
using SubashaVentures.Services.Users;
using Supabase;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ==================== HTTP CLIENT ====================
builder.Services.AddScoped(sp => new HttpClient 
{  
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromSeconds(30)
});

// ==================== LOGGING ====================
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("SubashaVentures", LogLevel.Debug);

builder.Services.AddSingleton<IMid_Logger, Mid_Logger>();

// ==================== BLAZORED SERVICES ====================
builder.Services.AddBlazoredLocalStorage(config =>
{
    config.JsonSerializerOptions.WriteIndented = false;
    config.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddBlazoredToast();

// ==================== AUTHENTICATION & AUTHORIZATION ====================
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();

// ==================== CORE SERVICES ====================
builder.Services.AddSingleton<INavigationService, NavigationService>();
builder.Services.AddScoped<ConnectivityService>();
builder.Services.AddScoped<IServerTimeService, ServerTimeService>();
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();
builder.Services.AddScoped<IImageCompressionService, ImageCompressionService>();
builder.Services.AddScoped<IImageCacheService, ImageCacheService>();

// ==================== FIREBASE SERVICES ====================
builder.Services.AddScoped<IFirebaseConfigService, FirebaseConfigService>();
builder.Services.AddScoped<IFirestoreService, FirestoreService>();

// ==================== SUPABASE CLIENT ====================
var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:AnonKey"];

if (!string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
{
    builder.Services.AddScoped<Supabase.Client>(sp =>
    {
        var options = new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false,
        };
        
        return new Supabase.Client(supabaseUrl, supabaseKey, options);
    });
}
else
{
    throw new InvalidOperationException("Supabase URL and AnonKey must be configured");
}

// ==================== SUPABASE SERVICES ====================
builder.Services.AddScoped<ISupabaseConfigService, SupabaseConfigService>();
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
builder.Services.AddScoped<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddScoped<ISupabaseDatabaseService, SupabaseDatabaseService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
// ==================== APPLICATION SERVICES ====================
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductOfTheDayService, ProductOfTheDayService>();
builder.Services.AddScoped<IBrandService, BrandService>();
// REMOVED: Admin setup service - SECURITY RISK

// ==================== BUILD AND INITIALIZE ====================
var host = builder.Build();

// ==================== INITIALIZE MID_LOGGER ====================
try
{
    var midLogger = host.Services.GetRequiredService<IMid_Logger>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
    
    midLogger.Initialize(logger, jsRuntime);
    MID_HelperFunctions.Initialize(midLogger);
    
    logger.LogInformation("✓ Mid_Logger initialized");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to initialize logging: {ex.Message}");
}

// ==================== INITIALIZE FIREBASE ====================
try
{
    var firebaseConfig = host.Services.GetRequiredService<IFirebaseConfigService>();
    await firebaseConfig.InitializeAsync();
    
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("✓ Firebase initialized");
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "❌ Failed to initialize Firebase");
}

// ==================== INITIALIZE SUPABASE CLIENT ====================
try
{
    var supabaseClient = host.Services.GetRequiredService<Supabase.Client>();
    await supabaseClient.InitializeAsync();
    
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("✓ Supabase client initialized (Realtime disabled for WASM)");
}
catch (Supabase.Realtime.Exceptions.RealtimeException ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "⚠ Realtime features disabled (expected in WebAssembly)");
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "❌ Failed to initialize Supabase client");
}

// ==================== RUN APPLICATION ====================
await host.RunAsync();
