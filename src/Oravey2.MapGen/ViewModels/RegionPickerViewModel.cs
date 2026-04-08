using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Oravey2.MapGen.WorldTemplate;

namespace Oravey2.MapGen.ViewModels;

public class RegionTreeItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public GeofabrikRegion Region { get; }
    public int Depth { get; }
    public bool HasChildren => Region.Children.Count > 0;

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpandIcon)); } }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public string ExpandIcon => HasChildren ? (IsExpanded ? "▼" : "▶") : "  ";
    public double IndentLeft => Depth * 20;

    public RegionTreeItem(GeofabrikRegion region, int depth)
    {
        Region = region;
        Depth = depth;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RegionPickerViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly IGeofabrikService _geofabrikService;
    private GeofabrikIndex? _index;
    private List<RegionTreeItem> _allItems = [];

    public ObservableCollection<RegionTreeItem> FlatItems { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    private RegionTreeItem? _selectedItem;
    public RegionTreeItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem == value) return;
            if (_selectedItem is not null) _selectedItem.IsSelected = false;
            _selectedItem = value;
            if (_selectedItem is not null) _selectedItem.IsSelected = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedRegion));
            OnPropertyChanged(nameof(SelectedPath));
            OnPropertyChanged(nameof(SelectedPbfUrl));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public GeofabrikRegion? SelectedRegion => SelectedItem?.Region;
    public bool HasSelection => SelectedItem is not null;

    public string SelectedPath
    {
        get
        {
            if (SelectedRegion is null) return string.Empty;
            return BuildPath(SelectedRegion);
        }
    }

    public string SelectedPbfUrl => SelectedRegion?.PbfUrl ?? string.Empty;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
    }

    public ICommand LoadIndexCommand { get; }
    public ICommand ToggleExpandCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CancelCommand { get; }

    public event Action<RegionPreset>? RegionSelected;
    public event Action? Cancelled;

    public RegionPickerViewModel(IGeofabrikService geofabrikService)
    {
        _geofabrikService = geofabrikService;
        LoadIndexCommand = new AsyncRelayCommand(LoadIndexAsync);
        ToggleExpandCommand = new RelayCommand(() => { }); // expand is done via ToggleExpand() directly
        SelectCommand = new RelayCommand(OnSelect);
        CancelCommand = new RelayCommand(() => Cancelled?.Invoke());
    }

    private async Task LoadIndexAsync()
    {
        IsLoading = true;
        try
        {
            _index = await _geofabrikService.GetIndexAsync();
            BuildFlatList();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildFlatList()
    {
        _allItems.Clear();
        if (_index is null) return;

        foreach (var root in _index.Roots)
            Flatten(root, 0);

        ApplyFilter();
    }

    private void Flatten(GeofabrikRegion region, int depth)
    {
        _allItems.Add(new RegionTreeItem(region, depth));
        foreach (var child in region.Children)
            Flatten(child, depth + 1);
    }

    private void ApplyFilter()
    {
        FlatItems.Clear();
        var query = SearchText.Trim();

        if (string.IsNullOrEmpty(query))
        {
            // Show only roots (depth 0) when no search and nothing expanded
            foreach (var item in _allItems)
            {
                if (item.Depth == 0 || IsAncestorExpanded(item))
                    FlatItems.Add(item);
            }
        }
        else
        {
            // Find matching regions
            var matchingIds = new HashSet<string>();
            foreach (var item in _allItems)
            {
                if (MatchesSearch(item.Region, query))
                {
                    matchingIds.Add(item.Region.Id);
                    // Add all ancestors
                    AddAncestorIds(item.Region, matchingIds);
                }
            }

            foreach (var item in _allItems)
            {
                if (matchingIds.Contains(item.Region.Id))
                {
                    if (item.HasChildren && !item.IsExpanded)
                        item.IsExpanded = true;
                    FlatItems.Add(item);
                }
            }
        }
    }

    private bool IsAncestorExpanded(RegionTreeItem item)
    {
        if (item.Depth == 0) return true;
        // Walk up the chain: find parent item
        var parentId = item.Region.Parent;
        while (parentId is not null)
        {
            var parentItem = _allItems.FirstOrDefault(i => i.Region.Id == parentId);
            if (parentItem is null) return false;
            if (!parentItem.IsExpanded) return false;
            parentId = parentItem.Region.Parent;
        }
        return true;
    }

    private void AddAncestorIds(GeofabrikRegion region, HashSet<string> ids)
    {
        var parentId = region.Parent;
        while (parentId is not null && _index?.ById.TryGetValue(parentId, out var parent) == true)
        {
            ids.Add(parentId);
            parentId = parent.Parent;
        }
    }

    private static bool MatchesSearch(GeofabrikRegion region, string query)
    {
        if (region.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (region.Iso3166Alpha2?.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase)) == true)
            return true;
        if (region.Iso3166_2?.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase)) == true)
            return true;
        return false;
    }

    public void ToggleExpand(RegionTreeItem? item)
    {
        if (item is null || !item.HasChildren) return;
        item.IsExpanded = !item.IsExpanded;
        ApplyFilter();
    }

    private void OnSelect()
    {
        if (SelectedRegion is null) return;
        try
        {
            var preset = SelectedRegion.ToRegionPreset();
            RegionSelected?.Invoke(preset);
        }
        catch (InvalidOperationException)
        {
            // No bounding box available — ignore
        }
    }

    private string BuildPath(GeofabrikRegion region)
    {
        var parts = new List<string> { region.Name };
        var parentId = region.Parent;
        while (parentId is not null && _index?.ById.TryGetValue(parentId, out var parent) == true)
        {
            parts.Add(parent.Name);
            parentId = parent.Parent;
        }
        parts.Reverse();
        return string.Join(" → ", parts);
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
