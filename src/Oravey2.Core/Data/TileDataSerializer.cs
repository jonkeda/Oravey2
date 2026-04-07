using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Oravey2.Core.World;

namespace Oravey2.Core.Data;

public static class TileDataSerializer
{
    public static byte[] SerializeTileGrid(TileData[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        var flat = new TileData[width * height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                flat[y * width + x] = grid[x, y];

        var bytes = MemoryMarshal.AsBytes(flat.AsSpan());
        return MapCompression.Compress(bytes);
    }

    public static TileData[,] DeserializeTileGrid(byte[] compressed, int width, int height)
    {
        int tileSize = Unsafe.SizeOf<TileData>();
        int expectedBytes = width * height * tileSize;
        var raw = MapCompression.Decompress(compressed, expectedBytes);
        var flat = MemoryMarshal.Cast<byte, TileData>(raw.AsSpan());

        var grid = new TileData[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[x, y] = flat[y * width + x];

        return grid;
    }
}
