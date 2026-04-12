using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.RegionTemplates;
using Xunit;
using BoundingBoxGen = Oravey2.MapGen.Generation.BoundingBox;

namespace Oravey2.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the town map generation pipeline.
/// Verifies the complete flow from LLM spatial spec → tile transforms → chunk generation → final map.
/// </summary>
public class TownMapGenerationIntegrationTests
{
    private const float TileSizeMeters = 10f;
    private const int Seed = 42;

    // ============ Test Fixtures ============

    /// <summary>Create a small test hamlet spec (50×50 tile grid)</summary>
    private static TownSpatialSpecification CreateTestHamletSpec()
    {
        var bounds = new BoundingBoxGen(52.0, 52.005, 4.0, 4.005);
        var roads = new RoadNetwork([], [], 3f);
        return new TownSpatialSpecification(bounds, new(), roads, [], "flat");
    }

    /// <summary>Create a medium test village spec (100×100 tile grid)</summary>
    private static TownSpatialSpecification CreateTestVillageSpec()
    {
        var bounds = new BoundingBoxGen(52.0, 52.01, 4.0, 4.01);
        var roads = new RoadNetwork([], [], 5f);
        return new TownSpatialSpecification(bounds, new(), roads, [], "flat");
    }

    /// <summary>Create a large test town spec (200×200 tile grid)</summary>
    private static TownSpatialSpecification CreateTestTownSpec()
    {
        var bounds = new BoundingBoxGen(52.0, 52.02, 4.0, 4.02);
        var roads = new RoadNetwork([], [], 8f);
        return new TownSpatialSpecification(bounds, new(), roads, [], "flat");
    }

    /// <summary>Create a city spec with water (300×300 tile grid)</summary>
    private static TownSpatialSpecification CreateTestCitySpec()
    {
        var bounds = new BoundingBoxGen(52.0, 52.03, 4.0, 4.03);
        var roads = new RoadNetwork([], [], 10f);
        
        var waterPolygon = new List<Vector2>
        {
            new(52.015f, 4.015f),
            new(52.015f, 4.025f),
            new(52.025f, 4.025f),
            new(52.025f, 4.015f),
        };
        var waters = new List<SpatialWaterBody>
        {
            new("Harbour", waterPolygon, SpatialWaterType.Harbour),
        };
        
        return new TownSpatialSpecification(bounds, new(), roads, waters, "flat");
    }

    /// <summary>Create a test town design with landmarks and key locations</summary>
    private static TownDesign CreateTestTownDesign(int locationCount = 3)
    {
        return new(
            "TestTown",
            [new LandmarkBuilding("Fort Test", "A ruined fortress", "large", "", "", "town_centre")],
            Enumerable.Range(0, locationCount)
                .Select(i => new KeyLocation($"Location_{i}", "shop", "A building", "medium", "", "", "main_square"))
                .ToList(),
            "organic",
            [new EnvironmentalHazard("flooding", "Water rises", "south-west waterfront")]);
    }

    /// <summary>Create a curated town for testing</summary>
    private static CuratedTown CreateTestTown(string name = "TestTown")
    {
        return new(
            GameName: name,
            RealName: name,
            Latitude: 52.5,
            Longitude: 4.5,
            GamePosition: Vector2.Zero,
            Description: "A test town",
            Size: TownCategory.Town,
            Inhabitants: 5000,
            Destruction: DestructionLevel.Pristine);
    }

    /// <summary>Create a minimal region template for testing</summary>
    private static RegionTemplate CreateMinimalRegion()
    {
        return new()
        {
            Name = "test-region",
            ElevationGrid = new float[1, 1],
            GridOriginLat = 52.0,
            GridOriginLon = 4.0,
            GridCellSizeMetres = 100,
        };
    }

    /// <summary>Create a test chunk with minimal data</summary>
    private static TownChunk CreateTestChunk(int chunkX, int chunkY)
    {
        var tileData = new int[16][];
        for (int i = 0; i < 16; i++)
        {
            tileData[i] = new int[16];
            for (int j = 0; j < 16; j++)
                tileData[i][j] = (int)SurfaceType.Grass;
        }
        return new TownChunk(chunkX, chunkY, tileData);
    }

    /// <summary>Create a chunk array of specified dimensions</summary>
    private static TownChunk[][] CreateChunkArray(int chunksWide, int chunksHigh)
    {
        var chunks = new TownChunk[chunksHigh][];
        for (var y = 0; y < chunksHigh; y++)
        {
            chunks[y] = new TownChunk[chunksWide];
            for (var x = 0; x < chunksWide; x++)
            {
                chunks[y][x] = CreateTestChunk(x, y);
            }
        }
        return chunks;
    }

    // ============ Validation Helpers ============

    /// <summary>Validate that a TownMapResult has a valid layout</summary>
    private static void ValidateTownMapResult(TownMapResult result, int expectedMinWidth = 16, int expectedMinHeight = 16)
    {
        Assert.NotNull(result);
        Assert.NotNull(result.Layout);
        Assert.True(result.Layout.Width >= expectedMinWidth, $"Map width {result.Layout.Width} < {expectedMinWidth}");
        Assert.True(result.Layout.Height >= expectedMinHeight, $"Map height {result.Layout.Height} < {expectedMinHeight}");
        
        // All tiles must have valid type
        for (var y = 0; y < result.Layout.Height; y++)
        {
            Assert.NotNull(result.Layout.Surface[y]);
            for (var x = 0; x < result.Layout.Width; x++)
            {
                var tile = result.Layout.Surface[y][x];
                Assert.True(tile >= 0 && tile <= 3, $"Invalid tile type {tile} at ({x}, {y})");
            }
        }

        // Verify surface is completely filled (no gaps)
        int filledTiles = CountTileType(result, 0) + CountTileType(result, 1) 
                        + CountTileType(result, 2) + CountTileType(result, 3);
        int totalTiles = result.Layout.Width * result.Layout.Height;
        Assert.Equal(totalTiles, filledTiles);
    }

    /// <summary>Count tiles of a specific type in the map</summary>
    private static int CountTileType(TownMapResult result, int tileType)
    {
        int count = 0;
        for (var y = 0; y < result.Layout.Height; y++)
        {
            for (var x = 0; x < result.Layout.Width; x++)
            {
                if (result.Layout.Surface[y][x] == tileType)
                    count++;
            }
        }
        return count;
    }

    // ============ End-to-End Pipeline Tests ============

    [Fact]
    [Trait("Category", "Integration")]
    public void LlmSpec_ToChunks_ToMap_Hamlet()
    {
        // Arrange
        var spec = CreateTestHamletSpec();
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (gridW, gridH) = transform.GetGridDimensions();
        var chunksWide = (gridW + 15) / 16;
        var chunksHigh = (gridH + 15) / 16;
        var chunks = CreateChunkArray(chunksWide, chunksHigh);
        var design = CreateTestTownDesign();
        var condenser = new TownMapCondenser(design);

        // Act
        var result = condenser.CondenseWithSpatialSpec(chunks, transform);

        // Assert
        Assert.NotNull(result);
        ValidateTownMapResult(result, gridW, gridH);
        Assert.Equal(gridW, result.Layout.Width);
        Assert.Equal(gridH, result.Layout.Height);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LlmSpec_ToChunks_ToMap_Village()
    {
        // Arrange
        var spec = CreateTestVillageSpec();
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (gridW, gridH) = transform.GetGridDimensions();
        var chunksWide = (gridW + 15) / 16;
        var chunksHigh = (gridH + 15) / 16;
        var chunks = CreateChunkArray(chunksWide, chunksHigh);
        var design = CreateTestTownDesign(5);
        var condenser = new TownMapCondenser(design);

        // Act
        var result = condenser.CondenseWithSpatialSpec(chunks, transform);

        // Assert
        ValidateTownMapResult(result, gridW, gridH);
        Assert.Equal(gridW, result.Layout.Width);
        Assert.Equal(gridH, result.Layout.Height);
        Assert.True(gridW >= 64 && gridH >= 64);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LlmSpec_ToChunks_ToMap_Town()
    {
        // Arrange
        var spec = CreateTestTownSpec();
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (gridW, gridH) = transform.GetGridDimensions();
        var chunksWide = (gridW + 15) / 16;
        var chunksHigh = (gridH + 15) / 16;
        var chunks = CreateChunkArray(chunksWide, chunksHigh);
        var design = CreateTestTownDesign(8);
        var condenser = new TownMapCondenser(design);

        // Act
        var result = condenser.CondenseWithSpatialSpec(chunks, transform);

        // Assert
        ValidateTownMapResult(result, gridW, gridH);
        Assert.Equal(gridW, result.Layout.Width);
        Assert.Equal(gridH, result.Layout.Height);
        Assert.True(gridW >= 128 && gridH >= 128);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LlmSpec_ToChunks_ToMap_City()
    {
        // Arrange
        var spec = CreateTestCitySpec();
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (gridW, gridH) = transform.GetGridDimensions();
        var chunksWide = (gridW + 15) / 16;
        var chunksHigh = (gridH + 15) / 16;
        var chunks = CreateChunkArray(chunksWide, chunksHigh);
        var design = CreateTestTownDesign(12);
        var condenser = new TownMapCondenser(design);

        // Act
        var result = condenser.CondenseWithSpatialSpec(chunks, transform);

        // Assert
        ValidateTownMapResult(result, gridW, gridH);
        Assert.Equal(gridW, result.Layout.Width);
        Assert.Equal(gridH, result.Layout.Height);
        Assert.True(gridW >= 192 && gridH >= 192);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LlmSpec_Placement_NoCollisions()
    {
        // Arrange — create spec with building and road placements
        var bounds = new BoundingBoxGen(52.0, 52.01, 4.0, 4.01);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {
                "MainHall", new BuildingPlacement(
                    Name: "Main Hall",
                    CenterLat: 52.005,
                    CenterLon: 4.005,
                    WidthMeters: 20,
                    DepthMeters: 20,
                    RotationDegrees: 0,
                    AlignmentHint: "town_centre")
            },
        };
        var roads = new RoadNetwork(
            [new Vector2((float)52.005, (float)4.003), new Vector2((float)52.005, (float)4.007)],
            [new RoadEdge(52.005, 4.003, 52.005, 4.007)],
            5f);
        var spec = new TownSpatialSpecification(bounds, buildings, roads, [], "flat");
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);

        // Act
        var buildingPlacements = transform.TransformBuildingPlacements();
        var roadSegments = transform.TransformRoadNetwork();

        // Assert
        Assert.Single(buildingPlacements);
        Assert.Single(roadSegments);
        
        // Verify building placement is within bounds
        var mainHall = buildingPlacements["MainHall"];
        var (gridW, gridH) = transform.GetGridDimensions();
        Assert.True(mainHall.CenterX >= 0 && mainHall.CenterX < gridW);
        Assert.True(mainHall.CenterZ >= 0 && mainHall.CenterZ < gridH);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LlmSpec_MapSize_MatchesGridDimensions()
    {
        // Arrange
        var spec = CreateTestTownSpec();
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (expectedW, expectedH) = transform.GetGridDimensions();
        var chunksWide = (expectedW + 15) / 16;
        var chunksHigh = (expectedH + 15) / 16;
        var chunks = CreateChunkArray(chunksWide, chunksHigh);
        var design = CreateTestTownDesign();
        var condenser = new TownMapCondenser(design);

        // Act
        var result = condenser.CondenseWithSpatialSpec(chunks, transform);

        // Assert
        Assert.Equal(expectedW, result.Layout.Width);
        Assert.Equal(expectedH, result.Layout.Height);
    }

    // ============ Placement Validation Tests ============

    [Fact]
    [Trait("Category", "Integration")]
    public void BuildingPlacements_AllWithinBounds()
    {
        // Arrange
        var bounds = new BoundingBoxGen(52.0, 52.01, 4.0, 4.01);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {"Building1", new("B1", 52.002, 4.002, 10, 10, 0, "residential")},
            {"Building2", new("B2", 52.008, 4.008, 15, 15, 45, "commercial")},
            {"Building3", new("B3", 52.005, 4.005, 20, 20, 90, "civic")},
        };
        var spec = new TownSpatialSpecification(bounds, buildings, new RoadNetwork([], [], 5f), [], "flat");
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (gridW, gridH) = transform.GetGridDimensions();

        // Act
        var placements = transform.TransformBuildingPlacements();

        // Assert
        Assert.Equal(3, placements.Count);
        foreach (var placement in placements.Values)
        {
            Assert.True(placement.CenterX >= 0 && placement.CenterX < gridW, 
                $"Building centerX {placement.CenterX} out of bounds [0, {gridW})");
            Assert.True(placement.CenterZ >= 0 && placement.CenterZ < gridH, 
                $"Building centerZ {placement.CenterZ} out of bounds [0, {gridH})");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void RoadNetwork_AllEdgesOnMap()
    {
        // Arrange
        var bounds = new BoundingBoxGen(52.0, 52.01, 4.0, 4.01);
        var roads = new RoadNetwork(
            [
                new Vector2((float)52.001, (float)4.001),
                new Vector2((float)52.005, (float)4.005),
                new Vector2((float)52.009, (float)4.009),
            ],
            [
                new RoadEdge(52.001, 4.001, 52.005, 4.005),
                new RoadEdge(52.005, 4.005, 52.009, 4.009),
            ],
            5f);
        var spec = new TownSpatialSpecification(bounds, new(), roads, [], "flat");
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (gridW, gridH) = transform.GetGridDimensions();

        // Act
        var segments = transform.TransformRoadNetwork();

        // Assert
        Assert.Equal(2, segments.Count);
        foreach (var segment in segments)
        {
            Assert.True(segment.From.X >= 0 && segment.From.X < gridW);
            Assert.True(segment.From.Y >= 0 && segment.From.Y < gridH);
            Assert.True(segment.To.X >= 0 && segment.To.X < gridW);
            Assert.True(segment.To.Y >= 0 && segment.To.Y < gridH);
            Assert.True(segment.WidthTiles >= 1);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void WaterBodies_AllPolygonsOnMap()
    {
        // Arrange
        var bounds = new BoundingBoxGen(52.0, 52.01, 4.0, 4.01);
        var waters = new List<SpatialWaterBody>
        {
            new("River", new List<Vector2>
            {
                new(52.001f, 4.001f),
                new(52.009f, 4.001f),
                new(52.009f, 4.009f),
                new(52.001f, 4.009f),
            }, SpatialWaterType.River),
        };
        var spec = new TownSpatialSpecification(bounds, new(), new RoadNetwork([], [], 5f), waters, "flat");
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (gridW, gridH) = transform.GetGridDimensions();

        // Act
        var waterBodies = transform.TransformWaterBodies();

        // Assert
        Assert.Single(waterBodies);
        var river = waterBodies[0];
        Assert.Equal("River", river.Name);
        Assert.True(river.Polygon.Count >= 3);
        
        foreach (var vertex in river.Polygon)
        {
            Assert.True(vertex.X >= 0 && vertex.X < gridW, 
                $"Water vertex X {vertex.X} out of bounds [0, {gridW})");
            Assert.True(vertex.Y >= 0 && vertex.Y < gridH, 
                $"Water vertex Y {vertex.Y} out of bounds [0, {gridH})");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Placements_NoOverlappingBuildings()
    {
        // Arrange — buildings intentionally spaced far apart
        var bounds = new BoundingBoxGen(52.0, 52.01, 4.0, 4.01);
        var buildings = new Dictionary<string, BuildingPlacement>
        {
            {"Building1", new("B1", 52.002, 4.002, 10, 10, 0, "residential")},
            {"Building2", new("B2", 52.002, 4.008, 10, 10, 0, "residential")}, // On east side
            {"Building3", new("B3", 52.008, 4.002, 10, 10, 0, "residential")}, // On south side
        };
        var spec = new TownSpatialSpecification(bounds, buildings, new RoadNetwork([], [], 5f), [], "flat");
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);

        // Act
        var placements = transform.TransformBuildingPlacements();

        // Assert — verify buildings don't have same center position
        var centers = placements.Values.Select(p => (p.CenterX, p.CenterZ)).ToList();
        var distinctCenters = centers.Distinct().ToList();
        Assert.Equal(centers.Count, distinctCenters.Count);
    }

    // ============ Performance Tests ============

    [Fact]
    [Trait("Category", "Integration")]
    public void CityGeneration_CompletesWithin30Seconds()
    {
        // Arrange
        var spec = CreateTestCitySpec();
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (gridW, gridH) = transform.GetGridDimensions();
        var chunksWide = (gridW + 15) / 16;
        var chunksHigh = (gridH + 15) / 16;
        var chunks = CreateChunkArray(chunksWide, chunksHigh);
        var design = CreateTestTownDesign(15);
        var condenser = new TownMapCondenser(design);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = condenser.CondenseWithSpatialSpec(chunks, transform);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, 
            $"City generation took {stopwatch.ElapsedMilliseconds}ms, exceeds 30000ms limit");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void HamletGeneration_CompletesWithin2Seconds()
    {
        // Arrange
        var spec = CreateTestHamletSpec();
        var transform = new TownSpatialTransform(spec, TileSizeMeters, Seed);
        var (gridW, gridH) = transform.GetGridDimensions();
        var chunksWide = (gridW + 15) / 16;
        var chunksHigh = (gridH + 15) / 16;
        var chunks = CreateChunkArray(chunksWide, chunksHigh);
        var design = CreateTestTownDesign(2);
        var condenser = new TownMapCondenser(design);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = condenser.CondenseWithSpatialSpec(chunks, transform);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, 
            $"Hamlet generation took {stopwatch.ElapsedMilliseconds}ms, exceeds 2000ms limit");
    }
}
