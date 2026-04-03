namespace Oravey2.Core.World.Rendering;

public sealed class BatchedChunkMeshes
{
    public Dictionary<SurfaceType, List<(int X, int Y, TileData Data)>> Batches { get; } = new();
    public int TotalDrawCalls => Batches.Count;
}

public static class ChunkMeshBatcher
{
    /// <summary>
    /// Groups all non-empty tiles in a chunk by surface type for batched rendering.
    /// Used by the Low quality preset as a fast path.
    /// </summary>
    public static BatchedChunkMeshes BatchChunk(ChunkData chunk, QualitySettings quality)
    {
        var result = new BatchedChunkMeshes();

        if (quality.SubTileAssembly)
            return result; // Sub-tile path handles rendering; return empty batches

        var map = chunk.Tiles;
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var tile = map.GetTileData(x, y);
                if (tile == TileData.Empty)
                    continue;

                if (!result.Batches.TryGetValue(tile.Surface, out var list))
                {
                    list = new List<(int, int, TileData)>();
                    result.Batches[tile.Surface] = list;
                }

                list.Add((x, y, tile));
            }
        }

        return result;
    }
}
