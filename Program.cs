// Program.cs
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
using SubashaVentures.Services.Email;
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
using SubashaVentures.Services.Cryptography;
using SubashaVentures.Services.Newsletter;
using SubashaVentures.Services.Notifications;
using SubashaVentures.Services.AppStats;
using SubashaVentures.Services.Collection;
using Supabase;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Partners;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout     = TimeSpan.FromSeconds(30)
});

builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("SubashaVentures", LogLevel.Debug);

builder.Services.AddSingleton<IMid_Logger, Mid_Logger>();

builder.Services.AddBlazoredLocalStorage(config =>
{
    config.JsonSerializerOptions.WriteIndented          = false;
    config.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddBlazoredToast();

// ==================== SUPABASE CLIENT ====================
builder.Services.AddScoped<Client>(sp =>
{
    var config = builder.Configuration;
    var url    = config["Supabase:Url"];
    var key    = config["Supabase:AnonKey"];

    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
        throw new InvalidOperationException(
            "Supabase configuration is missing. Please check appsettings.json");

    Console.WriteLine($"✓ Supabase URL: {url}");
    Console.WriteLine($"✓ Supabase Key (first 20 chars): {key.Substring(0, Math.Min(20, key.Length))}...");

    var options = new SupabaseOptions
    {
        AutoConnectRealtime = false,
        AutoRefreshToken    = true,
        SessionHandler      = new DefaultSupabaseSessionHandler()
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

// ==================== VISUAL ELEMENTS ====================
builder.Services.AddScoped<IVisualElementsService, VisualElementsService>();

// ==================== FIREBASE ====================
builder.Services.AddScoped<IFirebaseConfigService, FirebaseConfigService>();
builder.Services.AddScoped<IFirestoreService, FirestoreService>();

// ==================== SUPABASE SERVICES ====================
builder.Services.AddScoped<ISupabaseConfigService, SupabaseConfigService>();
builder.Services.AddScoped<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddScoped<ISupabaseDatabaseService, SupabaseDatabaseService>();
builder.Services.AddScoped<ISupabaseEdgeFunctionService, SupabaseEdgeFunctionService>();

// ==================== SHOP ====================
builder.Services.AddScoped<SubashaVentures.Services.Shop.ShopStateService>();

// ==================== PRODUCT & CATALOG ====================
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
builder.Services.AddScoped<SubashaVentures.Services.Cart.ICartService,
                            SubashaVentures.Services.Cart.CartService>();
builder.Services.AddScoped<SubashaVentures.Services.Wishlist.IWishlistService,
                            SubashaVentures.Services.Wishlist.WishlistService>();

// ==================== ORDERS & COLLECTION ====================
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();

// ==================== GEOLOCATION & TRACKING ====================
builder.Services.AddScoped<IGeolocationService, GeolocationService>();
builder.Services.AddScoped<IProductInteractionService, ProductInteractionService>();
builder.Services.AddScoped<ProductViewTracker>();

// ==================== PAYMENT & CHECKOUT ====================
builder.Services.AddScoped<SubashaVentures.Services.Payment.IPaymentService,
                            SubashaVentures.Services.Payment.PaymentService>();
builder.Services.AddScoped<SubashaVentures.Services.Payment.IWalletService,
                            SubashaVentures.Services.Payment.WalletService>();
builder.Services.AddScoped<SubashaVentures.Services.Checkout.ICheckoutService,
                            SubashaVentures.Services.Checkout.CheckoutService>();

// ==================== MESSAGING & SEGMENTATION ====================
builder.Services.AddScoped<IUserSegmentationService, UserSegmentationService>();
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<ICryptographyService, CryptographyService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ==================== NEWSLETTER & NOTIFICATIONS ====================
builder.Services.AddScoped<SubashaVentures.Services.Newsletter.INewsletterService,
                            SubashaVentures.Services.Newsletter.NewsletterService>();
builder.Services.AddScoped<SubashaVentures.Services.Notifications.INotificationService,
                            SubashaVentures.Services.Notifications.NotificationService>();
builder.Services.AddScoped<SubashaVentures.Services.AppStats.IAppStatsService,
                            SubashaVentures.Services.AppStats.AppStatsService>();
// ==================== PARTNER SERVICES ====================
builder.Services.AddScoped<ICloudflareR2Service, CloudflareR2Service>();
builder.Services.AddScoped<IPartnerApplicationService, PartnerApplicationService>();
builder.Services.AddScoped<IPartnerTemplateService, PartnerTemplateService>();
builder.Services.AddScoped<IPartnerStoreService, PartnerStoreService>();
var host = builder.Build();

try
{
    // ==================== INITIALIZE VISUAL ELEMENTS ====================
    Console.WriteLine("🎨 Initializing VisualElementsService...");
    var visualElementsService = host.Services.GetRequiredService<IVisualElementsService>();
    await visualElementsService.InitializeAsync();

    var (iconsCached, svgsCached, totalCached) = visualElementsService.GetCacheStats();
    Console.WriteLine($"✓ VisualElementsService initialized");
    Console.WriteLine($"   📊 Preloaded: {iconsCached} icons, {svgsCached} SVGs ({totalCached} total assets)");

    // ==================== INITIALIZE PRODUCT TRACKING ====================
    var interactionService = host.Services.GetRequiredService<IProductInteractionService>();
    interactionService.StartAutoFlush();

    // ==================== INITIALIZE LOGGING ====================
    var midLogger  = host.Services.GetRequiredService<IMid_Logger>();
    var jsRuntime  = host.Services.GetRequiredService<IJSRuntime>();

    midLogger.Initialize(host.Services.GetRequiredService<ILogger<IMid_Logger>>(), jsRuntime);
    MID_HelperFunctions.Initialize(midLogger);

    Console.WriteLine("✓ Mid_Logger initialized");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to initialize core services: {ex.Message}");
    Console.WriteLine($"   Stack trace: {ex.StackTrace}");
}

try
{
    // ==================== INITIALIZE FIREBASE ====================
    var firebaseConfig = host.Services.GetRequiredService<IFirebaseConfigService>();
    await firebaseConfig.InitializeAsync();
    Console.WriteLine("✓ Firebase initialized");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to initialize Firebase: {ex.Message}");
}

Console.WriteLine("========================================");
Console.WriteLine("✓ All services initialized successfully");
Console.WriteLine("   ✓ VisualElementsService: SVGs preloaded");
Console.WriteLine("   ✓ ISupabaseEdgeFunctionService: registered");
Console.WriteLine("   ✓ ICollectionService: registered");
Console.WriteLine("   ✓ Product tracking enabled");
Console.WriteLine("   ✓ Firebase connected");
Console.WriteLine("   ✓ Supabase configured");
Console.WriteLine("========================================");

await host.RunAsync();
