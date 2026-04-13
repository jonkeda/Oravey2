using System.Windows.Input;
using Oravey2.Contracts.Spatial;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;

namespace Oravey2.MapGen.ViewModels;

public class LayoutStepViewModel : BaseViewModel
{
    private PipelineState _state = new();
    
    // --- Spatial Transform ---
    private TownSpatialTransform? _spatialTransform;
    public TownSpatialTransform? SpatialTransform
    {
        get => _spatialTransform;
        set => SetProperty(ref _spatialTransform, value);
    }

    // --- Grid Display ---
    private int _gridWidthTiles;
    public int GridWidthTiles
    {
        get => _gridWidthTiles;
        private set => SetProperty(ref _gridWidthTiles, value);
    }

    private int _gridHeightTiles;
    public int GridHeightTiles
    {
        get => _gridHeightTiles;
        private set => SetProperty(ref _gridHeightTiles, value);
    }

    private string _gridDimensionText = "";
    public string GridDimensionText
    {
        get => _gridDimensionText;
        private set => SetProperty(ref _gridDimensionText, value);
    }

    // --- Statistics ---
    private int _buildingCount;
    public int BuildingCount
    {
        get => _buildingCount;
        private set => SetProperty(ref _buildingCount, value);
    }

    private double _roadNetworkLength;
    public double RoadNetworkLength
    {
        get => _roadNetworkLength;
        private set => SetProperty(ref _roadNetworkLength, value);
    }

    private double _waterSurfaceArea;
    public double WaterSurfaceArea
    {
        get => _waterSurfaceArea;
        private set => SetProperty(ref _waterSurfaceArea, value);
    }

    // --- Toggle ---
    private bool _useSpatialSpec;
    public bool UseSpatialSpec
    {
        get => _useSpatialSpec;
        set => SetProperty(ref _useSpatialSpec, value);
    }

    // --- Status ---
    private string _statusText = "Select a town design with spatial specification to visualize layout.";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private bool _hasSpatialSpec;
    public bool HasSpatialSpec
    {
        get => _hasSpatialSpec;
        private set => SetProperty(ref _hasSpatialSpec, value);
    }

    // --- Zoom ---
    private double _zoomLevel = 100.0;
    public double ZoomLevel
    {
        get => _zoomLevel;
        set => SetProperty(ref _zoomLevel, value);
    }

    public string ZoomText => $"Zoom: {ZoomLevel:F0}%";

    // --- Commands ---
    private readonly RelayCommand _resetViewCommand;
    private readonly RelayCommand _fitToScreenCommand;

    public ICommand ResetViewCommand => _resetViewCommand;
    public ICommand FitToScreenCommand => _fitToScreenCommand;

    public LayoutStepViewModel()
    {
        _resetViewCommand = new RelayCommand(ResetView, () => UseSpatialSpec && HasSpatialSpec);
        _fitToScreenCommand = new RelayCommand(FitToScreen, () => UseSpatialSpec && HasSpatialSpec);
    }

    /// Update visualization preview from a town design
    public void UpdatePreview(TownDesign design)
    {
        if (design.SpatialSpec == null)
        {
            SpatialTransform = null;
            HasSpatialSpec = false;
            StatusText = "Town design has no spatial specification.";
            return;
        }

        try
        {
            var transform = new TownSpatialTransform(design.SpatialSpec, tileSizeMeters: 1.0f, seed: 0);
            SpatialTransform = transform;

            // Calculate grid dimensions
            var (width, height) = transform.GetGridDimensions();
            GridWidthTiles = width;
            GridHeightTiles = height;
            GridDimensionText = $"{width}×{height} tiles";

            // Calculate statistics
            CalculateStatistics(design.SpatialSpec);

            HasSpatialSpec = true;
            StatusText = $"Layout: {design.TownName} — {GridDimensionText}";
        }
        catch (Exception ex)
        {
            HasSpatialSpec = false;
            StatusText = $"Error: {ex.Message}";
        }
    }

    /// Calculate road network length and water surface area
    private void CalculateStatistics(TownSpatialSpecification spec)
    {
        // Road network length
        double totalRoadLength = 0.0;
        foreach (var edge in spec.RoadNetwork.Edges)
        {
            var dx = edge.ToLon - edge.FromLon;
            var dy = edge.ToLat - edge.FromLat;
            totalRoadLength += Math.Sqrt(dx * dx + dy * dy);
        }
        RoadNetworkLength = totalRoadLength;

        // Water surface area (approximate polygon area)
        double totalWaterArea = 0.0;
        foreach (var water in spec.WaterBodies)
        {
            totalWaterArea += CalculatePolygonArea(water.Polygon);
        }
        WaterSurfaceArea = totalWaterArea;

        // Building count
        BuildingCount = spec.BuildingPlacements.Count;
    }

    /// Calculate the area of a polygon given vertices in lat/lon
    private static double CalculatePolygonArea(List<System.Numerics.Vector2> polygon)
    {
        if (polygon.Count < 3)
            return 0.0;

        // Shoelace formula
        double area = 0.0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            area += current.X * next.Y - next.X * current.Y;
        }

        return Math.Abs(area) / 2.0;
    }

    private void ResetView()
    {
        ZoomLevel = 100.0;
    }

    private void FitToScreen()
    {
        ZoomLevel = 100.0; // Could be more sophisticated
    }
}
