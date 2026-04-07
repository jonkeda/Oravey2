using System.Numerics;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Core.World.Vegetation;

/// <summary>
/// Places tree spawns on tiles that have the <see cref="TileFlags.Forested"/> flag.
/// Uses the tile's VariantSeed for deterministic pseudo-random placement.
/// </summary>
public static class TreePlacementHelper
{
    /// <summary>Average number of trees per forested tile.</summary>
    private const int TreesPerTile = 2;

    /// <summary>
    /// Generates tree spawns for all forested tiles in a chunk.
    /// Positions are in chunk-local world space.
    /// </summary>
    public static IReadOnlyList<TreeSpawn> GenerateForChunk(TileMapData tiles)
    {
        var spawns = new List<TreeSpawn>();
        int size = tiles.Width;
        float tileWorldSize = HeightmapMeshGenerator.TileWorldSize;

        for (int tx = 0; tx < size; tx++)
        {
            for (int ty = 0; ty < size; ty++)
            {
                var tile = tiles.GetTileData(tx, ty);
                if (!tile.Flags.HasFlag(TileFlags.Forested))
                    continue;

                int seed = tile.VariantSeed ^ (tx * 31 + ty * 17);
                int treeCount = 1 + (seed % TreesPerTile);

                for (int i = 0; i < treeCount; i++)
                {
                    // Deterministic pseudo-random within the tile (simple hash)
                    int hash = unchecked((seed * 1664525 + 1013904223) ^ (i * 214013 + 2531011));
                    hash = hash & 0x7FFFFFFF; // ensure positive

                    float fx = (hash % 1000) / 1000f;
                    float fz = ((hash / 1000) % 1000) / 1000f;

                    float worldX = (tx + fx) * tileWorldSize;
                    float worldZ = (ty + fz) * tileWorldSize;

                    var species = (TreeSpecies)((hash / 1000000) % 7);
                    byte growth = (byte)(128 + (hash % 128)); // 128–255 range
                    bool isDead = (hash % 10) == 0; // ~10% dead trees

                    spawns.Add(new TreeSpawn(
                        new Vector2(worldX, worldZ),
                        species,
                        growth,
                        isDead));
                }
            }
        }

        return spawns;
    }
}
