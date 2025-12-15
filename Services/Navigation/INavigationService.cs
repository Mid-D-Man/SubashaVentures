namespace SubashaVentures.Services.Navigation;

public interface INavigationService
{
    /// <summary>
    /// Gets whether the side panel is currently open
    /// </summary>
    bool IsSidePanelOpen { get; }
    
    /// <summary>
    /// Gets the current search query
    /// </summary>
    string SearchQuery { get; }
    
    /// <summary>
    /// Event raised when the side panel state changes
    /// </summary>
    event EventHandler<bool>? SidePanelStateChanged;
    
    /// <summary>
    /// Event raised when the search query changes
    /// </summary>
    event EventHandler<string>? SearchQueryChanged;
    
    /// <summary>
    /// Event raised when filter panel toggle is requested
    /// </summary>
    event EventHandler? FilterPanelToggleRequested;
    
    /// <summary>
    /// Event raised when filters have changed and products need to be reloaded
    /// </summary>
    event EventHandler? FiltersChanged;
    
    /// <summary>
    /// Opens the side panel
    /// </summary>
    void OpenSidePanel();
    
    /// <summary>
    /// Closes the side panel
    /// </summary>
    void CloseSidePanel();
    
    /// <summary>
    /// Toggles the side panel open/closed state
    /// </summary>
    void ToggleSidePanel();
    
    /// <summary>
    /// Sets the side panel state
    /// </summary>
    void SetSidePanelState(bool isOpen);
    
    /// <summary>
    /// Updates the search query
    /// </summary>
    void UpdateSearchQuery(string query);
    
    /// <summary>
    /// Clears the search query
    /// </summary>
    void ClearSearchQuery();
    
    /// <summary>
    /// Toggles the filter panel (mobile)
    /// </summary>
    void ToggleFilterPanel();
    
    /// <summary>
    /// Notifies subscribers that filters have changed
    /// </summary>
    void NotifyFiltersChanged();
    string? GetQueryParameter(string key);
}
