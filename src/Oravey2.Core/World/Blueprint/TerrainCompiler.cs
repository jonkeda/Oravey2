namespace Oravey2.Core.World.Blueprint;

public static class TerrainCompiler
{
    /// <summary>
    /// Compiles terrain regions into a flat TileData grid.
    /// </summary>
    public static TileData[,] Compile(MapBlueprint blueprint)
    {
        int width = blueprint.Dimensions.ChunksWide * ChunkData.Size;
        int height = blueprint.Dimensions.ChunksHigh * ChunkData.Size;
        var grid = new TileData[width, height];

        byte baseElevation = (byte)Math.Clamp(blueprint.Terrain.BaseElevation, 0, 255);

        // 1. Initialize all tiles at base elevation with default Dirt surface
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = new TileData(SurfaceType.Dirt, baseElevation, 0, 0, TileFlags.Walkable,
                    ComputeVariantSeed(x, y));

        // 2. Apply elevation regions
        foreach (var region in blueprint.Terrain.Regions)
        {
            var polygon = region.Polygon.Select(p => (p[0], p[1])).ToArray();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!PointInPolygon(x, y, polygon))
                        continue;

                    // Compute height within region range using deterministic hash
                    int range = region.MaxHeight - region.MinHeight;
                    byte regionHeight = range > 0
                        ? (byte)Math.Clamp(region.MinHeight + (HashPosition(x, y) % (range + 1)), 0, 255)
                        : (byte)Math.Clamp(region.MinHeight, 0, 255);

                    var current = grid[x, y];
                    grid[x, y] = new TileData(current.Surface, regionHeight, current.WaterLevel,
                        current.StructureId, current.Flags, current.VariantSeed);
                }
            }
        }

        // 3. Apply surface rules
        foreach (var rule in blueprint.Terrain.Surfaces)
        {
            var region = blueprint.Terrain.Regions.FirstOrDefault(r => r.Id == rule.RegionId);
            if (region == null) continue;

            var polygon = region.Polygon.Select(p => (p[0], p[1])).ToArray();
            var allocations = BuildAllocationTable(rule.Allocations);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!PointInPolygon(x, y, polygon))
                        continue;

                    var surface = PickSurface(allocations, HashPosition(x, y));
                    var current = grid[x, y];
                    grid[x, y] = new TileData(surface, current.HeightLevel, current.WaterLevel,
                        current.StructureId, current.Flags, current.VariantSeed);
                }
            }
        }

        return grid;
    }

    internal static byte ComputeVariantSeed(int x, int y)
        => (byte)((x * 73856093 ^ y * 19349669) & 0xFF);

    internal static int HashPosition(int x, int y)
    {
        int h = x * 73856093 ^ y * 19349669;
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return Math.Abs(h);
    }

    internal static bool PointInPolygon(int px, int py, (int X, int Y)[] polygon)
    {
        if (polygon.Length < 3) return false;

        bool inside = false;
        int n = polygon.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var (xi, yi) = polygon[i];
            var (xj, yj) = polygon[j];

            if (((yi > py) != (yj > py)) &&
                (px < (xj - xi) * (py - yi) / (double)(yj - yi) + xi))
                inside = !inside;
        }
        return inside;
    }

    private static (SurfaceType Surface, int CumulativePercent)[] BuildAllocationTable(SurfaceAllocation[] allocations)
    {
        var table = new List<(SurfaceType, int)>();
        int cumulative = 0;
        foreach (var alloc in allocations)
        {
            if (Enum.TryParse<SurfaceType>(alloc.Surface, true, out var surface))
            {
                cumulative += alloc.Percent;
                table.Add((surface, cumulative));
            }
        }
        return table.ToArray();
    }

    private static SurfaceType PickSurface((SurfaceType Surface, int CumulativePercent)[] table, int hash)
    {
        if (table.Length == 0) return SurfaceType.Dirt;
        int roll = hash % 100;
        foreach (var (surface, cumPercent) in table)
        {
            if (roll < cumPercent)
                return surface;
        }
        return table[^1].Surface;
    }
}
