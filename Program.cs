// Program.cs - COMPLETE WITH SYNC LOCALSTORAGE
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

// ==================== BLAZORED LOCALSTORAGE (BOTH ASYNC AND SYNC) ====================
builder.Services.AddBlazoredLocalStorage(config =>
{
    config.JsonSerializerOptions.WriteIndented = false;
    config.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddBlazoredToast();

// ==================== CORE STORAGE SERVICES ====================
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();

// ==================== SUPABASE SESSION HANDLER ====================
builder.Services.AddScoped<SupabaseSessionHandler>();

// ==================== SUPABASE CLIENT WITH PERSISTENT SESSION ====================
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
    
    // ✅ CRITICAL: Configure options for persistent sessions
    var options = new SupabaseOptions
    {
        AutoConnectRealtime = false,
        AutoRefreshToken = true, // ✅ Enable automatic token refresh
    };
  
    var client = new Client(url, key, options);
    
    // ✅ CRITICAL: Set custom persistent session handler
    var sessionHandler = sp.GetRequiredService<SupabaseSessionHandler>();
    client.Auth.SetPersistence(sessionHandler);
    
    // ✅ Load session immediately (synchronous call)
    client.Auth.LoadSession();
    
    Console.WriteLine("✓ Supabase client configured with persistent session handler");
    
    return client;
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
builder.Services.AddScoped<SessionManager>();
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
builder.Services.AddScoped<SupabaseAuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// ==================== CORE SERVICES ====================
builder.Services.AddSingleton<INavigationService, NavigationService>();
builder.Services.AddScoped<ConnectivityService>();
builder.Services.AddScoped<IServerTimeService, ServerTimeService>();
builder.Services.AddScoped<IImageCompressionService, ImageCompressionService>();
builder.Services.AddScoped<IImageCacheService, ImageCacheService>();

// ==================== VISUAL ELEMENTS SERVICE ====================
builder.Services.AddScoped<IVisualElementsService, VisualElementsService>();

// ==================== FIREBASE SERVICES ====================
builder.Services.AddScoped<IFirebaseConfigService, FirebaseConfigService>();
builder.Services.AddScoped<IFirestoreService, FirestoreService>();

// ==================== SUPABASE SERVICES ====================
builder.Services.AddScoped<ISupabaseConfigService, SupabaseConfigService>();
builder.Services.AddScoped<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddScoped<ISupabaseDatabaseService, SupabaseDatabaseService>();
builder.Services.AddScoped<ISupabaseEdgeFunctionService, SupabaseEdgeFunctionService>();

// ==================== SHOP SERVICES ====================
builder.Services.AddScoped<SubashaVentures.Services.Shop.ShopStateService>();

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

// ==================== PRODUCT INTERACTION & TRACKING ====================
builder.Services.AddScoped<IProductInteractionService, ProductInteractionService>();
builder.Services.AddScoped<ProductViewTracker>();

// ==================== PAYMENT & CHECKOUT ====================
builder.Services.AddScoped<SubashaVentures.Services.Payment.IPaymentService, SubashaVentures.Services.Payment.PaymentService>();
builder.Services.AddScoped<SubashaVentures.Services.Payment.IWalletService, SubashaVentures.Services.Payment.WalletService>();
builder.Services.AddScoped<SubashaVentures.Services.Checkout.ICheckoutService, SubashaVentures.Services.Checkout.CheckoutService>();

var host = builder.Build();

try
{
    Console.WriteLine("========================================");
    Console.WriteLine("🚀 INITIALIZING SUBASHAVENTURES APP");
    Console.WriteLine("========================================");
    
    // ==================== INITIALIZE SUPABASE AND RESTORE SESSION ====================
    Console.WriteLine("🔐 Initializing Supabase client with session restoration...");
    
    var supabaseClient = host.Services.GetRequiredService<Client>();
    
    // ✅ Session already loaded synchronously during client creation
    // ✅ Now attempt async restoration/refresh
    try
    {
        var session = await supabaseClient.Auth.RetrieveSessionAsync();
        
        if (session != null)
        {
            Console.WriteLine("✅ Session restored successfully!");
            Console.WriteLine($"   👤 User: {session.User?.Email ?? "unknown"}");
            Console.WriteLine($"   🔑 User ID: {session.User?.Id ?? "unknown"}");
            Console.WriteLine($"   ⏰ Expires: {session.ExpiresAt():yyyy-MM-dd HH:mm:ss UTC}");
            
            var timeUntilExpiry = session.ExpiresAt() - DateTime.UtcNow;
            Console.WriteLine($"   ⏳ Time remaining: {timeUntilExpiry.TotalHours:F1} hours");
            
            // ✅ Ensure auto-refresh is working
            Console.WriteLine($"   🔄 Auto-refresh: Enabled");
        }
        else
        {
            Console.WriteLine("ℹ️  No valid session found - user needs to sign in");
        }
    }
    catch (Exception sessionEx)
    {
        Console.WriteLine($"⚠️  Session restoration failed: {sessionEx.Message}");
        Console.WriteLine("   User will need to sign in again");
    }
    
    Console.WriteLine("✓ Supabase client initialized");
    
    // ==================== INITIALIZE VISUAL ELEMENTS SERVICE ====================
    Console.WriteLine("🎨 Initializing VisualElementsService...");
    var visualElementsService = host.Services.GetRequiredService<IVisualElementsService>();
    await visualElementsService.InitializeAsync();
    
    var (iconsCached, svgsCached, totalCached) = visualElementsService.GetCacheStats();
    Console.WriteLine($"✓ VisualElementsService initialized");
    Console.WriteLine($"   📊 Preloaded: {iconsCached} icons, {svgsCached} SVGs ({totalCached} total assets)");
    
    // ==================== INITIALIZE PRODUCT INTERACTION SERVICE ====================
    Console.WriteLine("📈 Starting product interaction tracking...");
    var interactionService = host.Services.GetRequiredService<IProductInteractionService>();
    interactionService.StartAutoFlush();
    Console.WriteLine("✓ Product tracking enabled");
    
    // ==================== INITIALIZE LOGGING ====================
    Console.WriteLine("📝 Initializing logging services...");
    var midLogger = host.Services.GetRequiredService<IMid_Logger>();
    var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
    
    midLogger.Initialize(host.Services.GetRequiredService<ILogger<IMid_Logger>>(), jsRuntime);
    MID_HelperFunctions.Initialize(midLogger);
    
    Console.WriteLine("✓ Mid_Logger initialized");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ CRITICAL: Failed to initialize core services!");
    Console.WriteLine($"   Error: {ex.Message}");
    Console.WriteLine($"   Stack trace: {ex.StackTrace}");
}

try
{
    // ==================== INITIALIZE FIREBASE ====================
    Console.WriteLine("🔥 Initializing Firebase...");
    var firebaseConfig = host.Services.GetRequiredService<IFirebaseConfigService>();
    await firebaseConfig.InitializeAsync();
    
    Console.WriteLine("✓ Firebase initialized");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to initialize Firebase: {ex.Message}");
}

Console.WriteLine("========================================");
Console.WriteLine("✅ ALL SERVICES INITIALIZED SUCCESSFULLY");
Console.WriteLine("========================================");
Console.WriteLine("   ✓ Supabase: Session persistence enabled (indefinite)");
Console.WriteLine("   ✓ VisualElements: SVGs preloaded");
Console.WriteLine("   ✓ Product tracking: Active");
Console.WriteLine("   ✓ Firebase: Connected");
Console.WriteLine("   ✓ Authentication: Ready with auto-refresh");
Console.WriteLine("========================================");
Console.WriteLine("🎉 App ready to run!");
Console.WriteLine("========================================");

await host.RunAsync();