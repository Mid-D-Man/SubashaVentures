// Program.cs - FIXED DI REGISTRATION
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
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Services.Auth;
using SubashaVentures.Services.Statistics;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.Authorization;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
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

// ✅ CRITICAL FIX: Register Supabase client factory BEFORE building
builder.Services.AddSingleton<Supabase.Client>(serviceProvider =>
{
    var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
    var sessionHandler = new SupabaseSessionHandler(jsRuntime);
    
    var options = new SupabaseOptions
    {
        AutoRefreshToken = true,
        AutoConnectRealtime = false,
        SessionHandler = sessionHandler
    };
    
    var client = new Supabase.Client(supabaseUrl, supabaseKey, options);
    
    // Initialize synchronously (session handler will load from localStorage)
    client.InitializeAsync().GetAwaiter().GetResult();
    
    Console.WriteLine("✓ Supabase client initialized with session handler");
    
    return client;
});

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

// ✅ Build host AFTER all services are registered
var host = builder.Build();

try
{
    var midLogger = host.Services.GetRequiredService<IMid_Logger>();
    var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
    
    midLogger.Initialize(host.Services.GetRequiredService<ILogger<IMid_Logger>>(), jsRuntime);
    MID_HelperFunctions.Initialize(midLogger);
    
    Console.WriteLine("✓ Mid_Logger initialized");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to initialize logging: {ex.Message}");
}

try
{
    var firebaseConfig = host.Services.GetRequiredService<IFirebaseConfigService>();
    await firebaseConfig.InitializeAsync();
    
    Console.WriteLine("✓ Firebase initialized");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to initialize Firebase: {ex.Message}");
}

await host.RunAsync();

// ✅ Custom session handler that reads from browser localStorage
public class SupabaseSessionHandler : IGotrueSessionPersistence<Session>
{
    private readonly IJSRuntime _jsRuntime;
    private const string SESSION_KEY = "supabase.auth.token";

    public SupabaseSessionHandler(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public void SaveSession(Session session)
    {
        try
        {
            var sessionJson = JsonConvert.SerializeObject(session);
            
            // Use InvokeVoidAsync (non-blocking) for saving
            _ = _jsRuntime.InvokeVoidAsync("eval", 
                $"try {{ localStorage.setItem('{SESSION_KEY}', {JsonConvert.SerializeObject(sessionJson)}); console.log('✓ Session saved'); }} catch(e) {{ console.error('❌ Save failed:', e); }}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to save session: {ex.Message}");
        }
    }

    public void DestroySession()
    {
        try
        {
            _ = _jsRuntime.InvokeVoidAsync("eval", 
                $"try {{ localStorage.removeItem('{SESSION_KEY}'); console.log('✓ Session destroyed'); }} catch(e) {{ console.error('❌ Destroy failed:', e); }}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to destroy session: {ex.Message}");
        }
    }

    public Session? LoadSession()
    {
        try
        {
            // Try to load session synchronously using JSInProcessRuntime
            if (_jsRuntime is IJSInProcessRuntime jsInProcess)
            {
                var sessionJson = jsInProcess.Invoke<string>("eval", 
                    $"(function() {{ try {{ var item = localStorage.getItem('{SESSION_KEY}'); return item; }} catch(e) {{ console.error('❌ Load failed:', e); return null; }} }})()");
                
                if (string.IsNullOrEmpty(sessionJson) || sessionJson == "null")
                {
                    Console.WriteLine("ℹ️ No session in localStorage");
                    return null;
                }

                var session = JsonConvert.DeserializeObject<Session>(sessionJson);
                Console.WriteLine("✓ Session loaded from localStorage");
                return session;
            }
            else
            {
                // Fallback for non-in-process runtime (shouldn't happen in Blazor WASM)
                Console.WriteLine("⚠️ JSInProcessRuntime not available, cannot load session synchronously");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to load session: {ex.Message}");
            return null;
        }
    }
}
