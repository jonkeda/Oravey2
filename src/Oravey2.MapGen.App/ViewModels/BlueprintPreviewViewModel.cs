using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Oravey2.MapGen.App.ViewModels;

public sealed class BlueprintPreviewViewModel : AppBaseViewModel
{
    private string? _blueprintJson;
    public string? BlueprintJson
    {
        get => _blueprintJson;
        set => SetProperty(ref _blueprintJson, value);
    }

    private string? _mapName;
    public string? MapName { get => _mapName; private set => SetProperty(ref _mapName, value); }

    private string? _mapDescription;
    public string? MapDescription { get => _mapDescription; private set => SetProperty(ref _mapDescription, value); }

    private string? _dimensions;
    public string? Dimensions { get => _dimensions; private set => SetProperty(ref _dimensions, value); }

    private int _terrainRegionCount;
    public int TerrainRegionCount { get => _terrainRegionCount; private set => SetProperty(ref _terrainRegionCount, value); }

    private int _roadCount;
    public int RoadCount { get => _roadCount; private set => SetProperty(ref _roadCount, value); }

    private int _buildingCount;
    public int BuildingCount { get => _buildingCount; private set => SetProperty(ref _buildingCount, value); }

    private int _zoneCount;
    public int ZoneCount { get => _zoneCount; private set => SetProperty(ref _zoneCount, value); }

    public ObservableCollection<string> ValidationErrors { get; } = new();

    public ICommand RevalidateCommand { get; }

    public BlueprintPreviewViewModel()
    {
        RevalidateCommand = new Command(() => { }, () => false);
    }
}
