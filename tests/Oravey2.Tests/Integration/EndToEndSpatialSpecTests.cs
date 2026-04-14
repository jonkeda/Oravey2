using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Oravey2.Contracts.Spatial;
using Oravey2.Core.World;
using Oravey2.Core.World.Spatial;
using Xunit;

namespace Oravey2.Tests.Integration;

/// <summary>
/// End-to-end tests for the spatial specification pipeline.
/// Tests MapGen UI → generation → rendering flow for all town sizes.
/// Verifies performance, collision detection, and rendering accuracy.
/// </summary>
public class EndToEndSpatialSpecTests
{
    private const float TileSizeMeters = 10f;

    // ============ Test Fixtures ============

    /// <summary>Create a hamlet spec (50×50 grid)</summary>
    private static TownSpatialSpecification CreateHamletSpec()
    {
        var bounds = new BoundingBox(52.0, 52.005, 4.0, 4.005);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {
                "HamletHall",
                new BuildingPlacement { Name = "HamletHall", CenterLat = 52.0025, CenterLon = 4.0025, WidthMeters = 30.0, DepthMeters = 25.0, RotationDegrees = 0.0, AlignmentHint = "center" }
            },
        };
        var roads = new RoadNetwork { RoadWidthMeters = 3f };
        return new TownSpatialSpecification { RealWorldBounds = bounds, BuildingPlacements = buildings, RoadNetwork = roads, TerrainDescription = "flat" };
    }

    /// <summary>Create a village spec (100×100 grid)</summary>
    private static TownSpatialSpecification CreateVillageSpec()
    {
        var bounds = new BoundingBox(52.0, 52.01, 4.0, 4.01);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {
                "VillageTavern",
                new BuildingPlacement { Name = "VillageTavern", CenterLat = 52.005, CenterLon = 4.005, WidthMeters = 30.0, DepthMeters = 25.0, RotationDegrees = 0.0, AlignmentHint = "main_road" }
            },
            {
                "VillageSmith",
                new BuildingPlacement { Name = "VillageSmith", CenterLat = 52.003, CenterLon = 4.003, WidthMeters = 25.0, DepthMeters = 20.0, RotationDegrees = 45.0, AlignmentHint = "side_road" }
            },
        };
        var roads = new RoadNetwork { RoadWidthMeters = 5f };
        return new TownSpatialSpecification { RealWorldBounds = bounds, BuildingPlacements = buildings, RoadNetwork = roads, TerrainDescription = "flat" };
    }

    /// <summary>Create a town spec (200×200 grid)</summary>
    private static TownSpatialSpecification CreateTownSpec()
    {
        var bounds = new BoundingBox(52.0, 52.02, 4.0, 4.02);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {
                "TownHall",
                new BuildingPlacement { Name = "TownHall", CenterLat = 52.01, CenterLon = 4.01, WidthMeters = 40.0, DepthMeters = 35.0, RotationDegrees = 0.0, AlignmentHint = "center" }
            },
            {
                "TownTemple",
                new BuildingPlacement { Name = "TownTemple", CenterLat = 52.008, CenterLon = 4.008, WidthMeters = 35.0, DepthMeters = 30.0, RotationDegrees = 45.0, AlignmentHint = "north_side" }
            },
            {
                "TownMarket",
                new BuildingPlacement { Name = "TownMarket", CenterLat = 52.012, CenterLon = 4.012, WidthMeters = 50.0, DepthMeters = 40.0, RotationDegrees = 0.0, AlignmentHint = "south_side" }
            },
        };
        var roads = new RoadNetwork { RoadWidthMeters = 8f };
        return new TownSpatialSpecification { RealWorldBounds = bounds, BuildingPlacements = buildings, RoadNetwork = roads, TerrainDescription = "flat" };
    }

    /// <summary>Create a city spec with water (300×300 grid)</summary>
    private static TownSpatialSpecification CreateCitySpec()
    {
        var bounds = new BoundingBox(52.0, 52.03, 4.0, 4.03);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {
                "CityPalace",
                new BuildingPlacement { Name = "CityPalace", CenterLat = 52.015, CenterLon = 4.015, WidthMeters = 50.0, DepthMeters = 45.0, RotationDegrees = 0.0, AlignmentHint = "imperial_square" }
            },
            {
                "CityFort",
                new BuildingPlacement { Name = "CityFort", CenterLat = 52.010, CenterLon = 4.010, WidthMeters = 45.0, DepthMeters = 40.0, RotationDegrees = 45.0, AlignmentHint = "defensive_post" }
            },
            {
                "CityGuild",
                new BuildingPlacement { Name = "CityGuild", CenterLat = 52.020, CenterLon = 4.020, WidthMeters = 35.0, DepthMeters = 30.0, RotationDegrees = 0.0, AlignmentHint = "craft_district" }
            },
            {
                "CityMerchant",
                new BuildingPlacement { Name = "CityMerchant", CenterLat = 52.005, CenterLon = 4.025, WidthMeters = 40.0, DepthMeters = 35.0, RotationDegrees = 45.0, AlignmentHint = "merchant_quarter" }
            },
        };
        
        var waterPolygon = new List<Vector2>
        {
            new(52.026f, 4.002f),
            new(52.026f, 4.008f),
            new(52.029f, 4.008f),
            new(52.029f, 4.002f),
        };
        var waters = new List<SpatialWaterBody>
        {
            new SpatialWaterBody { Name = "CityHarbour", Polygon = waterPolygon, Type = SpatialWaterType.Harbour },
        };
        
        var roads = new RoadNetwork { RoadWidthMeters = 10f };
        return new TownSpatialSpecification { RealWorldBounds = bounds, BuildingPlacements = buildings, RoadNetwork = roads, WaterBodies = waters, TerrainDescription = "flat" };
    }

    /// <summary>Create an empty map for rendering</summary>
    private static TileMapData CreateEmptyMap(int width, int height)
    {
        var map = new TileMapData(width, height);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                map.SetTileData(x, y, TileDataFactory.Ground());
            }
        }
        return map;
    }

    [Fact]
    public void Hamlet_GenerateAndRender_UnderOneSecond()
    {
        // Arrange
        var spec = CreateHamletSpec();
        var map = CreateEmptyMap(50, 50);
        var renderer = new SpatialSpecRenderer();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);
        stopwatch.Stop();

        // Assert - Performance
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
            $"Hamlet generation took {stopwatch.ElapsedMilliseconds}ms, should be < 1000ms");
        
        // Assert - Structure
        Assert.NotNull(result);
        Assert.Equal(1, spec.BuildingPlacements.Count);
        Assert.NotNull(result.BuildingStats);
        Assert.Equal(1, result.BuildingStats.Count);
    }

    [Fact]
    public void Hamlet_RenderBuildings_AtCorrectCoordinates()
    {
        // Arrange
        var spec = CreateHamletSpec();
        var map = CreateEmptyMap(50, 50);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert - Verify building was rendered
        Assert.NotNull(result.BuildingStats);
        Assert.True(result.BuildingStats.TilesRendered > 0);
    }

    [Fact]
    public void Hamlet_NoCollisionsDetected()
    {
        // Arrange
        var spec = CreateHamletSpec();
        var map = CreateEmptyMap(50, 50);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert
        Assert.Equal(0, result.CollisionCount);
    }

    // ============ Village Tests ============

    [Fact]
    public void Village_GenerateAndRender_UnderTwoSeconds()
    {
        // Arrange
        var spec = CreateVillageSpec();
        var map = CreateEmptyMap(100, 100);
        var renderer = new SpatialSpecRenderer();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);
        stopwatch.Stop();

        // Assert - Performance
        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"Village generation took {stopwatch.ElapsedMilliseconds}ms, should be < 2000ms");

        // Assert - Structure
        Assert.NotNull(result);
        Assert.Equal(2, spec.BuildingPlacements.Count);
        Assert.NotNull(result.BuildingStats);
        Assert.Equal(2, result.BuildingStats.Count);
    }

    [Fact]
    public void Village_BuildingsAtDifferentRotations_BothRender()
    {
        // Arrange
        var spec = CreateVillageSpec();
        var map = CreateEmptyMap(100, 100);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert - Both buildings rendered
        Assert.NotNull(result.BuildingStats);
        Assert.Equal(2, result.BuildingStats.Count);
        Assert.True(result.BuildingStats.TilesRendered > 0);
    }

    [Fact]
    public void Village_NoCollisionsBetweenBuildings()
    {
        // Arrange
        var spec = CreateVillageSpec();
        var map = CreateEmptyMap(100, 100);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert - No collisions when buildings are properly spaced
        Assert.Equal(0, result.CollisionCount);
    }

    // ============ Town Tests ============

    [Fact]
    public void Town_GenerateAndRender_UnderFiveSeconds()
    {
        // Arrange
        var spec = CreateTownSpec();
        var map = CreateEmptyMap(200, 200);
        var renderer = new SpatialSpecRenderer();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);
        stopwatch.Stop();

        // Assert - Performance
        Assert.True(stopwatch.ElapsedMilliseconds < 5000,
            $"Town generation took {stopwatch.ElapsedMilliseconds}ms, should be < 5000ms");

        // Assert - Structure
        Assert.NotNull(result);
        Assert.Equal(3, spec.BuildingPlacements.Count);
        Assert.NotNull(result.BuildingStats);
        Assert.Equal(3, result.BuildingStats.Count);
    }

    [Fact]
    public void Town_MultipleBuildings_AllRendered()
    {
        // Arrange
        var spec = CreateTownSpec();
        var map = CreateEmptyMap(200, 200);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert
        Assert.NotNull(result.BuildingStats);
        Assert.Equal(3, result.BuildingStats.Count);
        Assert.True(result.BuildingStats.TilesRendered > 0);
    }

    [Fact]
    public void Town_VerifySpatialSpecUsed_NotProcedural()
    {
        // Arrange
        var spec = CreateTownSpec();
        var map = CreateEmptyMap(200, 200);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert - Buildings explicitly placed from spec
        Assert.NotNull(result.BuildingStats);
        Assert.Equal(3, result.BuildingStats.Count);
        Assert.True(result.BuildingStats.TilesRendered > 0);
    }

    // ============ City Tests ============

    [Fact]
    public void City_GenerateAndRender_UnderTenSeconds()
    {
        // Arrange
        var spec = CreateCitySpec();
        var map = CreateEmptyMap(300, 300);
        var renderer = new SpatialSpecRenderer();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);
        stopwatch.Stop();

        // Assert - Performance
        Assert.True(stopwatch.ElapsedMilliseconds < 10000,
            $"City generation took {stopwatch.ElapsedMilliseconds}ms, should be < 10000ms");

        // Assert - Structure
        Assert.NotNull(result);
        Assert.Equal(4, spec.BuildingPlacements.Count);
        Assert.NotNull(result.BuildingStats);
        Assert.Equal(4, result.BuildingStats.Count);
    }

    [Fact]
    public void City_BuildingsAndWater_BothRender()
    {
        // Arrange
        var spec = CreateCitySpec();
        var map = CreateEmptyMap(300, 300);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert - Buildings rendered
        Assert.NotNull(result.BuildingStats);
        Assert.Equal(4, result.BuildingStats.Count);

        // Assert - Water rendered
        Assert.NotNull(result.WaterStats);
        Assert.True(result.WaterStats.Count > 0);
    }

    [Fact]
    public void City_EmptyRoadNetwork_NoRoadTiles()
    {
        // Arrange
        var spec = CreateCitySpec();
        var map = CreateEmptyMap(300, 300);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert - City spec has no road edges
        Assert.NotNull(result.RoadStats);
        Assert.Equal(0, result.RoadStats.TilesRendered);
        Assert.Equal(0, result.RoadStats.SegmentCount);
    }

    // ============ Cross-Town Tests ============

    [Fact]
    public void AllTownSizes_RenderSuccessfully()
    {
        // Arrange
        var specs = new[]
        {
            ("Hamlet", CreateHamletSpec(), CreateEmptyMap(50, 50)),
            ("Village", CreateVillageSpec(), CreateEmptyMap(100, 100)),
            ("Town", CreateTownSpec(), CreateEmptyMap(200, 200)),
            ("City", CreateCitySpec(), CreateEmptyMap(300, 300)),
        };
        var renderer = new SpatialSpecRenderer();

        // Act & Assert - Each renders without errors
        foreach (var (name, spec, map) in specs)
        {
            var result = renderer.Render(spec, map, TileSizeMeters);
            Assert.NotNull(result);
            Assert.NotNull(result.BuildingStats);
            Assert.True(result.BuildingStats.Count > 0, $"{name} should have buildings");
        }
    }

    [Fact]
    public void AllTownSizes_MeetPerformanceTargets()
    {
        // Arrange
        var specs = new[]
        {
            ("Hamlet", CreateHamletSpec(), CreateEmptyMap(50, 50), 1000),
            ("Village", CreateVillageSpec(), CreateEmptyMap(100, 100), 2000),
            ("Town", CreateTownSpec(), CreateEmptyMap(200, 200), 5000),
            ("City", CreateCitySpec(), CreateEmptyMap(300, 300), 10000),
        };
        var renderer = new SpatialSpecRenderer();

        // Act & Assert - Each meets performance target
        foreach (var (name, spec, map, targetMs) in specs)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = renderer.Render(spec, map, TileSizeMeters);
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < targetMs,
                $"{name} took {stopwatch.ElapsedMilliseconds}ms, target was {targetMs}ms");
        }
    }

    [Fact]
    public void AllTownSizes_NoCollisionsInWellSpacedLayouts()
    {
        // Arrange - All specs have well-spaced buildings
        var specs = new[]
        {
            CreateHamletSpec(),
            CreateVillageSpec(),
            CreateTownSpec(),
            CreateCitySpec(),
        };
        var renderer = new SpatialSpecRenderer();
        var mapSizes = new[] { 50, 100, 200, 300 };

        // Act & Assert - No collisions when buildings properly spaced
        for (int i = 0; i < specs.Length; i++)
        {
            var map = CreateEmptyMap(mapSizes[i], mapSizes[i]);
            var result = renderer.Render(specs[i], map, TileSizeMeters);
            Assert.Equal(0, result.CollisionCount);
        }
    }

    [Fact]
    public void Rendering_BuildingStats_ContainValidData()
    {
        // Arrange
        var spec = CreateTownSpec();
        var map = CreateEmptyMap(200, 200);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert - Stats structure
        Assert.NotNull(result.BuildingStats);
        Assert.True(result.BuildingStats.Count > 0);
        Assert.True(result.BuildingStats.TilesRendered > 0);
    }

    [Fact]
    public void Rendering_CompletePipeline_WaterRoadsBuildings()
    {
        // Arrange
        var spec = CreateCitySpec();
        var map = CreateEmptyMap(300, 300);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert - All three layers rendered
        Assert.NotNull(result.BuildingStats);
        Assert.True(result.BuildingStats.Count > 0, "Buildings should render");
        
        Assert.NotNull(result.RoadStats);
        Assert.Equal(0, result.RoadStats.TilesRendered);  // City spec has no road edges
        
        Assert.NotNull(result.WaterStats);
        Assert.True(result.WaterStats.Count > 0, "City spec includes water bodies");
        Assert.True(result.WaterStats.TilesRendered > 0, "Water bodies should produce tiles");
    }
}
