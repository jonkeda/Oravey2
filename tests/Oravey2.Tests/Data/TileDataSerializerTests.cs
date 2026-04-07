using System.Runtime.CompilerServices;
using Oravey2.Core.Data;
using Oravey2.Core.World;

namespace Oravey2.Tests.Data;

public class TileDataSerializerTests
{
    [Fact]
    public void SerializeGrid_ThenDeserialize_RoundTrips()
    {
        const int size = 16;
        var grid = new TileData[size, size];
        var rng = new Random(123);

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                grid[x, y] = new TileData(
                    Surface: (SurfaceType)(rng.Next(8)),
                    HeightLevel: (byte)rng.Next(256),
                    WaterLevel: (byte)rng.Next(256),
                    StructureId: rng.Next(1000),
                    Flags: (TileFlags)rng.Next(256),
                    VariantSeed: (byte)rng.Next(256),
                    Liquid: (LiquidType)(rng.Next(9)),
                    HalfCover: (CoverEdges)(rng.Next(16)),
                    FullCover: (CoverEdges)(rng.Next(16)));

        var compressed = TileDataSerializer.SerializeTileGrid(grid);
        var result = TileDataSerializer.DeserializeTileGrid(compressed, size, size);

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                Assert.Equal(grid[x, y], result[x, y]);
    }

    [Fact]
    public void SerializeGrid_CompressedSize_SmallerThanRaw()
    {
        const int size = 16;
        var grid = new TileData[size, size];

        // Fill with repetitive data — compresses well
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                grid[x, y] = new TileData(
                    Surface: SurfaceType.Grass,
                    HeightLevel: 10,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: 0);

        var compressed = TileDataSerializer.SerializeTileGrid(grid);
        int rawSize = size * size * Unsafe.SizeOf<TileData>();

        Assert.True(compressed.Length < rawSize,
            $"Compressed {compressed.Length} bytes should be smaller than raw {rawSize} bytes");
    }

    [Fact]
    public void SerializeGrid_SingleTileGrid_Works()
    {
        var grid = new TileData[1, 1];
        grid[0, 0] = new TileData(
            Surface: SurfaceType.Rock,
            HeightLevel: 42,
            WaterLevel: 10,
            StructureId: 99,
            Flags: TileFlags.Walkable | TileFlags.Destructible,
            VariantSeed: 7,
            Liquid: LiquidType.Toxic);

        var compressed = TileDataSerializer.SerializeTileGrid(grid);
        var result = TileDataSerializer.DeserializeTileGrid(compressed, 1, 1);

        Assert.Equal(grid[0, 0], result[0, 0]);
    }
}
