namespace Oravey2.Core.World.Liquids;

/// <summary>
/// Groups contiguous liquid tiles into regions by LiquidType using flood-fill.
/// Tiles with different LiquidType are not grouped together.
/// </summary>
public static class LiquidRegionFinder
{
    /// <summary>
    /// Finds all liquid regions in a chunk's tile data (chunk-local coordinates).
    /// </summary>
    public static List<LiquidRegion> FindRegions(TileMapData tiles)
    {
        var regions = new List<LiquidRegion>();
        var visited = new bool[tiles.Width, tiles.Height];

        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                if (visited[x, y]) continue;

                var tile = tiles.GetTileData(x, y);
                if (!tile.HasWater) continue;

                var region = FloodFill(tiles, x, y, tile.Liquid, visited);
                regions.Add(region);
            }
        }

        return regions;
    }

    private static LiquidRegion FloodFill(
        TileMapData tiles, int startX, int startY,
        LiquidType type, bool[,] visited)
    {
        var regionTiles = new List<(int X, int Y)>();
        var shoreTiles = new List<(int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();

        queue.Enqueue((startX, startY));
        visited[startX, startY] = true;

        // Use the water level from the first tile as the region surface
        float surfaceY = WaterHelper.GetWaterSurfaceY(tiles.GetTileData(startX, startY));

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            regionTiles.Add((cx, cy));

            // Check if this tile is a shore tile
            if (WaterHelper.IsShore(tiles, cx, cy))
                shoreTiles.Add((cx, cy));

            // 4-directional flood fill — only same liquid type
            foreach (var (nx, ny) in new[] { (cx, cy - 1), (cx + 1, cy), (cx, cy + 1), (cx - 1, cy) })
            {
                if (nx < 0 || nx >= tiles.Width || ny < 0 || ny >= tiles.Height) continue;
                if (visited[nx, ny]) continue;

                var neighbor = tiles.GetTileData(nx, ny);
                if (!neighbor.HasWater) continue;

                // Group by liquid type — different types form separate regions
                if (neighbor.Liquid != type) continue;

                visited[nx, ny] = true;
                queue.Enqueue((nx, ny));
            }
        }

        return new LiquidRegion(type, regionTiles, surfaceY, shoreTiles);
    }
}
