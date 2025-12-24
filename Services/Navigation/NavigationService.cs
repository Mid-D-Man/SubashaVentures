using Microsoft.AspNetCore.Components;

namespace SubashaVentures.Services.Navigation;

public class NavigationService : INavigationService
{
    private readonly NavigationManager _navigationManager;
    private bool _isSidePanelOpen;
    
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
    
    public event EventHandler<bool>? SidePanelStateChanged;
    public event EventHandler? FilterPanelToggleRequested;
    public event EventHandler? FiltersChanged;
    
    public void OpenSidePanel() => IsSidePanelOpen = true;
    public void CloseSidePanel() => IsSidePanelOpen = false;
    public void ToggleSidePanel() => IsSidePanelOpen = !IsSidePanelOpen;
    public void SetSidePanelState(bool isOpen) => IsSidePanelOpen = isOpen;
    public void ToggleFilterPanel() => FilterPanelToggleRequested?.Invoke(this, EventArgs.Empty);
    public void NotifyFiltersChanged() => FiltersChanged?.Invoke(this, EventArgs.Empty);
    
    public void NavigateTo(string uri)
    {
        _navigationManager.NavigateTo(uri);
    }
    
    public void NavigateTo(string uri, bool forceLoad)
    {
        _navigationManager.NavigateTo(uri, forceLoad);
    }
}
