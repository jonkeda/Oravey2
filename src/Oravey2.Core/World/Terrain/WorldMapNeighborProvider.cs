namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Provides neighbour tiles from a WorldMapData grid for edge stitching.
/// </summary>
public sealed class WorldMapNeighborProvider : IChunkNeighborProvider
{
    private readonly WorldMapData _world;
    private readonly int _chunkX;
    private readonly int _chunkY;

    public WorldMapNeighborProvider(WorldMapData world, int chunkX, int chunkY)
    {
        _world = world;
        _chunkX = chunkX;
        _chunkY = chunkY;
    }

    public TileData GetTileAt(int localX, int localY)
    {
        int cx = _chunkX;
        int cy = _chunkY;

        // Walk into neighbour chunk if out of local range
        while (localX < 0)
        {
            cx--;
            localX += ChunkData.Size;
        }
        while (localX >= ChunkData.Size)
        {
            cx++;
            localX -= ChunkData.Size;
        }
        while (localY < 0)
        {
            cy--;
            localY += ChunkData.Size;
        }
        while (localY >= ChunkData.Size)
        {
            cy++;
            localY -= ChunkData.Size;
        }

        var chunk = _world.GetChunk(cx, cy);
        if (chunk == null)
            return TileData.Empty;

        return chunk.Tiles.GetTileData(localX, localY);
    }
}
