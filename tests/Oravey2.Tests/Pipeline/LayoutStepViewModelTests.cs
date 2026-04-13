using System.Numerics;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.Tests.Pipeline;

public class LayoutStepViewModelTests
{
    private static LayoutStepViewModel MakeVM() => new();

    private static BoundingBox MakeBounds() => new(
        MinLat: 52.0,
        MaxLat: 53.0,
        MinLon: 4.0,
        MaxLon: 5.0);

    private static TownSpatialSpecification MakeSpatialSpec() => new(
        RealWorldBounds: MakeBounds(),
        BuildingPlacements: new Dictionary<string, BuildingPlacement>
        {
            ["Fort"] = new(
                Name: "Fort Kijkduin",
                CenterLat: 52.5,
                CenterLon: 4.5,
                WidthMeters: 50.0,
                DepthMeters: 40.0,
                RotationDegrees: 45.0,
                AlignmentHint: "on_main_road"),
            ["Market"] = new(
                Name: "Market Square",
                CenterLat: 52.6,
                CenterLon: 4.6,
                WidthMeters: 30.0,
                DepthMeters: 30.0,
                RotationDegrees: 0.0,
                AlignmentHint: "square_corner")
        },
        RoadNetwork: new(
            Nodes: [new Vector2(52.5f, 4.5f), new Vector2(52.6f, 4.6f)],
            Edges: [new RoadEdge(52.5, 4.5, 52.6, 4.6)],
            RoadWidthMeters: 5.0f),
        WaterBodies: new List<SpatialWaterBody>
        {
            new(
                Name: "Harbour",
                Polygon: new List<Vector2>
                {
                    new(52.3f, 4.3f),
                    new(52.4f, 4.3f),
                    new(52.4f, 4.4f),
                    new(52.3f, 4.4f)
                },
                Type: SpatialWaterType.Harbour)
        },
        TerrainDescription: "flat");

    private static TownDesign MakeTownDesign(string townName = "Haven", bool withSpatialSpec = true) => new(
        TownName: townName,
        Landmarks: new List<LandmarkBuilding>
        {
            new("Fort", "A massive coastal fortress", "large", "", "", "")
        },
        KeyLocations: new List<KeyLocation>
        {
            new("Market", "shop", "An old market", "medium", "", "", "")
        },
        LayoutStyle: "compound",
        Hazards: new List<EnvironmentalHazard>(),
        SpatialSpec: withSpatialSpec ? MakeSpatialSpec() : null);

    // --- Default state ---

    [Fact]
    public void Default_IsNotEmpty()
    {
        var vm = MakeVM();
        Assert.False(vm.HasSpatialSpec);
        Assert.Equal(0, vm.GridWidthTiles);
        Assert.Equal(0, vm.GridHeightTiles);
        Assert.Equal(100.0, vm.ZoomLevel);
    }

    [Fact]
    public void Default_StatusIsEmpty()
    {
        var vm = MakeVM();
        Assert.NotEmpty(vm.StatusText);
        Assert.Contains("Select a town", vm.StatusText);
    }

    // --- Update Preview ---

    [Fact]
    public void UpdatePreview_WithoutSpatialSpec_HasSpatialSpecIsFalse()
    {
        var vm = MakeVM();
        var design = MakeTownDesign(withSpatialSpec: false);

        vm.UpdatePreview(design);

        Assert.False(vm.HasSpatialSpec);
        Assert.Null(vm.SpatialTransform);
        Assert.Contains("no spatial specification", vm.StatusText);
    }

    [Fact]
    public void UpdatePreview_WithSpatialSpec_PopulatesGridDimensions()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();

        vm.UpdatePreview(design);

        Assert.True(vm.HasSpatialSpec);
        Assert.NotNull(vm.SpatialTransform);
        Assert.True(vm.GridWidthTiles > 0);
        Assert.True(vm.GridHeightTiles > 0);
    }

    [Fact]
    public void UpdatePreview_WithSpatialSpec_GridDimensionTextIsSet()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();

        vm.UpdatePreview(design);

        Assert.NotEmpty(vm.GridDimensionText);
        Assert.Contains("×", vm.GridDimensionText);
        Assert.Contains("tiles", vm.GridDimensionText);
    }

    [Fact]
    public void UpdatePreview_WithSpatialSpec_CalculatesBuildingCount()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();

        vm.UpdatePreview(design);

        Assert.Equal(2, vm.BuildingCount); // Fort and Market
    }

    [Fact]
    public void UpdatePreview_WithSpatialSpec_CalculatesRoadLength()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();

        vm.UpdatePreview(design);

        Assert.True(vm.RoadNetworkLength >= 0.0);
    }

    [Fact]
    public void UpdatePreview_WithSpatialSpec_CalculatesWaterArea()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();

        vm.UpdatePreview(design);

        Assert.True(vm.WaterSurfaceArea >= 0.0);
    }

    // --- Spatial Transform ---

    [Fact]
    public void SpatialTransform_ConvertsBuildingPlacements()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();

        vm.UpdatePreview(design);

        Assert.NotNull(vm.SpatialTransform);
        var buildings = vm.SpatialTransform.TransformBuildingPlacements();
        Assert.Equal(2, buildings.Count);
    }

    [Fact]
    public void SpatialTransform_ConvertsRoadNetwork()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();

        vm.UpdatePreview(design);

        Assert.NotNull(vm.SpatialTransform);
        var roads = vm.SpatialTransform.TransformRoadNetwork();
        Assert.Single(roads);
    }

    [Fact]
    public void SpatialTransform_ConvertsWaterBodies()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();

        vm.UpdatePreview(design);

        Assert.NotNull(vm.SpatialTransform);
        var waters = vm.SpatialTransform.TransformWaterBodies();
        Assert.Single(waters);
        Assert.Equal("Harbour", waters[0].Name);
    }

    // --- Zoom ---

    [Fact]
    public void ZoomLevel_DefaultIs100()
    {
        var vm = MakeVM();
        Assert.Equal(100.0, vm.ZoomLevel);
    }

    [Fact]
    public void ZoomText_ReflectsZoomLevel()
    {
        var vm = MakeVM();
        vm.ZoomLevel = 150.0;
        Assert.Contains("150", vm.ZoomText);
    }

    // --- Commands ---

    [Fact]
    public void ResetViewCommand_ResetsZoomTo100()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();
        vm.UpdatePreview(design);
        vm.UseSpatialSpec = true;
        vm.ZoomLevel = 200.0;

        vm.ResetViewCommand.Execute(null);

        Assert.Equal(100.0, vm.ZoomLevel);
    }

    [Fact]
    public void FitToScreenCommand_ModifiesZoom()
    {
        var vm = MakeVM();
        var design = MakeTownDesign();
        vm.UpdatePreview(design);
        vm.UseSpatialSpec = true;
        vm.ZoomLevel = 50.0;

        vm.FitToScreenCommand.Execute(null);

        Assert.Equal(100.0, vm.ZoomLevel);
    }

    // --- Properties ---

    [Fact]
    public void UseSpatialSpec_CanBeToggled()
    {
        var vm = MakeVM();
        Assert.False(vm.UseSpatialSpec);

        vm.UseSpatialSpec = true;
        Assert.True(vm.UseSpatialSpec);
    }

    [Fact]
    public void StatusText_UpdatesOnPreviewChange()
    {
        var vm = MakeVM();
        var originalStatus = vm.StatusText;
        
        var design = MakeTownDesign();
        vm.UpdatePreview(design);

        Assert.NotEqual(originalStatus, vm.StatusText);
        Assert.Contains("Haven", vm.StatusText);
    }
}
