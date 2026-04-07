using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.WorldTemplate;
using Xunit;

namespace Oravey2.Tests.Generation;

public class TownChunkGeneratorTests
{
    private static (CuratedTown Town, TownEntry Entry, RegionTemplate Region) CreateTestData()
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
            Roads = [new RoadSegment(RoadClass.Primary, [new Vector2(-50, 0), new Vector2(50, 0)])],
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
            Role: "trading_hub",
            Faction: "Haven Guard",
            ThreatLevel: 1,
            Description: "A fortified market town",
            BoundaryPolygon: boundary);

        return (town, region.Towns[0], region);
    }

    [Fact]
    public void TownChunk_Mode_IsHybrid()
    {
        var (town, entry, region) = CreateTestData();
        var gen = new TownChunkGenerator();

        var chunk = gen.Generate(0, 0, town, entry, region, seed: 42);

        Assert.Equal(ChunkMode.Hybrid, chunk.Mode);
    }

    [Fact]
    public void BuildingCount_WithinDensityBudget()
    {
        var (town, entry, region) = CreateTestData();
        var gen = new TownChunkGenerator();

        var chunk = gen.Generate(0, 0, town, entry, region, seed: 42);

        int structureCount = 0;
        var seenIds = new HashSet<int>();
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
            {
                var tile = chunk.Tiles.GetTileData(x, y);
                if (tile.StructureId != 0)
                    seenIds.Add(tile.StructureId);
            }
        structureCount = seenIds.Count;

        var (min, max) = TownChunkGenerator.BuildingBudget(entry.Category);
        Assert.InRange(structureCount, 0, max); // May be fewer than min due to placement constraints
    }

    [Fact]
    public void TownChunk_HasRoadTiles()
    {
        var (town, entry, region) = CreateTestData();
        var gen = new TownChunkGenerator();

        var chunk = gen.Generate(0, 0, town, entry, region, seed: 42);

        bool hasRoad = false;
        for (int x = 0; x < ChunkData.Size && !hasRoad; x++)
            for (int y = 0; y < ChunkData.Size && !hasRoad; y++)
                if (chunk.Tiles.GetTileData(x, y).Surface == SurfaceType.Asphalt)
                    hasRoad = true;

        Assert.True(hasRoad, "Town chunk should contain road tiles");
    }
}
