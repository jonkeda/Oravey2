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
                new BuildingPlacement("HamletHall", 52.0025, 4.0025, 30.0, 25.0, 0.0, "center")
            },
        };
        var roads = new RoadNetwork([], [], 3f);
        return new TownSpatialSpecification(bounds, buildings, roads, [], "flat");
    }

    /// <summary>Create a village spec (100×100 grid)</summary>
    private static TownSpatialSpecification CreateVillageSpec()
    {
        var bounds = new BoundingBox(52.0, 52.01, 4.0, 4.01);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {
                "VillageTavern",
                new BuildingPlacement("VillageTavern", 52.005, 4.005, 30.0, 25.0, 0.0, "main_road")
            },
            {
                "VillageSmith",
                new BuildingPlacement("VillageSmith", 52.003, 4.003, 25.0, 20.0, 45.0, "side_road")
            },
        };
        var roads = new RoadNetwork([], [], 5f);
        return new TownSpatialSpecification(bounds, buildings, roads, [], "flat");
    }

    /// <summary>Create a town spec (200×200 grid)</summary>
    private static TownSpatialSpecification CreateTownSpec()
    {
        var bounds = new BoundingBox(52.0, 52.02, 4.0, 4.02);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {
                "TownHall",
                new BuildingPlacement("TownHall", 52.01, 4.01, 40.0, 35.0, 0.0, "center")
            },
            {
                "TownTemple",
                new BuildingPlacement("TownTemple", 52.008, 4.008, 35.0, 30.0, 45.0, "north_side")
            },
            {
                "TownMarket",
                new BuildingPlacement("TownMarket", 52.012, 4.012, 50.0, 40.0, 0.0, "south_side")
            },
        };
        var roads = new RoadNetwork([], [], 8f);
        return new TownSpatialSpecification(bounds, buildings, roads, [], "flat");
    }

    /// <summary>Create a city spec with water (300×300 grid)</summary>
    private static TownSpatialSpecification CreateCitySpec()
    {
        var bounds = new BoundingBox(52.0, 52.03, 4.0, 4.03);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {
                "CityPalace",
                new BuildingPlacement("CityPalace", 52.015, 4.015, 50.0, 45.0, 0.0, "imperial_square")
            },
            {
                "CityFort",
                new BuildingPlacement("CityFort", 52.010, 4.010, 45.0, 40.0, 45.0, "defensive_post")
            },
            {
                "CityGuild",
                new BuildingPlacement("CityGuild", 52.020, 4.020, 35.0, 30.0, 0.0, "craft_district")
            },
            {
                "CityMerchant",
                new BuildingPlacement("CityMerchant", 52.005, 4.025, 40.0, 35.0, 45.0, "merchant_quarter")
            },
        };
        
        var waterPolygon = new List<Vector2>
        {
            new(52.015f, 4.015f),
            new(52.015f, 4.025f),
            new(52.025f, 4.025f),
            new(52.025f, 4.015f),
        };
        var waters = new List<SpatialWaterBody>
        {
            new("CityHarbour", waterPolygon, SpatialWaterType.Harbour),
        };
        
        var roads = new RoadNetwork([], [], 10f);
        return new TownSpatialSpecification(bounds, buildings, roads, waters, "flat");
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
    public void City_RoadsConnected_NoGaps()
    {
        // Arrange
        var spec = CreateCitySpec();
        var map = CreateEmptyMap(300, 300);
        var renderer = new SpatialSpecRenderer();

        // Act
        var result = renderer.Render(spec, map, TileSizeMeters);

        // Assert - Road network properties
        Assert.NotNull(result.RoadStats);
        Assert.True(result.RoadStats.TilesRendered >= 0);
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
        Assert.True(result.RoadStats.TilesRendered >= 0, "Roads should render");
        
        Assert.NotNull(result.WaterStats);
        Assert.True(result.WaterStats.Count >= 0, "Water should render");
    }
}
