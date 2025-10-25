using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;

namespace SubashaVentures.Layout.Admin;

public partial class AdminNavMenu : ComponentBase
{
    private ElementReference navElement;
    private bool isExpanded = false;
    private bool isPinned = false;
    private bool isMobileOpen = false;
    private HashSet<string> expandedGroups = new();
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<AdminNavMenu>? dotNetRef;

    private List<NavigationSection> NavigationSections = new()
    {
        new NavigationSection
        {
            Title = "Main",
            Items = new List<NavigationItem>
            {
                new() { Id = "dashboard", Label = "Dashboard", Path = "/admin", Icon = "dashboard" },
                new() { Id = "analytics", Label = "Analytics", Path = "/admin/analytics", Icon = "analytics" }
            }
        },
        new NavigationSection
        {
            Title = "Management",
            Items = new List<NavigationItem>
            {
                new()
                {
                    Id = "products",
                    Label = "Products",
                    Path = "/admin/products",
                    Icon = "products",
                    Children = new List<NavigationItem>
                    {
                        new() { Id = "products-all", Label = "All Products", Path = "/admin/products", Icon = "list" },
                        new() { Id = "products-add", Label = "Add Product", Path = "/admin/products/add", Icon = "add" },
                        new() { Id = "products-categories", Label = "Categories", Path = "/admin/products/categories", Icon = "category" }
                    }
                },
                new()
                {
                    Id = "orders",
                    Label = "Orders",
                    Path = "/admin/orders",
                    Icon = "orders",
                    Badge = 12,
                    Children = new List<NavigationItem>
                    {
                        new() { Id = "orders-all", Label = "All Orders", Path = "/admin/orders", Icon = "list" },
                        new() { Id = "orders-pending", Label = "Pending", Path = "/admin/orders/pending", Icon = "pending", Badge = 5 },
                        new() { Id = "orders-completed", Label = "Completed", Path = "/admin/orders/completed", Icon = "check" }
                    }
                },
                new() { Id = "customers", Label = "Customers", Path = "/admin/customers", Icon = "customers" },
                new() { Id = "reviews", Label = "Reviews", Path = "/admin/reviews", Icon = "reviews", Badge = 3 }
            }
        },
        new NavigationSection
        {
            Title = "Marketing",
            Items = new List<NavigationItem>
            {
                new() { Id = "promotions", Label = "Promotions", Path = "/admin/promotions", Icon = "promotions" },
                new() { Id = "emails", Label = "Email Campaigns", Path = "/admin/emails", Icon = "email" }
            }
        },
        new NavigationSection
        {
            Title = "System",
            Items = new List<NavigationItem>
            {
                new() { Id = "users", Label = "Users & Roles", Path = "/admin/users", Icon = "users" },
                new() { Id = "logs", Label = "Activity Logs", Path = "/admin/logs", Icon = "logs" }
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
                
                MID_HelperFunctions.DebugMessage("AdminNavMenu initialized", DebugClass.Info);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage(
                    $"Failed to initialize AdminNavMenu: {ex.Message}", 
                    DebugClass.Error);
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
        isPinned = !isPinned;
        isExpanded = isPinned;
        StateHasChanged();
    }

    [JSInvokable]
    public void OpenMobileNav()
    {
        isMobileOpen = true;
        isExpanded = true;
        StateHasChanged();
    }

    private void CloseMobileNav()
    {
        isMobileOpen = false;
        isExpanded = false;
        StateHasChanged();
    }

    private void ToggleGroup(string groupId)
    {
        if (expandedGroups.Contains(groupId))
        {
            expandedGroups.Remove(groupId);
        }
        else
        {
            expandedGroups.Add(groupId);
        }
        StateHasChanged();
    }

    private void HandleNavigation(string path)
    {
        if (isMobileOpen)
        {
            CloseMobileNav();
        }
        NavigationManager.NavigateTo(path);
    }

    private bool IsCurrentPath(string path)
    {
        var currentPath = NavigationManager.Uri.Replace(NavigationManager.BaseUri, "/");
        return currentPath.StartsWith(path, StringComparison.OrdinalIgnoreCase);
    }

    private RenderFragment GetIconSvg(string iconName) => builder =>
    {
        var svg = iconName switch
        {
            "dashboard" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""2"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""11"" y=""2"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""2"" y=""11"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""11"" y=""11"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",
            "analytics" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M3 17V10M10 17V3M17 17V7"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",
            "products" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""2"" width=""16"" height=""16"" rx=""2"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M2 7H18M7 2V7M13 2V7"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",
            "orders" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M3 3H17L15 13H5L3 3Z"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/><circle cx=""7"" cy=""17"" r=""1"" fill=""currentColor""/><circle cx=""13"" cy=""17"" r=""1"" fill=""currentColor""/></svg>",
            "customers" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><circle cx=""10"" cy=""7"" r=""3"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M4 17C4 14 6.5 12 10 12C13.5 12 16 14 16 17"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",
            "reviews" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M10 2L12 8L18 8L13 12L15 18L10 14L5 18L7 12L2 8L8 8L10 2Z"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg>",
            "promotions" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M3 10L10 3L17 10L10 17L3 10Z"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/><circle cx=""10"" cy=""10"" r=""2"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",
            "email" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""4"" width=""16"" height=""12"" rx=""2"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M2 6L10 11L18 6"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg>",
            "users" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><circle cx=""7"" cy=""6"" r=""2.5"" stroke=""currentColor"" stroke-width=""1.5""/><circle cx=""13"" cy=""6"" r=""2.5"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M2 16C2 13.5 4 12 7 12C10 12 12 13.5 12 16M8 16C8 13.5 10 12 13 12C16 12 18 13.5 18 16"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",
            "logs" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M3 6H17M3 10H17M3 14H17"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",
            "list" => @"<svg width=""16"" height=""16"" viewBox=""0 0 16 16"" fill=""none""><path d=""M5 4H13M5 8H13M5 12H13"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/><circle cx=""2.5"" cy=""4"" r=""0.5"" fill=""currentColor""/><circle cx=""2.5"" cy=""8"" r=""0.5"" fill=""currentColor""/><circle cx=""2.5"" cy=""12"" r=""0.5"" fill=""currentColor""/></svg>",
            "add" => @"<svg width=""16"" height=""16"" viewBox=""0 0 16 16"" fill=""none""><path d=""M8 3V13M3 8H13"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",
            "category" => @"<svg width=""16"" height=""16"" viewBox=""0 0 16 16"" fill=""none""><rect x=""2"" y=""2"" width=""5"" height=""5"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""9"" y=""2"" width=""5"" height=""5"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""2"" y=""9"" width=""5"" height=""5"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""9"" y=""9"" width=""5"" height=""5"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",
            "pending" => @"<svg width=""16"" height=""16"" viewBox=""0 0 16 16"" fill=""none""><circle cx=""8"" cy=""8"" r=""6"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M8 4V8L11 11"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",
            "check" => @"<svg width=""16"" height=""16"" viewBox=""0 0 16 16"" fill=""none""><path d=""M3 8L6 11L13 4"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg>",
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
                    $"Error disposing AdminNavMenu: {ex.Message}", 
                    DebugClass.Warning);
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
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Path { get; set; } = "";
        public string Icon { get; set; } = "";
        public int Badge { get; set; }
        public List<NavigationItem>? Children { get; set; }
    }
}