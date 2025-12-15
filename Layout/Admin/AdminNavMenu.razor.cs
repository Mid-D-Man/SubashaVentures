using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;

using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

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
                new() { Id = "dashboard", Label = "Dashboard", Path = "admin", Icon = "dashboard" }
            }
        },
        new NavigationSection
        {
            Title = "Management",
            Items = new List<NavigationItem>
            {
                new() { Id = "users", Label = "User Management", Path = "admin/users", Icon = "users" },
                new() { Id = "products", Label = "Product Management", Path = "admin/products", Icon = "products" },
                new() { Id = "images", Label = "Image Management", Path = "admin/images", Icon = "images" },
                new() { Id = "categories", Label = "Category Management", Path = "admin/categories", Icon = "category" },
                new() { Id = "misc", Label = "Misc Management", Path = "admin/misc", Icon = "misc" }
            }
        },
        new NavigationSection
        {
            Title = "Analytics",
            Items = new List<NavigationItem>
            {
                new() { Id = "statistics", Label = "Statistics", Path = "admin/statistics", Icon = "analytics" }
            }
        },
        new NavigationSection
        {
            Title = "Communication",
            Items = new List<NavigationItem>
            {
                new() { Id = "messages", Label = "Messages", Path = "admin/messages", Icon = "messages" }
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
                    $"Failed to initialize AdminNavMenu: {ex.Message}", 
                    LogLevel.Error);
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
        var currentPath = NavigationManager.Uri.Replace(NavigationManager.BaseUri, "");
        
        // Remove leading slash if present
        if (currentPath.StartsWith("/"))
        {
            currentPath = currentPath.Substring(1);
        }
        
        // Exact match for root admin path
        if (path == "admin" && currentPath == "admin")
        {
            return true;
        }
        
        // For other paths, check if it starts with the path (but not root)
        if (path != "admin" && currentPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        return false;
    }

    private RenderFragment GetIconSvg(string iconName) => builder =>
    {
        var svg = iconName switch
        {
            "dashboard" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""2"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""11"" y=""2"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""2"" y=""11"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""11"" y=""11"" width=""7"" height=""7"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",
            "users" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><circle cx=""7"" cy=""6"" r=""2.5"" stroke=""currentColor"" stroke-width=""1.5""/><circle cx=""13"" cy=""6"" r=""2.5"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M2 16C2 13.5 4 12 7 12C10 12 12 13.5 12 16M8 16C8 13.5 10 12 13 12C16 12 18 13.5 18 16"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",
            "products" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""2"" width=""16"" height=""16"" rx=""2"" stroke=""currentColor"" stroke-width=""1.5""/><path d=""M2 7H18M7 2V7M13 2V7"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",
            "images" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""3"" width=""16"" height=""14"" rx=""2"" stroke=""currentColor"" stroke-width=""1.5""/><circle cx=""7"" cy=""8"" r=""1.5"" fill=""currentColor""/><path d=""M2 13L6 9L10 13L14 9L18 13"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg>",
            "category" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><rect x=""2"" y=""2"" width=""5"" height=""5"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""9"" y=""2"" width=""9"" height=""5"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""2"" y=""9"" width=""9"" height=""9"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/><rect x=""13"" y=""9"" width=""5"" height=""9"" rx=""1"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",
            "misc" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><circle cx=""10"" cy=""10"" r=""1.5"" fill=""currentColor""/><circle cx=""10"" cy=""4"" r=""1.5"" fill=""currentColor""/><circle cx=""10"" cy=""16"" r=""1.5"" fill=""currentColor""/></svg>",
            "analytics" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M3 17V10M10 17V3M17 17V7"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round""/></svg>",
            "messages" => @"<svg width=""20"" height=""20"" viewBox=""0 0 20 20"" fill=""none""><path d=""M2 6L10 11L18 6"" stroke=""currentColor"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/><rect x=""2"" y=""4"" width=""16"" height=""12"" rx=""2"" stroke=""currentColor"" stroke-width=""1.5""/></svg>",
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
                    LogLevel.Warning);
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
