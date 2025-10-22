using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SubashaVentures;
using SubashaVentures.Services.Navigation;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Time;
using SubashaVentures.Services.Connectivity;
using Blazored.LocalStorage;
using Supabase;

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

// ==================== LOCAL STORAGE ====================
builder.Services.AddBlazoredLocalStorage(config =>
{
    config.JsonSerializerOptions.WriteIndented = false;
    config.JsonSerializerOptions.IgnoreNullValues = true;
});
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();

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
// Note: Supabase real-time is not supported in WASM, only REST API and Auth
var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:AnonKey"];

if (!string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
{
    builder.Services.AddScoped<Supabase.Client>(_ =>
    {
        var options = new Supabase.SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false, // Disabled for WASM
            SessionHandler = new Supabase.Gotrue.SupabaseSessionHandler()
        };
        
        return new Supabase.Client(supabaseUrl, supabaseKey, options);
    });
}

builder.Services.AddScoped<ISupabaseConfigService, SupabaseConfigService>();
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();

// ==================== BUILD AND INITIALIZE ====================
var host = builder.Build();

// Initialize Firebase configuration
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

await host.RunAsync();
