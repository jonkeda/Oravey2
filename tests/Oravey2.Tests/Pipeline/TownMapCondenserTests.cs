using Oravey2.Contracts.Spatial;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.RegionTemplates;
using System.Numerics;
using Oravey2.Core.World;

namespace Oravey2.Tests.Pipeline;

public class TownMapCondenserTests
{
    private static TownDesign CreateTestDesign(int keyLocationCount = 3) => new(
        "TestTown",
        [new LandmarkBuilding("Fort Test", "A ruined fortress", "large", "", "", "")],
        Enumerable.Range(0, keyLocationCount).Select(i =>
            new KeyLocation($"Location_{i}", "shop", "A building", "medium", "", "", "")).ToList(),
        "organic",
        [new EnvironmentalHazard("flooding", "Water rises", "south-west waterfront")]);

    private static CuratedTown CreateTestTown() => new(
        "TestTown", "RealTest", 52.5, 4.8,
        System.Numerics.Vector2.Zero,
        "A test town", TownCategory.Town, 5000, DestructionLevel.Moderate);

    private static RegionTemplate CreateMinimalRegion() => new()
    {
        Name = "test-region",
        ElevationGrid = new float[1, 1],
        GridOriginLat = 52.0,
        GridOriginLon = 4.0,
        GridCellSizeMetres = 100,
    };

    /// <summary>Test 1: Backward compatibility with existing procedural API (no spatial spec)</summary>
    [Fact]
    public void CondenseWithoutSpatialSpec_BackwardCompatible()
    {
        var town = CreateTestTown();
        var design = CreateTestDesign();
        var region = CreateMinimalRegion();
        var condenser = new TownMapCondenser(design);

        var result = condenser.Condense(town, design, region, 42);

        Assert.NotNull(result.Layout);
        Assert.True(result.Layout.Width >= 16);
        Assert.True(result.Layout.Height >= 16);
        Assert.Equal(0, result.Layout.Width % 16);
        Assert.Null(result.SpatialSpec);  // No spatial spec in backward-compat mode
    }

    /// <summary>Test 2: Small variable-size map with spatial spec</summary>
    [Fact]
    public void CondenseWithSpatialSpec_SmallMap_ProducesCorrectDimensions()
    {
        var design = CreateTestDesign();
        var bounds = new BoundingBox(52.0, 52.01, 4.0, 4.01);
        var spec = new TownSpatialSpecification
        {
            RealWorldBounds = bounds,
            RoadNetwork = new RoadNetwork { RoadWidthMeters = 10f },
            TerrainDescription = "flat"
        };

        var spatialTransform = new TownSpatialTransform(spec, tileSizeMeters: 10f, seed: 42);
        var (gridW, gridH) = spatialTransform.GetGridDimensions();

        // Create minimal chunk array
        var chunks = new TownChunk[1][];
        chunks[0] = new TownChunk[1];
        chunks[0][0] = CreateTestChunk(0, 0);

        var condenser = new TownMapCondenser(design);
        var result = condenser.CondenseWithSpatialSpec(chunks, spatialTransform);

        Assert.Equal(gridW, result.Layout.Width);
        Assert.Equal(gridH, result.Layout.Height);
        Assert.True(gridW >= 50);  // Min 50 tiles per dimension
        Assert.True(gridH >= 50);
    }

    /// <summary>Test 3: Large variable-size map with spatial spec</summary>
    [Fact]
    public void CondenseWithSpatialSpec_LargeMap_ProducesCorrectDimensions()
    {
        var design = CreateTestDesign();
        // Larger bounds = larger grid
        var bounds = new BoundingBox(52.0, 52.1, 4.0, 4.1);
        var spec = new TownSpatialSpecification
        {
            RealWorldBounds = bounds,
            RoadNetwork = new RoadNetwork { RoadWidthMeters = 10f },
            TerrainDescription = "flat"
        };

        var spatialTransform = new TownSpatialTransform(spec, tileSizeMeters: 5f, seed: 42);
        var (gridW, gridH) = spatialTransform.GetGridDimensions();

        // Create chunk array
        var chunksNeeded = (gridW + 15) / 16;
        var chunks = new TownChunk[chunksNeeded][];
        for (var cy = 0; cy < chunksNeeded; cy++)
        {
            chunks[cy] = new TownChunk[chunksNeeded];
            for (var cx = 0; cx < chunksNeeded; cx++)
                chunks[cy][cx] = CreateTestChunk(cx, cy);
        }

        var condenser = new TownMapCondenser(design);
        var result = condenser.CondenseWithSpatialSpec(chunks, spatialTransform);

        Assert.Equal(gridW, result.Layout.Width);
        Assert.Equal(gridH, result.Layout.Height);
        // Large maps should be significantly larger than small ones
        Assert.True(gridW > 100);
        Assert.True(gridH > 100);
    }

    /// <summary>Test 4: Fallback to procedural when insufficient chunks</summary>
    [Fact]
    public void CondenseWithSpatialSpec_InsufficientChunks_FallsBackToProcedural()
    {
        var design = CreateTestDesign();
        var bounds = new BoundingBox(52.0, 52.1, 4.0, 4.1);
        var spec = new TownSpatialSpecification
        {
            RealWorldBounds = bounds,
            RoadNetwork = new RoadNetwork { RoadWidthMeters = 10f },
            TerrainDescription = "flat"
        };

        var spatialTransform = new TownSpatialTransform(spec, tileSizeMeters: 5f, seed: 42);
        var (gridW, gridH) = spatialTransform.GetGridDimensions();

        // Create insufficient chunks (less than needed)
        var chunks = new TownChunk[1][];
        chunks[0] = new TownChunk[1];
        chunks[0][0] = CreateTestChunk(0, 0);

        var condenser = new TownMapCondenser(design);
        var result = condenser.CondenseWithSpatialSpec(chunks, spatialTransform);

        // Should fall back and use a different size
        Assert.NotNull(result.Layout);
        Assert.NotEmpty(result.Zones);
    }

    /// <summary>Test 5: Non-power-of-2 chunk dimensions handled correctly</summary>
    [Fact]
    public void CondenseWithSpatialSpec_NonPowerOf2Grid_TilesCorrectly()
    {
        var design = CreateTestDesign();
        var bounds = new BoundingBox(52.0, 52.05, 4.0, 4.05);
        var spec = new TownSpatialSpecification
        {
            RealWorldBounds = bounds,
            RoadNetwork = new RoadNetwork { RoadWidthMeters = 10f },
            TerrainDescription = "flat"
        };

        var spatialTransform = new TownSpatialTransform(spec, tileSizeMeters: 8f, seed: 42);
        var (gridW, gridH) = spatialTransform.GetGridDimensions();

        // Allocate exactly enough chunks for non-power-of-2 grid
        var chunksWide = (gridW + 15) / 16;
        var chunksHigh = (gridH + 15) / 16;
        var chunks = new TownChunk[chunksHigh][];
        for (var cy = 0; cy < chunksHigh; cy++)
        {
            chunks[cy] = new TownChunk[chunksWide];
            for (var cx = 0; cx < chunksWide; cx++)
                chunks[cy][cx] = CreateTestChunk(cx, cy);
        }

        var condenser = new TownMapCondenser(design);
        var result = condenser.CondenseWithSpatialSpec(chunks, spatialTransform);

        // Verify dimensions match exactly
        Assert.Equal(gridW, result.Layout.Width);
        Assert.Equal(gridH, result.Layout.Height);
        
        // Verify surface was populated
        Assert.NotNull(result.Layout.Surface);
        Assert.Equal(gridH, result.Layout.Surface.Length);
        Assert.Equal(gridW, result.Layout.Surface[0].Length);
    }

    [Fact]
    public void ComputeGridSize_MinimumIs16()
    {
        var design = CreateTestDesign(keyLocationCount: 1);
        var (w, h) = TownMapCondenser.ComputeGridSize(design);
        Assert.True(w >= 16);
        Assert.True(h >= 16);
    }

    [Fact]
    public void ComputeGridSize_AlignedToChunkBoundary()
    {
        var design = CreateTestDesign(keyLocationCount: 8);
        var (w, h) = TownMapCondenser.ComputeGridSize(design);
        Assert.Equal(0, w % 16);
        Assert.Equal(0, h % 16);
    }

    [Fact]
    public void ComputeGridSize_ScalesWithLocations()
    {
        var small = CreateTestDesign(keyLocationCount: 2);
        var large = CreateTestDesign(keyLocationCount: 10);
        var (wSmall, _) = TownMapCondenser.ComputeGridSize(small);
        var (wLarge, _) = TownMapCondenser.ComputeGridSize(large);
        Assert.True(wLarge >= wSmall);
    }

    [Fact]
    public void BuildSurface_CorrectDimensions()
    {
        var rng = new Random(42);
        var surface = TownMapCondenser.BuildSurface(32, 16, rng);
        Assert.Equal(16, surface.Length);
        Assert.Equal(32, surface[0].Length);
    }

    [Fact]
    public void SnapRoads_Grid_ProducesGridPattern()
    {
        var rng = new Random(42);
        var roads = TownMapCondenser.SnapRoads(16, 16, "grid", rng);
        Assert.NotEmpty(roads);
        Assert.All(roads, r =>
        {
            Assert.InRange(r.X, 0, 15);
            Assert.InRange(r.Y, 0, 15);
        });
    }

    [Fact]
    public void SnapRoads_Linear_HasHorizontalAndVertical()
    {
        var rng = new Random(42);
        var roads = TownMapCondenser.SnapRoads(16, 16, "linear", rng);
        // Should have full horizontal and vertical lines
        Assert.Contains(roads, r => r.Y == 8); // horizontal through middle
        Assert.Contains(roads, r => r.X == 8); // vertical through middle
    }

    [Fact]
    public void SnapRoads_Organic_ProducesRoads()
    {
        var rng = new Random(42);
        var roads = TownMapCondenser.SnapRoads(32, 32, "organic", rng);
        Assert.NotEmpty(roads);
    }

    [Fact]
    public void PlaceLandmark_AtCentre()
    {
        var landmark = new LandmarkBuilding("Fort Test", "Big fort", "large", "", "", "");
        var occupied = new HashSet<(int, int)>();
        var building = TownMapCondenser.PlaceLandmark(landmark, 16, 16, 0, occupied);

        Assert.Equal("Fort Test", building.Name);
        Assert.Equal("large", building.SizeCategory);
        Assert.Equal(3, building.Floors);
        Assert.NotEmpty(building.Footprint);
        Assert.NotEmpty(occupied);
    }

    [Fact]
    public void PlaceKeyLocations_AllPlaced()
    {
        var locations = new List<KeyLocation>
        {
            new("Shop", "shop", "A shop", "small", "", "", ""),
            new("Inn", "rest", "An inn", "medium", "", "", ""),
        };
        var rng = new Random(42);
        var roadTiles = new List<(int X, int Y)>();
        for (var x = 0; x < 32; x++) roadTiles.Add((x, 16));
        var buildings = new List<PlacedBuilding>();
        var occupied = new HashSet<(int, int)>();

        TownMapCondenser.PlaceKeyLocations(locations, roadTiles, 32, 32, rng, buildings, occupied);

        Assert.Equal(2, buildings.Count);
        Assert.Contains(buildings, b => b.Name == "Shop");
        Assert.Contains(buildings, b => b.Name == "Inn");
    }

    [Fact]
    public void PlaceProps_WithinBounds()
    {
        var rng = new Random(42);
        var occupied = new HashSet<(int, int)>();
        var props = TownMapCondenser.PlaceProps(32, 32, rng, occupied, 70, 30);

        Assert.NotEmpty(props);
        Assert.True(props.Count <= 30);
        Assert.All(props, p =>
        {
            var globalX = p.Placement.ChunkX * 16 + p.Placement.LocalTileX;
            var globalY = p.Placement.ChunkY * 16 + p.Placement.LocalTileY;
            Assert.InRange(globalX, 0, 31);
            Assert.InRange(globalY, 0, 31);
        });
    }

    [Fact]
    public void DefineZones_HasMainZone()
    {
        var design = CreateTestDesign();
        var zones = TownMapCondenser.DefineZones(16, 16, design, 5);

        Assert.NotEmpty(zones);
        Assert.Contains(zones, z => z.Id == "zone_main");
        var main = zones.First(z => z.Id == "zone_main");
        Assert.True(main.IsFastTravelTarget);
    }

    [Fact]
    public void DefineZones_IncludesHazardZones()
    {
        var design = CreateTestDesign();
        var zones = TownMapCondenser.DefineZones(16, 16, design, 5);

        Assert.Contains(zones, z => z.Id.StartsWith("zone_hazard_"));
    }

    [Fact]
    public void HazardBounds_South_ReturnsBottomRow()
    {
        var (sx, sy, ex, ey) = TownMapCondenser.HazardBounds("south-west waterfront", 2, 2);
        Assert.Equal(0, sx);
        Assert.Equal(1, sy);
        Assert.Equal(1, ex);
        Assert.Equal(1, ey);
    }

    [Fact]
    public void HazardBounds_North_ReturnsTopRow()
    {
        var (sx, sy, ex, ey) = TownMapCondenser.HazardBounds("north district", 2, 2);
        Assert.Equal(0, sx);
        Assert.Equal(0, sy);
        Assert.Equal(1, ex);
        Assert.Equal(0, ey);
    }

    [Fact]
    public void Condense_FullPipeline_ProducesValidResult()
    {
        var town = CreateTestTown();
        var design = CreateTestDesign();
        var region = CreateMinimalRegion();
        var condenser = new TownMapCondenser(design);

        var result = condenser.Condense(town, design, region, 42);

        Assert.NotNull(result.Layout);
        Assert.True(result.Layout.Width >= 16);
        Assert.True(result.Layout.Height >= 16);
        Assert.Equal(0, result.Layout.Width % 16);

        // Landmark + key locations should all be placed
        Assert.True(result.Buildings.Count >= 4); // 1 landmark + 3 key + generics
        Assert.Contains(result.Buildings, b => b.Name == "Fort Test");

        Assert.NotEmpty(result.Props);
        Assert.NotEmpty(result.Zones);
    }

    [Fact]
    public void Condense_DeterministicWithSameSeed()
    {
        var town = CreateTestTown();
        var design = CreateTestDesign();
        var region = CreateMinimalRegion();

        var r1 = new TownMapCondenser(design).Condense(town, design, region, 42);
        var r2 = new TownMapCondenser(design).Condense(town, design, region, 42);

        Assert.Equal(r1.Layout.Width, r2.Layout.Width);
        Assert.Equal(r1.Buildings.Count, r2.Buildings.Count);
        Assert.Equal(r1.Props.Count, r2.Props.Count);
    }

    [Fact]
    public void Condense_DifferentSeeds_ProduceDifferentProps()
    {
        var town = CreateTestTown();
        var design = CreateTestDesign();
        var region = CreateMinimalRegion();

        var r1 = new TownMapCondenser(design).Condense(town, design, region, 1);
        var r2 = new TownMapCondenser(design).Condense(town, design, region, 999);

        // Props should differ (rotation/position) even if counts happen to match
        if (r1.Props.Count > 0 && r2.Props.Count > 0)
        {
            var different = r1.Props[0].Rotation != r2.Props[0].Rotation
                         || r1.Props[0].Placement != r2.Props[0].Placement;
            Assert.True(different);
        }
    }

    [Fact]
    public void Condense_AllBuildingsWithinBounds()
    {
        var town = CreateTestTown();
        var design = CreateTestDesign(keyLocationCount: 6);
        var region = CreateMinimalRegion();

        var result = new TownMapCondenser(design).Condense(town, design, region, 42);

        foreach (var b in result.Buildings)
        {
            foreach (var tile in b.Footprint)
            {
                Assert.InRange(tile[0], 0, result.Layout.Width - 1);
                Assert.InRange(tile[1], 0, result.Layout.Height - 1);
            }
        }
    }

    [Fact]
    public void ComputeGridSize_ExplicitMedium32()
    {
        var design = CreateTestDesign(keyLocationCount: 1);
        var parms = new MapGenerationParams { GridSize = GridSizeMode.Medium_32 };
        var (w, h) = TownMapCondenser.ComputeGridSize(design, parms);
        Assert.Equal(32, w);
        Assert.Equal(32, h);
    }

    [Fact]
    public void ComputeGridSize_CustomClampedAndAligned()
    {
        var design = CreateTestDesign(keyLocationCount: 1);
        var parms = new MapGenerationParams { GridSize = GridSizeMode.Custom, CustomGridDimension = 20 };
        var (w, h) = TownMapCondenser.ComputeGridSize(design, parms);
        Assert.Equal(32, w); // 20 rounds up to 2 chunks = 32
    }

    [Fact]
    public void Condense_WithParams_UsesProvidedSeed()
    {
        var town = CreateTestTown();
        var design = CreateTestDesign();
        var region = CreateMinimalRegion();
        var parms = new MapGenerationParams { Seed = 42 };

        var r1 = new TownMapCondenser(design).Condense(town, design, region, parms);
        var r2 = new TownMapCondenser(design).Condense(town, design, region, parms);

        Assert.Equal(r1.Buildings.Count, r2.Buildings.Count);
        Assert.Equal(r1.Props.Count, r2.Props.Count);
    }

    [Fact]
    public void Condense_ZeroPropDensity_NoProps()
    {
        var town = CreateTestTown();
        var design = CreateTestDesign();
        var region = CreateMinimalRegion();
        var parms = new MapGenerationParams { Seed = 42, PropDensityPercent = 0, MaxProps = 0 };

        var result = new TownMapCondenser(design).Condense(town, design, region, parms);

        Assert.Empty(result.Props);
    }

    /// <summary>Helper to create a test chunk with dummy tile data.</summary>
    private static TownChunk CreateTestChunk(int chunkX, int chunkY)
    {
        var tileData = new int[16][];
        for (var y = 0; y < 16; y++)
        {
            tileData[y] = new int[16];
            for (var x = 0; x < 16; x++)
                tileData[y][x] = (int)SurfaceType.Grass;
        }
        return new TownChunk(chunkX, chunkY, tileData);
    }
}
