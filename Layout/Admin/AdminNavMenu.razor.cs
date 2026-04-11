using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.Partners;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Layout.Admin;

public partial class AdminNavMenu : ComponentBase
{
    [Inject] private IPartnerApplicationService PartnerApplicationService { get; set; } = default!;

    private ElementReference navElement;
    private bool isExpanded   = false;
    private bool isPinned     = false;
    private bool isMobileOpen = false;
    private int  pendingApplicationCount = 0;

    private HashSet<string> expandedGroups = new();
    private IJSObjectReference?           jsModule;
    private DotNetObjectReference<AdminNavMenu>? dotNetRef;

    private List<NavigationSection> NavigationSections => new()
    {
        new NavigationSection
        {
            Title = "Main",
            Items = new List<NavigationItem>
            {
                new() { Id = "dashboard", Label = "Dashboard", Path = "admin", Icon = "dashboard" }
            }
        },
        new NavigationSection
        {
            Title = "Management",
            Items = new List<NavigationItem>
            {
                new() { Id = "orders",       Label = "Order Management",      Path = "admin/orders",               Icon = "orders"       },
                new() { Id = "users",        Label = "User Management",       Path = "admin/users",                Icon = "users"        },
                new() { Id = "products",     Label = "Product Management",    Path = "admin/products",             Icon = "products"     },
                new() { Id = "partners",     Label = "Partner Management",    Path = "admin/partners",             Icon = "partners"     },
                new() { Id = "applications", Label = "Partner Applications",  Path = "admin/partner-applications", Icon = "applications", Badge = pendingApplicationCount },
                new() { Id = "images",       Label = "Image Management",      Path = "admin/images",               Icon = "images"       },
                new() { Id = "categories",   Label = "Category Management",   Path = "admin/categories",           Icon = "category"     },
                new() { Id = "reviews",      Label = "Review Management",     Path = "admin/reviews",              Icon = "reviews"      },
                new() { Id = "misc",         Label = "Misc Management",       Path = "admin/misc",                 Icon = "misc"         },
            }
        },
        new NavigationSection
        {
            Title = "Operations",
            Items = new List<NavigationItem>
            {
                new() { Id = "collection-scanner", Label = "Collection Scanner", Path = "admin/collection-scanner", Icon = "scanner" },
            }
        },
        new NavigationSection
        {
            Title = "Communications",
            Items = new List<NavigationItem>
            {
                new() { Id = "messages",   Label = "Messages",  Path = "admin/messages",   Icon = "messages"   },
                new() { Id = "newsletter", Label = "Newsletter", Path = "admin/newsletter", Icon = "newsletter" },
            }
        },
        new NavigationSection
        {
            Title = "Analytics",
            Items = new List<NavigationItem>
            {
                new() { Id = "statistics", Label = "Statistics", Path = "admin/statistics", Icon = "analytics" }
            }
        }
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./js/adminNavMenu.js");

                dotNetRef = DotNetObjectReference.Create(this);

                await jsModule.InvokeVoidAsync("initializeNavMenu", navElement, dotNetRef);

                MID_HelperFunctions.DebugMessage("AdminNavMenu initialized", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage(
                    $"Failed to initialize AdminNavMenu JS: {ex.Message}", LogLevel.Error);
            }

            // Load pending application badge count
            try
            {
                var stats = await PartnerApplicationService.GetApplicationStatisticsAsync();
                pendingApplicationCount = stats.Pending + stats.UnderReview;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage(
                    $"Failed to load application badge count: {ex.Message}", LogLevel.Warning);
            }
        }
    }

    private void HandleMouseEnter()
    {
        if (!isPinned && !isMobileOpen)
        {
            isExpanded = true;
        }
    }

    private void HandleMouseLeave()
    {
        if (!isPinned && !isMobileOpen)
        {
            isExpanded = false;
        }
    }

    private void TogglePinned()
    {
        isPinned   = !isPinned;
        isExpanded = isPinned;
        StateHasChanged();
    }

    [JSInvokable]
    public void OpenMobileNav()
    {
        isMobileOpen = true;
        isExpanded   = true;
        StateHasChanged();
    }

    private void CloseMobileNav()
    {
        isMobileOpen = false;
        isExpanded   = false;
        StateHasChanged();
    }

    private void ToggleGroup(string groupId)
    {
        if (expandedGroups.Contains(groupId))
            expandedGroups.Remove(groupId);
        else
            expandedGroups.Add(groupId);
        StateHasChanged();
    }

    private void HandleNavigation(string path)
    {
        if (isMobileOpen) CloseMobileNav();
        NavigationManager.NavigateTo(path);
    }

    private bool IsCurrentPath(string path)
    {
        var currentPath = NavigationManager.Uri.Replace(NavigationManager.BaseUri, "");

        if (currentPath.StartsWith("/"))
            currentPath = currentPath.Substring(1);

        if (path == "admin" && currentPath == "admin")
            return true;

        if (path != "admin" && currentPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private RenderFragment GetIconSvg(string iconName) => builder =>
    {
        var svg = iconName switch
        {
            "dashboard" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""2"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""11"" y=""2"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""2"" y=""11"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""11"" y=""11"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",

            "orders" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M10 2L2 6l8 4 8-4z"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linejoin=""round""/><path d=""M2 6v8l8 4 8-4V6"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linejoin=""round""/><line x1=""10"" y1=""10"" x2=""10"" y2=""18"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",

            "users" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><circle cx=""7"" cy=""6"" r=""2.5"" stroke=""currentColor"" stroke-width=""1.5""/><circle cx=""13"" cy=""6"" r=""2.5"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M2 16C2 13.5 4 12 7 12C10 12 12 13.5 12 16M8 16C8 13.5 10 12 13 12C16 12 18 13.5 18 16"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",

            "products" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""2"" width=""16"" height=""16"" rx=""2"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M2 7H18M7 2V7M13 2V7"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",

            "partners" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M13 7C13 8.66 11.66 10 10 10C8.34 10 7 8.66 7 7C7 5.34 8.34 4 10 4C11.66 4 13 5.34 13 7Z"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M3 18C3 15.79 4.79 14 7 14H13C15.21 14 17 15.79 17 18"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/><path d=""M17 7C17 8.1 16.1 9 15 9"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/><path d=""M3 7C3 8.1 3.9 9 5 9"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",

            "applications" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M4 2h9l4 4v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linejoin=""round""/><path d=""M13 2v4h4"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linejoin=""round""/><path d=""M7 9h6M7 12h6M7 15h4"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",

            "images" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""3"" width=""16"" height=""14"" rx=""2"" stroke=""currentColor"" stroke-width=""1.5""/><circle cx=""7"" cy=""8"" r=""1.5"" fill=""currentColor""/><path d=""M2 13L6 9L10 13L14 9L18 13"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg>",

            "category" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""2"" width=""5"" height=""5"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""9"" y=""2"" width=""9"" height=""5"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""2"" y=""9"" width=""9"" height=""9"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""13"" y=""9"" width=""5"" height=""9"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",

            "reviews" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M10 2L12.5 7L18 8L14 12L15 18L10 15L5 18L6 12L2 8L7.5 7L10 2Z"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linejoin=""round""/></svg>",

            "misc" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><circle cx=""10"" cy=""10"" r=""1.5"" fill=""currentColor""/><circle cx=""10"" cy=""4"" r=""1.5"" fill=""currentColor""/><circle cx=""10"" cy=""16"" r=""1.5"" fill=""currentColor""/></svg>",

            "scanner" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""2"" width=""6"" height=""6"" rx=""0.5"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""3.5"" y=""3.5"" width=""3"" height=""3"" rx=""0.5"" fill=""currentColor""/><rect x=""12"" y=""2"" width=""6"" height=""6"" rx=""0.5"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""13.5"" y=""3.5"" width=""3"" height=""3"" rx=""0.5"" fill=""currentColor""/><rect x=""2"" y=""12"" width=""6"" height=""6"" rx=""0.5"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""3.5"" y=""13.5"" width=""3"" height=""3"" rx=""0.5"" fill=""currentColor""/><rect x=""12"" y=""12"" width=""2"" height=""2"" rx=""0.5"" fill=""currentColor""/><rect x=""14"" y=""14"" width=""2"" height=""2"" rx=""0.5"" fill=""currentColor""/><rect x=""12"" y=""16"" width=""2"" height=""2"" rx=""0.5"" fill=""currentColor""/><rect x=""16"" y=""12"" width=""2"" height=""2"" rx=""0.5"" fill=""currentColor""/><rect x=""16"" y=""16"" width=""2"" height=""2"" rx=""0.5"" fill=""currentColor""/></svg>",

            "messages" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M17 3H3a1 1 0 0 0-1 1v9a1 1 0 0 0 1 1h9l4 3V4a1 1 0 0 0-1-1z"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linejoin=""round""/><line x1=""6"" y1=""8"" x2=""14"" y2=""8"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/><line x1=""6"" y1=""11"" x2=""11"" y2=""11"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",

            "newsletter" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""4"" width=""16"" height=""12"" rx=""2"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M2 7l8 5 8-5"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg>",

            "analytics" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M3 17V10M10 17V3M17 17V7"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",

            _ => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><circle cx=""10"" cy=""10"" r=""8"" stroke=""currentColor"" stroke-width=""1.5""/></svg>"
        };
        builder.AddMarkupContent(0, svg);
    };

    public async ValueTask DisposeAsync()
    {
        if (jsModule != null)
        {
            try
            {
                await jsModule.InvokeVoidAsync("dispose");
                await jsModule.DisposeAsync();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage(
                    $"Error disposing AdminNavMenu: {ex.Message}", LogLevel.Warning);
            }
        }

        dotNetRef?.Dispose();
    }

    public class NavigationSection
    {
        public string Title { get; set; } = "";
        public List<NavigationItem> Items { get; set; } = new();
    }

    public class NavigationItem
    {
        public string Id    { get; set; } = "";
        public string Label { get; set; } = "";
        public string Path  { get; set; } = "";
        public string Icon  { get; set; } = "";
        public int    Badge { get; set; }
        public List<NavigationItem>? Children { get; set; }
    }
}
