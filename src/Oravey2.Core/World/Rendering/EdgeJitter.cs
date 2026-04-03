namespace Oravey2.Core.World.Rendering;

public static class EdgeJitter
{
    private const float MaxAmplitude = 0.1f;

    /// <summary>
    /// Computes a deterministic 2D displacement for a vertex at the given world position.
    /// Returns a displacement in the XZ plane with magnitude in [0, 0.1].
    /// </summary>
    public static (float Dx, float Dz) GetDisplacement(float worldX, float worldZ, byte variantSeed)
    {
        // Simple deterministic hash combining position and seed
        int hash = Hash(worldX, worldZ, variantSeed);
        float dx = ((hash & 0xFFFF) / 65535f - 0.5f) * 2f * MaxAmplitude;
        float dz = (((hash >> 16) & 0xFFFF) / 65535f - 0.5f) * 2f * MaxAmplitude;
        return (dx, dz);
    }

    /// <summary>
    /// Determines whether a vertex at a local position within a tile is on the border
    /// between two different surface types.
    /// </summary>
    public static bool IsBorderVertex(TileMapData map, int tileX, int tileY,
        float vertexLocalX, float vertexLocalZ)
    {
        var center = map.GetTileData(tileX, tileY).Surface;

        // Check the neighbor in the direction of the vertex offset
        // vertexLocalX/Z are in [-0.5, 0.5] relative to tile center
        int nx = tileX + (vertexLocalX > 0.25f ? 1 : vertexLocalX < -0.25f ? -1 : 0);
        int nz = tileY + (vertexLocalZ > 0.25f ? 1 : vertexLocalZ < -0.25f ? -1 : 0);

        if (nx == tileX && nz == tileY)
            return false; // Interior vertex

        if (nx < 0 || nx >= map.Width || nz < 0 || nz >= map.Height)
            return true; // Edge of map counts as border

        return map.GetTileData(nx, nz).Surface != center;
    }

    private static int Hash(float x, float z, byte seed)
    {
        // Combine bits deterministically
        int ix = BitConverter.SingleToInt32Bits(x);
        int iz = BitConverter.SingleToInt32Bits(z);
        int h = ix * 73856093 ^ iz * 19349669 ^ seed * 83492791;
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }
}
