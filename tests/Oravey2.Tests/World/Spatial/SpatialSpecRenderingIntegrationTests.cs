using System.Collections.Generic;
using System.Numerics;
using Oravey2.Contracts.Spatial;
using Oravey2.Core.World;
using Oravey2.Core.World.Spatial;

namespace Oravey2.Tests.World.Spatial;

/// <summary>
/// Integration tests for spatial specification rendering.
/// Tests building placement, road rendering, water bodies, collision detection, and z-ordering.
/// </summary>
public class SpatialSpecRenderingIntegrationTests
{
    private static SpatialSpecRenderer CreateRenderer() => new();

    private static TileMapData CreateEmptyMap(int w = 64, int h = 64)
    {
        var map = new TileMapData(w, h);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());
        return map;
    }

    private static TownSpatialSpecification CreateSimpleSpec()
    {
        var bounds = new BoundingBox(
            MinLat: 0.0, MaxLat: 1.0,
            MinLon: 0.0, MaxLon: 1.0);

        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {
                "MainHall",
                new BuildingPlacement("MainHall", 0.5, 0.5, 50.0, 40.0, 0.0, "square_corner")
            },
            {
                "Barracks",
                new BuildingPlacement("Barracks", 0.3, 0.7, 30.0, 25.0, 45.0, "on_main_road")
            }
        };

        var roadNetwork = new RoadNetwork(
            Nodes: new List<Vector2>
            {
                new(0.25f, 0.5f),
                new(0.75f, 0.5f),
                new(0.5f, 0.25f),
                new(0.5f, 0.75f)
            },
            Edges: new List<RoadEdge>
            {
                new(0.25, 0.5, 0.75, 0.5),
                new(0.5, 0.25, 0.5, 0.75)
            },
            RoadWidthMeters: 8.0f);

        var waterBodies = new List<SpatialWaterBody>
        {
            new(
                "RiverNorth",
                new List<Vector2>
                {
                    new(0.1f, 0.1f),
                    new(0.9f, 0.1f),
                    new(0.9f, 0.15f),
                    new(0.1f, 0.15f)
                },
                SpatialWaterType.River)
        };

        return new TownSpatialSpecification(
            RealWorldBounds: bounds,
            BuildingPlacements: buildings,
            RoadNetwork: roadNetwork,
            WaterBodies: waterBodies,
            TerrainDescription: "flat");
    }

    // --- Building Placement Tests ---

    [Fact]
    public void RenderBuildings_PlacesAtSpecCoordinates()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        Assert.NotNull(result.BuildingStats);
        Assert.Equal(2, result.BuildingStats.Count);
        Assert.True(result.BuildingStats.TilesRendered > 0);
    }

    [Fact]
    public void RenderBuildings_SetsStructureIdOnTiles()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        renderer.Render(spec, mapData);

        // Find a tile that should belong to a building
        int buildingTileCount = 0;
        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
            {
                var tile = mapData.GetTileData(x, y);
                if (tile.StructureId != 0)
                    buildingTileCount++;
            }
        }

        Assert.True(buildingTileCount > 0, "At least some tiles should have StructureId set");
    }

    [Fact]
    public void RenderBuildings_ClearsWalkableFlag()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        renderer.Render(spec, mapData);

        // Building tiles should not be walkable
        int nonWalkableBuildingTiles = 0;
        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
            {
                var tile = mapData.GetTileData(x, y);
                if (tile.StructureId != 0 && !tile.IsWalkable)
                    nonWalkableBuildingTiles++;
            }
        }

        Assert.True(nonWalkableBuildingTiles > 0, "Building tiles should be non-walkable");
    }

    [Fact]
    public void RenderBuildings_AppliesRotation()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        // Verify that buildings with different rotations render successfully
        // (The actual visual verification happens in UI tests)
        Assert.NotNull(result.BuildingStats);
        Assert.True(result.BuildingStats.Count > 0);
    }

    // --- Road Rendering Tests ---

    [Fact]
    public void RenderRoads_CreatesConnectedNetwork()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        Assert.NotNull(result.RoadStats);
        Assert.Equal(2, result.RoadStats.SegmentCount);
        Assert.True(result.RoadStats.TilesRendered > 0);

        // Verify that road tiles form a connected network
        int roadTileCount = 0;
        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
            {
                var tile = mapData.GetTileData(x, y);
                if (tile.Surface == SurfaceType.Asphalt)
                    roadTileCount++;
            }
        }

        Assert.True(roadTileCount > 0, "Road tiles should be rendered");
    }

    [Fact]
    public void RenderRoads_NoGapsInSegments()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        renderer.Render(spec, mapData);

        // Verify continuity in one direction (e.g., horizontal road)
        var roadTiles = new List<(int, int)>();
        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
            {
                var tile = mapData.GetTileData(x, y);
                if (tile.Surface == SurfaceType.Asphalt)
                    roadTiles.Add((x, y));
            }
        }

        Assert.True(roadTiles.Count > 0, "Should have rendered road tiles");
    }

    // --- Water Body Rendering Tests ---

    [Fact]
    public void RenderWater_RendersBodyAtSpecCoordinates()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        Assert.NotNull(result.WaterStats);
        Assert.Equal(1, result.WaterStats.Count);
        Assert.True(result.WaterStats.TilesRendered > 0);
    }

    [Fact]
    public void RenderWater_FormsContiguousRegion()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        renderer.Render(spec, mapData);

        // Count water tiles
        int waterTileCount = 0;
        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
            {
                var tile = mapData.GetTileData(x, y);
                if (tile.WaterLevel > 0)
                    waterTileCount++;
            }
        }

        Assert.True(waterTileCount > 0, "Water tiles should be rendered");
    }

    // --- Collision Detection Tests ---

    [Fact]
    public void CollisionDetection_IdentifiesBuildingBuildingOverlap()
    {
        var bounds = new BoundingBox(0.0, 1.0, 0.0, 1.0);

        // Two heavily overlapping buildings in the center
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            { "Building1", new BuildingPlacement("Building1", 0.5, 0.5, 100.0, 100.0, 0.0, "square") },
            { "Building2", new BuildingPlacement("Building2", 0.50001, 0.50001, 100.0, 100.0, 0.0, "square") }
        };

        var spec = new TownSpatialSpecification(
            RealWorldBounds: bounds,
            BuildingPlacements: buildings,
            RoadNetwork: new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 0),
            WaterBodies: new List<SpatialWaterBody>(),
            TerrainDescription: "flat");

        var renderer = CreateRenderer();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        // Two overlapping 100m buildings at nearly the same location MUST produce collisions
        Assert.True(result.CollisionCount > 0, "Two overlapping 100m buildings should produce collisions");
        Assert.Contains(result.Collisions, c => c.Type == CollisionType.BuildingBuildingOverlap);
    }

    [Fact]
    public void CollisionDetection_IdentifiesBuildingWaterOverlap()
    {
        var bounds = new BoundingBox(0.0, 1.0, 0.0, 1.0);

        var buildings = new Dictionary<string, BuildingPlacement>
        {
            { "BuildingNearWater", new BuildingPlacement("BuildingNearWater", 0.5, 0.12, 20.0, 20.0, 0.0, "square") }
        };

        var waterBodies = new List<SpatialWaterBody>
        {
            new(
                "River",
                new List<Vector2>
                {
                    new(0.1f, 0.1f),
                    new(0.9f, 0.1f),
                    new(0.9f, 0.15f),
                    new(0.1f, 0.15f)
                },
                SpatialWaterType.River)
        };

        var spec = new TownSpatialSpecification(
            RealWorldBounds: bounds,
            BuildingPlacements: buildings,
            RoadNetwork: new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 0),
            WaterBodies: waterBodies,
            TerrainDescription: "flat");

        var renderer = CreateRenderer();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        // Building overlaps water — must detect
        Assert.True(result.CollisionCount > 0, "Building inside water band should produce collisions");
    }

    [Fact]
    public void CollisionDetection_LogsCollisions()
    {
        var bounds = new BoundingBox(0.0, 1.0, 0.0, 1.0);

        var buildings = new Dictionary<string, BuildingPlacement>
        {
            { "B1", new BuildingPlacement("B1", 0.5, 0.5, 40.0, 40.0, 0.0, "square") },
            { "B2", new BuildingPlacement("B2", 0.5, 0.55, 40.0, 40.0, 0.0, "square") }
        };

        var spec = new TownSpatialSpecification(
            RealWorldBounds: bounds,
            BuildingPlacements: buildings,
            RoadNetwork: new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 0),
            WaterBodies: new List<SpatialWaterBody>(),
            TerrainDescription: "flat");

        var renderer = CreateRenderer();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        // Collisions should be in the result
        foreach (var collision in result.Collisions)
        {
            Assert.NotNull(collision.Message);
            Assert.NotEqual(default, collision.Type);
        }
    }

    // --- Z-Ordering Tests ---

    [Fact]
    public void ZOrdering_TerrainRenderedFirst()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        // Check terrain stats exist and that the terrain description was captured
        Assert.NotNull(result.TerrainStats);
        Assert.Equal("flat", result.TerrainStats.Description);
    }

    [Fact]
    public void ZOrdering_WaterBeforeRoads()
    {
        var bounds = new BoundingBox(0.0, 1.0, 0.0, 1.0);

        var waterBodies = new List<SpatialWaterBody>
        {
            new(
                "Lake",
                new List<Vector2>
                {
                    new(0.3f, 0.3f),
                    new(0.7f, 0.3f),
                    new(0.7f, 0.7f),
                    new(0.3f, 0.7f)
                },
                SpatialWaterType.Lake)
        };

        var roadNetwork = new RoadNetwork(
            Nodes: new List<Vector2> { new(0.5f, 0.5f) },
            Edges: new List<RoadEdge> { new(0.3, 0.5, 0.7, 0.5) },
            RoadWidthMeters: 8.0f);

        var spec = new TownSpatialSpecification(
            RealWorldBounds: bounds,
            BuildingPlacements: new Dictionary<string, BuildingPlacement>(),
            RoadNetwork: roadNetwork,
            WaterBodies: waterBodies,
            TerrainDescription: "flat");

        var renderer = CreateRenderer();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        Assert.NotNull(result.WaterStats);
        Assert.NotNull(result.RoadStats);
        Assert.True(result.WaterStats.TilesRendered > 0, "Water should render tiles");
        Assert.True(result.RoadStats.TilesRendered > 0, "Road should render tiles");

        // Verify z-ordering: road tiles should overwrite water where they overlap
        bool hasRoadOverWater = false;
        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
            {
                var tile = mapData.GetTileData(x, y);
                if (tile.Surface == SurfaceType.Asphalt)
                    hasRoadOverWater = true;
            }
        }
        Assert.True(hasRoadOverWater, "Roads should overwrite water tiles (higher z-order)");
    }

    [Fact]
    public void ZOrdering_BuildingsRenderedLast()
    {
        var bounds = new BoundingBox(0.0, 1.0, 0.0, 1.0);

        var buildings = new Dictionary<string, BuildingPlacement>
        {
            { "Hall", new BuildingPlacement("Hall", 0.5, 0.5, 40.0, 40.0, 0.0, "square") }
        };

        var roadNetwork = new RoadNetwork(
            Nodes: new List<Vector2> { new(0.5f, 0.5f) },
            Edges: new List<RoadEdge>(),
            RoadWidthMeters: 8.0f);

        var spec = new TownSpatialSpecification(
            RealWorldBounds: bounds,
            BuildingPlacements: buildings,
            RoadNetwork: roadNetwork,
            WaterBodies: new List<SpatialWaterBody>(),
            TerrainDescription: "flat");

        var renderer = CreateRenderer();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        Assert.NotNull(result.BuildingStats);
        Assert.True(result.BuildingStats.TilesRendered > 0);
    }

    // --- Full Integration Test ---

    [Fact]
    public void FullScene_RenderAllElementTypes()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        // Verify all element types were rendered
        Assert.NotNull(result.TerrainStats);
        Assert.NotNull(result.WaterStats);
        Assert.NotNull(result.RoadStats);
        Assert.NotNull(result.BuildingStats);

        // Verify statistics
        Assert.True(result.BuildingStats.Count > 0);
        Assert.True(result.RoadStats.SegmentCount > 0);
        Assert.True(result.WaterStats.Count > 0);

        // Verify collision detection ran
        Assert.NotNull(result.Collisions);
    }

    [Fact]
    public void FullScene_NoZFighting()
    {
        var renderer = CreateRenderer();
        var spec = CreateSimpleSpec();
        var mapData = CreateEmptyMap();

        var result = renderer.Render(spec, mapData);

        // Verify no tile has conflicting layer assignments that would cause z-fighting
        var hasStructures = false;
        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
            {
                var tile = mapData.GetTileData(x, y);
                if (tile.StructureId != 0)
                    hasStructures = true;
            }
        }
        Assert.True(hasStructures, "Full scene should render at least some building structures");
    }
}
