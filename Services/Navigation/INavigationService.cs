namespace SubashaVentures.Services.Navigation;

public interface INavigationService
{
    bool IsSidePanelOpen { get; }
    
    event EventHandler<bool>? SidePanelStateChanged;
    event EventHandler? FilterPanelToggleRequested;
    event EventHandler? FiltersChanged;
    
    void OpenSidePanel();
    void CloseSidePanel();
    void ToggleSidePanel();
    void SetSidePanelState(bool isOpen);
    void ToggleFilterPanel();
    void NotifyFiltersChanged();
    void NavigateTo(string uri);
    void NavigateTo(string uri, bool forceLoad);
}
