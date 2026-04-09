using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.Generation;

public class WildernessChunkGeneratorTests
{
    private static RegionTemplate CreateTestRegion()
    {
        var elevation = new float[10, 10];
        for (int r = 0; r < 10; r++)
            for (int c = 0; c < 10; c++)
                elevation[r, c] = 5f + r * 0.5f;

        return new RegionTemplate
        {
            Name = "TestRegion",
            ElevationGrid = elevation,
            GridOriginLat = 52.50,
            GridOriginLon = 4.95,
            GridCellSizeMetres = 30.0,
            Towns = [],
            Roads = [],
            WaterBodies = [],
            Railways = [],
            LandUseZones =
            [
                new LandUseZone(LandUseType.Forest,
                [
                    new Vector2(0, 0), new Vector2(100, 0),
                    new Vector2(100, 100), new Vector2(0, 100)
                ])
            ]
        };
    }

    [Fact]
    public void SameSeedSameCoords_ProducesIdenticalTileData()
    {
        var region = CreateTestRegion();
        var gen = new WildernessChunkGenerator();

        var chunk1 = gen.Generate(3, 4, 42, region);
        var chunk2 = gen.Generate(3, 4, 42, region);

        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                Assert.Equal(chunk1.Tiles.GetTileData(x, y), chunk2.Tiles.GetTileData(x, y));
    }

    [Fact]
    public void DifferentCoords_ProducesDifferentTileData()
    {
        var region = CreateTestRegion();
        var gen = new WildernessChunkGenerator();

        var chunkA = gen.Generate(0, 0, 42, region);
        var chunkB = gen.Generate(1, 0, 42, region);

        bool anyDifferent = false;
        for (int x = 0; x < ChunkData.Size && !anyDifferent; x++)
            for (int y = 0; y < ChunkData.Size && !anyDifferent; y++)
                if (chunkA.Tiles.GetTileData(x, y) != chunkB.Tiles.GetTileData(x, y))
                    anyDifferent = true;

        Assert.True(anyDifferent, "Adjacent chunks should not have identical tile data");
    }

    [Fact]
    public void HeightLevels_WithinByteRange()
    {
        var region = CreateTestRegion();
        var gen = new WildernessChunkGenerator();

        var chunk = gen.Generate(0, 0, 99, region);

        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
            {
                var tile = chunk.Tiles.GetTileData(x, y);
                Assert.InRange(tile.HeightLevel, (byte)0, (byte)255);
            }
    }
}
