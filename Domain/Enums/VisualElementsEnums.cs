namespace SubashaVentures.Domain.Enums;

/// <summary>
/// Types of icons available in the application
/// </summary>
public enum IconType
{
    SBV_ICON = 1
}

/// <summary>
/// Icon sizes matching manifest.json entries
/// </summary>
public enum IconSize
{
    Size_72 = 72,
    Size_96 = 96,
    Size_128 = 128,
    Size_144 = 144,
    Size_152 = 152,
    Size_192 = 192,
    Size_384 = 384,
    Size_512 = 512,
    Size_1024 = 1024
}

/// <summary>
/// All available SVG types in the application
/// Each enum corresponds to a file in wwwroot/svgs/
/// </summary>
public enum SvgType
{
    None = 0,
    
    // General UI
    About = 1,
    Close = 2,
    Search = 3,
    Settings = 4,
    Warning = 5,
    CheckMark = 6,
    Star = 7,
    ThumbsUp = 8,
    Flame = 9,
    
    // Navigation & Home
    Home = 10,
    Home2 = 11,
    
    // User Related
    User = 20,
    AddUser = 21,
    RemoveUser = 22,
    AdminUser = 23,
    
    // Shopping & Products
    Cart = 30,
    Wishlist = 31,
    AllProducts = 32,
    ShopNow = 33,
    Order = 34,
    TrackOrders = 35,
    
    // Product Categories
    Beddings = 40,
    ChildrenClothes = 41,
    Dress = 42,
    Tuxido = 43,
    
    // Communication
    Mail = 50,
    Messages = 51,
    Contact = 52,
    ContactUs = 53,
    HelpCenter = 54,
    
    // Account & Profile
    Address = 60,
    Payment = 61,
    History = 62,
    History2 = 63,
    Records = 64,
    Notification = 65,
    
    // Business & Admin
    Stats = 70,
    Reports = 71,
    Offer = 72,
    Story = 73,
    
    // Brand
    Sv = 80,
    
    // Status & Connection
    NoConnection = 90,
    
    // Love & Favorites
    Heart = 100
}

/// <summary>
/// SVG manipulation options for color
/// </summary>
public enum SvgColorMode
{
    /// <summary>
    /// Use original SVG colors
    /// </summary>
    Original = 0,
    
    /// <summary>
    /// Replace all fill colors with specified color
    /// </summary>
    SingleColor = 1,
    
    /// <summary>
    /// Use currentColor (inherits from CSS)
    /// </summary>
    CurrentColor = 2,
    
    /// <summary>
    /// Custom color replacement map
    /// </summary>
    CustomMap = 3
}

/// <summary>
/// SVG size presets
/// </summary>
public enum SvgSize
{
    /// <summary>
    /// Extra small - 16x16
    /// </summary>
    ExtraSmall = 16,
    
    /// <summary>
    /// Small - 24x24
    /// </summary>
    Small = 24,
    
    /// <summary>
    /// Medium - 32x32
    /// </summary>
    Medium = 32,
    
    /// <summary>
    /// Large - 48x48
    /// </summary>
    Large = 48,
    
    /// <summary>
    /// Extra large - 64x64
    /// </summary>
    ExtraLarge = 64,
    
    /// <summary>
    /// Custom size (use width/height params)
    /// </summary>
    Custom = 0
}
