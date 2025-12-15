namespace SubashaVentures.Services.Navigation;

/// <summary>
/// Implementation of navigation service for managing side panel, filter panel, search state, and filters
/// </summary>
public class NavigationService : INavigationService
{
    private bool _isSidePanelOpen;
    private string _searchQuery = string.Empty;
    
    /// <inheritdoc/>
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
    
    /// <inheritdoc/>
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
    
    /// <inheritdoc/>
    public event EventHandler<bool>? SidePanelStateChanged;
    
    /// <inheritdoc/>
    public event EventHandler<string>? SearchQueryChanged;
    
    /// <inheritdoc/>
    public event EventHandler? FilterPanelToggleRequested;
    
    /// <inheritdoc/>
    public event EventHandler? FiltersChanged;
    
    /// <inheritdoc/>
    public void OpenSidePanel()
    {
        IsSidePanelOpen = true;
    }
    
    /// <inheritdoc/>
    public void CloseSidePanel()
    {
        IsSidePanelOpen = false;
    }
    
    /// <inheritdoc/>
    public void ToggleSidePanel()
    {
        IsSidePanelOpen = !IsSidePanelOpen;
    }
    
    /// <inheritdoc/>
    public void SetSidePanelState(bool isOpen)
    {
        IsSidePanelOpen = isOpen;
    }
    
    /// <inheritdoc/>
    public void UpdateSearchQuery(string query)
    {
        SearchQuery = query ?? string.Empty;
    }
    
    /// <inheritdoc/>
    public void ClearSearchQuery()
    {
        SearchQuery = string.Empty;
    }
    
    /// <inheritdoc/>
    public void ToggleFilterPanel()
    {
        FilterPanelToggleRequested?.Invoke(this, EventArgs.Empty);
    }
    
    /// <inheritdoc/>
    public void NotifyFiltersChanged()
    {
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }
}
