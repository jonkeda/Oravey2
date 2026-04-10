using System.Numerics;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.Tests.Pipeline;

public class OverworldGeneratorTests
{
    private static RegionTemplate MakeRegion(
        List<RoadSegment>? roads = null,
        List<WaterBody>? water = null) => new()
    {
        Name = "test-region",
        ElevationGrid = new float[1, 1],
        GridOriginLat = 0,
        GridOriginLon = 0,
        GridCellSizeMetres = 100,
        Roads = roads ?? [],
        WaterBodies = water ?? [],
    };

    private static CuratedTown MakeTown(string name, float x, float y, int threat = 1) =>
        new(name, name, 0, 0, new Vector2(x, y), "role", "faction", threat, "desc");

    // --- ComputeWorldBounds ---

    [Fact]
    public void ComputeWorldBounds_NoTowns_Returns1x1()
    {
        var (w, h) = OverworldGenerator.ComputeWorldBounds([]);
        Assert.Equal(1, w);
        Assert.Equal(1, h);
    }

    [Fact]
    public void ComputeWorldBounds_SingleTown_AtOrigin()
    {
        var towns = new List<CuratedTown> { MakeTown("a", 0.5f, 0.5f) };
        var (w, h) = OverworldGenerator.ComputeWorldBounds(towns);
        Assert.True(w >= 1);
        Assert.True(h >= 1);
    }

    [Fact]
    public void ComputeWorldBounds_MultipleTowns()
    {
        var towns = new List<CuratedTown>
        {
            MakeTown("a", 0.5f, 0.5f),
            MakeTown("b", 2.5f, 1.5f),
        };
        var (w, h) = OverworldGenerator.ComputeWorldBounds(towns);
        Assert.True(w >= 3); // x=2.5 => need chunk 2
        Assert.True(h >= 2); // y=1.5 => need chunk 1
    }

    // --- GamePosToPlacement ---

    [Fact]
    public void GamePosToPlacement_Origin()
    {
        var p = OverworldGenerator.GamePosToPlacement(new Vector2(0, 0), 2, 2);
        Assert.Equal(0, p.ChunkX);
        Assert.Equal(0, p.ChunkY);
        Assert.Equal(0, p.LocalTileX);
        Assert.Equal(0, p.LocalTileY);
    }

    [Fact]
    public void GamePosToPlacement_Fractional()
    {
        var p = OverworldGenerator.GamePosToPlacement(new Vector2(1.5f, 0.25f), 3, 3);
        Assert.Equal(1, p.ChunkX);
        Assert.Equal(0, p.ChunkY);
        Assert.True(p.LocalTileX >= 0 && p.LocalTileX < 16);
        Assert.True(p.LocalTileY >= 0 && p.LocalTileY < 16);
    }

    // --- FilterRoads ---

    [Fact]
    public void FilterRoads_IncludesNearbyRoads()
    {
        var towns = new List<CuratedTown> { MakeTown("haven", 1.0f, 1.0f) };
        var roads = new List<RoadSegment>
        {
            new(RoadClass.Primary, [new Vector2(1.0f, 1.05f), new Vector2(1.0f, 2.0f)]),
        };

        var result = OverworldGenerator.FilterRoads(roads, towns);
        Assert.Single(result);
        Assert.Equal("haven", result[0].FromTown);
    }

    [Fact]
    public void FilterRoads_ExcludesDistantRoads()
    {
        var towns = new List<CuratedTown> { MakeTown("haven", 1.0f, 1.0f) };
        var roads = new List<RoadSegment>
        {
            new(RoadClass.Residential, [new Vector2(5.0f, 5.0f), new Vector2(6.0f, 6.0f)]),
        };

        var result = OverworldGenerator.FilterRoads(roads, towns);
        Assert.Empty(result);
    }

    [Fact]
    public void FilterRoads_ConnectsTwoTowns()
    {
        var towns = new List<CuratedTown>
        {
            MakeTown("a", 1.0f, 1.0f),
            MakeTown("b", 1.0f, 1.1f),
        };
        var roads = new List<RoadSegment>
        {
            new(RoadClass.Secondary, [new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.1f)]),
        };

        var result = OverworldGenerator.FilterRoads(roads, towns);
        Assert.Single(result);
        Assert.NotNull(result[0].FromTown);
        Assert.NotNull(result[0].ToTown);
        Assert.NotEqual(result[0].FromTown, result[0].ToTown);
    }

    // --- FilterWater ---

    [Fact]
    public void FilterWater_AlwaysIncludesSea()
    {
        var towns = new List<CuratedTown> { MakeTown("haven", 100f, 100f) };
        var water = new List<WaterBody>
        {
            new(WaterType.Sea, [new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1)]),
        };

        var result = OverworldGenerator.FilterWater(water, towns);
        Assert.Single(result);
        Assert.Equal("Sea", result[0].WaterType);
    }

    [Fact]
    public void FilterWater_IncludesNearbyRiver()
    {
        var towns = new List<CuratedTown> { MakeTown("haven", 1.0f, 1.0f) };
        var water = new List<WaterBody>
        {
            new(WaterType.River, [new Vector2(1.0f, 1.1f), new Vector2(1.0f, 2.0f)]),
        };

        var result = OverworldGenerator.FilterWater(water, towns);
        Assert.Single(result);
    }

    [Fact]
    public void FilterWater_ExcludesDistantRiver()
    {
        var towns = new List<CuratedTown> { MakeTown("haven", 1.0f, 1.0f) };
        var water = new List<WaterBody>
        {
            new(WaterType.River, [new Vector2(10.0f, 10.0f), new Vector2(11.0f, 11.0f)]),
        };

        var result = OverworldGenerator.FilterWater(water, towns);
        Assert.Empty(result);
    }

    // --- Full Generate ---

    [Fact]
    public void Generate_ProducesValidResult()
    {
        var towns = new List<CuratedTown>
        {
            MakeTown("haven", 0.5f, 0.5f, threat: 1),
            MakeTown("outpost", 1.5f, 0.5f, threat: 3),
        };
        var roads = new List<RoadSegment>
        {
            new(RoadClass.Primary, [new Vector2(0.5f, 0.5f), new Vector2(1.5f, 0.5f)]),
        };
        var water = new List<WaterBody>
        {
            new(WaterType.Sea, [new Vector2(0, 0), new Vector2(3, 0)]),
        };
        var region = MakeRegion(roads, water);
        var gen = new OverworldGenerator();

        var result = gen.Generate(region, towns, "TestRegion");

        Assert.Equal("TestRegion", result.World.Name);
        Assert.True(result.World.ChunksWide >= 2);
        Assert.Equal(2, result.World.Towns.Count);
        Assert.NotEmpty(result.Roads);
        Assert.NotEmpty(result.Water);
    }

    [Fact]
    public void Generate_PlayerStartsAtLowestThreat()
    {
        var towns = new List<CuratedTown>
        {
            MakeTown("danger", 0.5f, 0.5f, threat: 5),
            MakeTown("safe", 1.5f, 0.5f, threat: 1),
        };
        var region = MakeRegion();
        var gen = new OverworldGenerator();

        var result = gen.Generate(region, towns, "Test");

        // Player start chunk should be near "safe" town (x=1.5 → chunk 1)
        Assert.Equal(1, result.World.PlayerStart.ChunkX);
    }
}
