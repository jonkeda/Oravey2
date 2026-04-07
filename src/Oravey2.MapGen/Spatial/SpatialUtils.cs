using Oravey2.Core.World;

namespace Oravey2.MapGen.Spatial;

public static class SpatialUtils
{
    public static bool IsTileWithinBounds(
        int chunkX, int chunkY, int localTileX, int localTileY,
        int chunksWide, int chunksHigh, int tilesPerChunk = ChunkData.Size)
    {
        if (chunkX < 0 || chunkX >= chunksWide || chunkY < 0 || chunkY >= chunksHigh)
            return false;

        if (localTileX < 0 || localTileX >= tilesPerChunk || localTileY < 0 || localTileY >= tilesPerChunk)
            return false;

        return true;
    }
}
