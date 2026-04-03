using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Validation;

namespace Oravey2.MapGen.App.ViewModels;

public sealed class BlueprintPreviewViewModel : BaseViewModel
{
    private readonly IBlueprintValidator _validator;

    private string? _blueprintJson;
    public string? BlueprintJson
    {
        get => _blueprintJson;
        set
        {
            if (SetProperty(ref _blueprintJson, value))
                ParseBlueprint();
        }
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

    public BlueprintPreviewViewModel(IBlueprintValidator validator)
    {
        _validator = validator;
        RevalidateCommand = new Command(Revalidate, () => BlueprintJson is not null);
    }

    private void ParseBlueprint()
    {
        if (string.IsNullOrEmpty(BlueprintJson))
        {
            ClearFields();
            return;
        }

        try
        {
            var bp = BlueprintLoader.LoadFromString(BlueprintJson);
            MapName = bp.Name;
            MapDescription = bp.Description;
            Dimensions = $"{bp.Dimensions.ChunksWide}×{bp.Dimensions.ChunksHigh} chunks ({bp.Dimensions.ChunksWide * 16}×{bp.Dimensions.ChunksHigh * 16} tiles)";
            TerrainRegionCount = bp.Terrain.Regions?.Length ?? 0;
            RoadCount = bp.Roads?.Length ?? 0;
            BuildingCount = bp.Buildings?.Length ?? 0;
            ZoneCount = bp.Zones?.Length ?? 0;

            Revalidate();
        }
        catch (Exception ex)
        {
            ClearFields();
            ValidationErrors.Clear();
            ValidationErrors.Add($"Parse error: {ex.Message}");
        }
    }

    private void Revalidate()
    {
        ValidationErrors.Clear();

        if (string.IsNullOrEmpty(BlueprintJson)) return;

        try
        {
            var bp = BlueprintLoader.LoadFromString(BlueprintJson);
            var result = _validator.Validate(bp);

            if (result.IsValid)
            {
                ValidationErrors.Add("✓ No errors");
            }
            else
            {
                foreach (var error in result.Errors)
                    ValidationErrors.Add($"[{error.Code}] {error.Message}");
            }
        }
        catch (Exception ex)
        {
            ValidationErrors.Add($"Validation error: {ex.Message}");
        }
    }

    private void ClearFields()
    {
        MapName = null;
        MapDescription = null;
        Dimensions = null;
        TerrainRegionCount = 0;
        RoadCount = 0;
        BuildingCount = 0;
        ZoneCount = 0;
    }
}
