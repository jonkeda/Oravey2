using System.Numerics;
using Oravey2.Contracts.Spatial;
using Oravey2.Core.World;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.RegionTemplates;
using Oravey2.MapGen.Services;
using Xunit;

namespace Oravey2.Tests.Generation;

/// <summary>
/// Tests for the integrated spatial spec generation pipeline.
/// Covers decision routing, logging, configuration, and error handling.
/// </summary>
public class SpatialSpecGenerationPipelineTests
{
    private static (CuratedTown Town, TownEntry Entry, RegionTemplate Region, TownDesign Design) CreateTestScenario()
    {
        var elevation = new float[10, 10];
        for (int r = 0; r < 10; r++)
            for (int c = 0; c < 10; c++)
                elevation[r, c] = 3f;

        var boundary = new Vector2[]
        {
            new(-100, -100), new(100, -100),
            new(100, 100), new(-100, 100)
        };

        var region = new RegionTemplate
        {
            Name = "TestRegion",
            ElevationGrid = elevation,
            GridOriginLat = 52.50,
            GridOriginLon = 4.95,
            GridCellSizeMetres = 30.0,
            Towns = [new TownEntry("TestTown", 52.50, 4.95, 50000, new Vector2(0, 0), TownCategory.Town, boundary)],
            Roads = [new RoadSegment(LinearFeatureType.Primary, [new Vector2(-50, 0), new Vector2(50, 0)])],
            WaterBodies = [],
            Railways = [],
            LandUseZones = [new LandUseZone(LandUseType.Residential,
                [new Vector2(-100, -100), new Vector2(100, -100), new Vector2(100, 100), new Vector2(-100, 100)])]
        };

        var town = new CuratedTown(
            GameName: "Haven",
            RealName: "TestTown",
            Latitude: 52.50,
            Longitude: 4.95,
            GamePosition: new Vector2(0, 0),
            Description: "A fortified market town",
            Size: TownCategory.Town,
            Inhabitants: 5000,
            Destruction: DestructionLevel.Pristine,
            BoundaryPolygon: boundary);

        var townEntry = new TownEntry("Haven", 52.50, 4.95, 50000, new Vector2(0, 0), TownCategory.Town, boundary);

        var design = new TownDesign(
            TownName: "Haven",
            Landmarks: [new LandmarkBuilding("Castle", "Ruined castle", "large", "castle", "castle prompt", "town_centre")],
            KeyLocations: [new KeyLocation("Well", "Water source", "Well structure", "small", "well", "well prompt", "market_square")],
            LayoutStyle: "grid",
            Hazards: [],
            SpatialSpec: null);

        return (town, townEntry, region, design);
    }

    private static TownSpatialSpecification CreateValidSpatialSpec()
    {
        return new TownSpatialSpecification
        {
            RealWorldBounds = new BoundingBox(52.50, 52.51, 4.95, 4.96),
            BuildingPlacements = new Dictionary<string, BuildingPlacement>
            {
                ["castle"] = new BuildingPlacement { Name = "Castle", CenterLat = 52.505, CenterLon = 4.955, WidthMeters = 30, DepthMeters = 30, RotationDegrees = 0, AlignmentHint = "town_centre" },
                ["well"] = new BuildingPlacement { Name = "Well", CenterLat = 52.504, CenterLon = 4.954, WidthMeters = 5, DepthMeters = 5, RotationDegrees = 0, AlignmentHint = "market_square" }
            },
            RoadNetwork = new RoadNetwork
            {
                Nodes = [new Vector2(52.50f, 4.95f), new Vector2(52.51f, 4.96f)],
                Edges = [new RoadEdge(52.50, 4.95, 52.51, 4.96)],
                RoadWidthMeters = 5f
            },
            WaterBodies = [new SpatialWaterBody
            {
                Name = "River",
                Polygon = [new Vector2(52.502f, 4.952f), new Vector2(52.503f, 4.953f)],
                Type = SpatialWaterType.River
            }],
            TerrainDescription = "flat"
        };
    }

    [Fact]
    public async Task GenerateTownMapAsync_WithValidSpatialSpec_UsesSpatialSpecPath()
    {
        // Arrange
        var (town, townEntry, region, baseDesign) = CreateTestScenario();
        var spec = CreateValidSpatialSpec();
        var design = baseDesign with { SpatialSpec = spec };

        var logMessages = new List<string>();
        var service = new MapGeneratorService(new DummyAssetRegistry());
        service.SetLogger(msg => logMessages.Add(msg));
        service.SetPreferSpatialSpecs(true);

        var parms = new MapGenerationParams { Seed = 42 };

        // Act
        var result = await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        // Assert
        Assert.NotNull(result);
        Assert.True(logMessages.Any(m => m.Contains("Generating town with spatial spec")), 
            "Expected spatial spec decision log");
        Assert.True(logMessages.Any(m => m.Contains("buildings")), 
            "Expected building count in log");
    }

    [Fact]
    public async Task GenerateTownMapAsync_WithNullSpatialSpec_UsesProcedural()
    {
        // Arrange
        var (town, townEntry, region, design) = CreateTestScenario();
        // design has SpatialSpec = null by default

        var logMessages = new List<string>();
        var service = new MapGeneratorService(new DummyAssetRegistry());
        service.SetLogger(msg => logMessages.Add(msg));
        service.SetPreferSpatialSpecs(true);

        var parms = new MapGenerationParams { Seed = 42 };

        // Act
        var result = await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        // Assert
        Assert.NotNull(result);
        Assert.True(logMessages.Any(m => m.Contains("procedural") && m.Contains("no spatial spec")), 
            "Expected procedural fallback log");
    }

    [Fact]
    public async Task GenerateTownMapAsync_WithSpatialSpecButDisabled_UsesProcedural()
    {
        // Arrange
        var (town, townEntry, region, baseDesign) = CreateTestScenario();
        var spec = CreateValidSpatialSpec();
        var design = baseDesign with { SpatialSpec = spec };

        var logMessages = new List<string>();
        var service = new MapGeneratorService(new DummyAssetRegistry());
        service.SetLogger(msg => logMessages.Add(msg));
        service.SetPreferSpatialSpecs(false); // Disable despite having spec

        var parms = new MapGenerationParams { Seed = 42 };

        // Act
        var result = await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        // Assert
        Assert.NotNull(result);
        Assert.True(logMessages.Any(m => m.Contains("procedural") && m.Contains("disabled")), 
            "Expected procedural fallback log when specs disabled");
    }

    [Fact]
    public async Task DecisionLogging_IncludesSpecStatistics()
    {
        // Arrange
        var (town, townEntry, region, baseDesign) = CreateTestScenario();
        var spec = CreateValidSpatialSpec();
        var design = baseDesign with { SpatialSpec = spec };

        var logMessages = new List<string>();
        var service = new MapGeneratorService(new DummyAssetRegistry());
        service.SetLogger(msg => logMessages.Add(msg));
        service.SetPreferSpatialSpecs(true);

        var parms = new MapGenerationParams { Seed = 42 };

        // Act
        await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        // Assert
        var logs = string.Join(" ", logMessages);
        Assert.Contains("buildings", logs);
        Assert.Contains("roads", logs);
        Assert.Contains("water bodies", logs);
    }

    [Fact]
    public async Task PreferSpatialSpecs_DefaultIsTrue()
    {
        // Arrange
        var (town, townEntry, region, baseDesign) = CreateTestScenario();
        var spec = CreateValidSpatialSpec();
        var design = baseDesign with { SpatialSpec = spec };

        var logMessages = new List<string>();
        var service = new MapGeneratorService(new DummyAssetRegistry());
        service.SetLogger(msg => logMessages.Add(msg));
        // Don't call SetPreferSpatialSpecs - should default to true

        var parms = new MapGenerationParams { Seed = 42 };

        // Act
        await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        // Assert
        Assert.True(logMessages.Any(m => m.Contains("Generating town with spatial spec")), 
            "Expected default to prefer spatial specs");
    }

    [Fact]
    public async Task ConfigurationToggle_CanForceProcedural()
    {
        // Arrange
        var (town, townEntry, region, baseDesign) = CreateTestScenario();
        var spec = CreateValidSpatialSpec();
        var design = baseDesign with { SpatialSpec = spec };

        var service = new MapGeneratorService(new DummyAssetRegistry());

        var parms = new MapGenerationParams { Seed = 42 };

        // Act - test both paths with same design
        service.SetPreferSpatialSpecs(true);
        var result1 = await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        service.SetPreferSpatialSpecs(false);
        var result2 = await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        // Assert - both should succeed but take different paths
        Assert.NotNull(result1);
        Assert.NotNull(result2);
    }

    [Fact]
    public async Task ErrorHandling_GracefullyHandlesInvalidSpec()
    {
        // Arrange
        var (town, townEntry, region, baseDesign) = CreateTestScenario();
        
        // Create an invalid spec that might cause issues
        var invalidSpec = new TownSpatialSpecification
        {
            RealWorldBounds = new BoundingBox(0, 0, 0, 0),
            RoadNetwork = new RoadNetwork { RoadWidthMeters = 0 },
            TerrainDescription = ""
        };
        
        var design = baseDesign with { SpatialSpec = invalidSpec };

        var logMessages = new List<string>();
        var service = new MapGeneratorService(new DummyAssetRegistry());
        service.SetLogger(msg => logMessages.Add(msg));
        service.SetPreferSpatialSpecs(true);

        var parms = new MapGenerationParams { Seed = 42 };

        // Act - should not throw, should gracefully handle or fall back
        var result = await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        // Assert - the key is that we get a result without throwing
        Assert.NotNull(result);
        Assert.NotNull(result.Layout);
    }

    [Fact]
    public async Task ResultContainsSpatialSpec_WhenUsingSpecPath()
    {
        // Arrange
        var (town, townEntry, region, baseDesign) = CreateTestScenario();
        var spec = CreateValidSpatialSpec();
        var design = baseDesign with { SpatialSpec = spec };

        var service = new MapGeneratorService(new DummyAssetRegistry());
        service.SetPreferSpatialSpecs(true);

        var parms = new MapGenerationParams { Seed = 42 };

        // Act
        var result = await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.SpatialSpec);
        Assert.Equal(spec.RealWorldBounds, result.SpatialSpec.RealWorldBounds);
    }

    [Fact]
    public async Task ResultDoesNotContainSpatialSpec_WhenUsingProceduralPath()
    {
        // Arrange
        var (town, townEntry, region, design) = CreateTestScenario();
        // design has SpatialSpec = null

        var service = new MapGeneratorService(new DummyAssetRegistry());
        service.SetPreferSpatialSpecs(true);

        var parms = new MapGenerationParams { Seed = 42 };

        // Act
        var result = await service.GenerateTownMapAsync(design, town, townEntry, region, parms);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SpatialSpec);
    }
}

/// <summary>Dummy asset registry for testing.</summary>
internal sealed class DummyAssetRegistry : IAssetRegistry
{
    public IReadOnlyList<AssetEntry> Search(string assetType, string query) => [];
    public IReadOnlyList<AssetEntry> ListPrefabs(string category) => [];
    public bool Exists(string assetType, string id) => false;
}
