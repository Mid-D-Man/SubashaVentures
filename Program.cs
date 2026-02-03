

// Program.cs - UPDATED SERVICES REGISTRATION
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
using SubashaVentures.Services.Wishlist;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Authorization;
using Blazored.LocalStorage;
using Blazored.Toast;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SubashaVentures.Services.Addresses;
using SubashaVentures.Services.Statistics;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.Auth;
using SubashaVentures.Services.Geolocation;
using SubashaVentures.Services.Orders;
using SubashaVentures.Services.Partners;
using SubashaVentures.Services.Shop;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Utilities.Tracking;
using Supabase;
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

// ==================== SUPABASE CLIENT ====================
builder.Services.AddScoped<Client>(sp =>
{
    var config = builder.Configuration;
    var url = config["Supabase:Url"];
    var key = config["Supabase:AnonKey"];
    
    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
    {
        throw new InvalidOperationException(
            "Supabase configuration is missing. Please check appsettings.json");
    }
    
    Console.WriteLine($"✓ Supabase URL: {url}");
    Console.WriteLine($"✓ Supabase Key (first 20 chars): {key.Substring(0, Math.Min(20, key.Length))}...");
    
    var options = new SupabaseOptions
    {
        AutoConnectRealtime = false,
        AutoRefreshToken = true,
        SessionHandler = new DefaultSupabaseSessionHandler()
    };
  
    return new Client(url, key, options);
});

// ==================== AUTHENTICATION ====================
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("SuperiorAdminOnly", policy =>
        policy.RequireRole("superior_admin"));
    
    options.AddPolicy("AuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());
    
    options.AddPolicy("AnyRole", policy =>
        policy.RequireRole("superior_admin", "user"));
});
builder.Services.AddScoped<CustomSupabaseClaimsFactory>();
// Add SessionManager to DI container
builder.Services.AddScoped<SessionManager>();
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
builder.Services.AddScoped<SupabaseAuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// ==================== CORE SERVICES ====================
builder.Services.AddSingleton<INavigationService, NavigationService>();
builder.Services.AddScoped<ConnectivityService>();
builder.Services.AddScoped<IServerTimeService, ServerTimeService>();
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();
builder.Services.AddScoped<IImageCompressionService, ImageCompressionService>();
builder.Services.AddScoped<IImageCacheService, ImageCacheService>();
// Add this line in the CORE SERVICES section, after the other services:
builder.Services.AddScoped<IVisualElementsService, VisualElementsService>();
builder.Services.AddScoped<IFirebaseConfigService, FirebaseConfigService>();
builder.Services.AddScoped<IFirestoreService, FirestoreService>();

builder.Services.AddScoped<ISupabaseConfigService, SupabaseConfigService>();
builder.Services.AddScoped<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddScoped<ISupabaseDatabaseService, SupabaseDatabaseService>();
builder.Services.AddScoped<ISupabaseEdgeFunctionService, SupabaseEdgeFunctionService>();

// ==================== SHOP SERVICES - SIMPLIFIED ====================
builder.Services.AddScoped<SubashaVentures.Services.Shop.ShopStateService>(); // SINGLE SERVICE NOW

// ==================== PRODUCT & CATALOG SERVICES ====================
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductOfTheDayService, ProductOfTheDayService>();
builder.Services.AddScoped<IReviewService, ReviewService>();

// ==================== USER SERVICES ====================
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IPartnerService, PartnerService>();
// ==================== CART & WISHLIST ====================
builder.Services.AddScoped<SubashaVentures.Services.Cart.ICartService, SubashaVentures.Services.Cart.CartService>();
builder.Services.AddScoped<SubashaVentures.Services.Wishlist.IWishlistService, SubashaVentures.Services.Wishlist.WishlistService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IGeolocationService, GeolocationService>();
// Product Interaction Tracking Services
builder.Services.AddScoped<IProductInteractionService, ProductInteractionService>();
builder.Services.AddScoped<ProductViewTracker>();
// Add Payment Service
builder.Services.AddScoped<SubashaVentures.Services.Payment.IPaymentService, SubashaVentures.Services.Payment.PaymentService>();
builder.Services.AddScoped<SubashaVentures.Services.Payment.IWalletService, SubashaVentures.Services.Payment.WalletService>();
// Program.cs - ADD THIS LINE in services section
builder.Services.AddScoped<SubashaVentures.Services.Checkout.ICheckoutService, SubashaVentures.Services.Checkout.CheckoutService>();
var host = builder.Build();

try
{
    // Start auto-flush for product interactions
    var interactionService = host.Services.GetRequiredService<IProductInteractionService>();
    interactionService.StartAutoFlush();

    
    
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

Console.WriteLine("✓ All services initialized with proper filter management");

await host.RunAsync();
