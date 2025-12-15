using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace SubashaVentures.Services.Navigation;

public class NavigationService : INavigationService
{
    private readonly NavigationManager _navigationManager;
    private bool _isSidePanelOpen;
    private string _searchQuery = string.Empty;
    
    public NavigationService(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }
    
    public bool IsSidePanelOpen
    {
        get => _isSidePanelOpen;
        private set
        {
            if (_isSidePanelOpen != value)
            {
                _isSidePanelOpen = value;
                SidePanelStateChanged?.Invoke(this, value);
            }
        }
    }
    
    public string SearchQuery
    {
        get => _searchQuery;
        private set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                SearchQueryChanged?.Invoke(this, value);
            }
        }
    }
    
    public event EventHandler<bool>? SidePanelStateChanged;
    public event EventHandler<string>? SearchQueryChanged;
    public event EventHandler? FilterPanelToggleRequested;
    public event EventHandler? FiltersChanged;
    
    public void OpenSidePanel() => IsSidePanelOpen = true;
    public void CloseSidePanel() => IsSidePanelOpen = false;
    public void ToggleSidePanel() => IsSidePanelOpen = !IsSidePanelOpen;
    public void SetSidePanelState(bool isOpen) => IsSidePanelOpen = isOpen;
    public void UpdateSearchQuery(string query) => SearchQuery = query ?? string.Empty;
    public void ClearSearchQuery() => SearchQuery = string.Empty;
    public void ToggleFilterPanel() => FilterPanelToggleRequested?.Invoke(this, EventArgs.Empty);
    public void NotifyFiltersChanged() => FiltersChanged?.Invoke(this, EventArgs.Empty);
    
    public string? GetQueryParameter(string key)
    {
        var uri = new Uri(_navigationManager.Uri);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        return queryParams.TryGetValue(key, out var value) ? value.ToString() : null;
    }
}
