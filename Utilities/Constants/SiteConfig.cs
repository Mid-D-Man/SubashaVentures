// Utilities/Constants/SiteConfig.cs
// Single source of truth for all site-wide configurable strings.
// Edit here — nowhere else.
namespace SubashaVentures.Utilities.Constants;

public static class SiteConfig
{
    // ── Brand ────────────────────────────────────────────────────────────────
    public const string BrandName    = "SubashaVentures";
    public const string BrandShort   = "Subasha";
    public const string BrandTagline = "Style That Speaks Your Language";
    public const int    FoundedYear  = 2022;

    // ── Contact ──────────────────────────────────────────────────────────────
    public const string ContactPhone   = "+234 81 999 999 99";
    public const string ContactEmail   = "subashaventures@gmail.com";
    public const string ContactAddress = "Lagos, Nigeria";

    // ── Social media URLs ────────────────────────────────────────────────────
    public const string SocialFacebook  = "#";
    public const string SocialInstagram = "#";
    public const string SocialTwitter   = "#";
    public const string SocialLinkedIn  = "#";

    // ── Legal page routes ────────────────────────────────────────────────────
    public const string LegalPrivacyUrl  = "privacy";
    public const string LegalTermsUrl    = "terms";
    public const string LegalCookiesUrl  = "cookies";

    // ── Nav routes ───────────────────────────────────────────────────────────
    public const string RouteHome      = "";
    public const string RouteShop      = "shop";
    public const string RouteOurStory  = "our-story";
    public const string RouteContact   = "contact";
    public const string RouteHelp      = "help";
    public const string RouteShipping  = "shipping";
    public const string RouteReturns   = "returns";
    public const string RouteSizeGuide = "size-guide";

    // ── Our Story page content ────────────────────────────────────────────────
    // Change the copy here without touching markup.
    public const string StoryHeroHeadline   = "We Didn't Build a Store.";
    public const string StoryHeroAccent     = "We Built a Statement.";
    public const string StoryHeroSub        = "Subasha was born from a simple Nigerian truth: you shouldn't have to choose between quality and affordability.";

    public const string StoryOriginLabel    = "The Beginning";
    public const string StoryOriginHeading  = "Lagos, 2022.";
    public const string StoryOriginPara1    =
        "It started in a Lagos apartment with two people, a shared frustration, and an unshakeable " +
        "conviction — Nigerian shoppers deserved better. Not the watered-down \"affordable\" that meant " +
        "flimsy fabrics and stitching that unravelled after three washes. Real quality. Real style.";
    public const string StoryOriginPara2    =
        "We started by hand-picking every single item, testing each piece ourselves before it ever " +
        "reached a customer. No shortcuts. No compromises. If we wouldn't wear it or put it in our " +
        "own home, it didn't make the cut. That standard hasn't changed — it's just scaled.";

    public const string StoryMissionQuote   =
        "To make every Nigerian feel like they can dress their best, live their best, " +
        "and still have money left for jollof.";

    public const string StoryCtaHeading     = "Experience It Yourself";
    public const string StoryCtaSub         = "Browse our collection and discover quality that speaks for itself.";
}
