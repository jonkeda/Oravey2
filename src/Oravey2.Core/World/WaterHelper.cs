namespace Oravey2.Core.World;

public readonly record struct ShoreConfig(
    bool North, bool East, bool South, bool West,
    bool NorthEast, bool SouthEast, bool SouthWest, bool NorthWest);

public static class WaterHelper
{
    public static bool HasWater(TileData tile) => tile.HasWater;

    public static int GetDepth(TileData tile) => tile.WaterDepth;

    public static float GetWaterSurfaceY(TileData tile)
        => tile.WaterLevel * HeightHelper.HeightStep;

    public static bool IsShore(TileMapData map, int x, int y)
    {
        var tile = map.GetTileData(x, y);
        if (!tile.HasWater) return false;

        // Check 4 cardinal neighbors for dry land
        return IsDry(map, x, y - 1) || IsDry(map, x + 1, y) ||
               IsDry(map, x, y + 1) || IsDry(map, x - 1, y);
    }

    public static Direction[] GetShoreDirections(TileMapData map, int x, int y)
    {
        var tile = map.GetTileData(x, y);
        if (!tile.HasWater) return [];

        var dirs = new List<Direction>();
        if (IsDry(map, x, y - 1)) dirs.Add(Direction.North);
        if (IsDry(map, x + 1, y)) dirs.Add(Direction.East);
        if (IsDry(map, x, y + 1)) dirs.Add(Direction.South);
        if (IsDry(map, x - 1, y)) dirs.Add(Direction.West);
        return dirs.ToArray();
    }

    public static ShoreConfig GetShoreConfig(TileMapData map, int x, int y)
    {
        var tile = map.GetTileData(x, y);
        if (!tile.HasWater) return default;

        return new ShoreConfig(
            North: IsDry(map, x, y - 1),
            East: IsDry(map, x + 1, y),
            South: IsDry(map, x, y + 1),
            West: IsDry(map, x - 1, y),
            NorthEast: IsDry(map, x + 1, y - 1),
            SouthEast: IsDry(map, x + 1, y + 1),
            SouthWest: IsDry(map, x - 1, y + 1),
            NorthWest: IsDry(map, x - 1, y - 1));
    }

    /// <summary>
    /// Flood-fills from a water tile to find all connected water tiles (4-directional).
    /// </summary>
    public static HashSet<(int X, int Y)> FindConnectedWater(TileMapData map, int startX, int startY)
    {
        var result = new HashSet<(int, int)>();
        var startTile = map.GetTileData(startX, startY);
        if (!startTile.HasWater) return result;

        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        result.Add((startX, startY));

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (nx, ny) in new[] { (cx, cy - 1), (cx + 1, cy), (cx, cy + 1), (cx - 1, cy) })
            {
                if (result.Contains((nx, ny))) continue;
                if (nx < 0 || nx >= map.Width || ny < 0 || ny >= map.Height) continue;

                var neighbor = map.GetTileData(nx, ny);
                if (neighbor.HasWater)
                {
                    result.Add((nx, ny));
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return result;
    }

    private static bool IsDry(TileMapData map, int x, int y)
    {
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
            return true; // Out of bounds counts as dry (shore edge)
        return !map.GetTileData(x, y).HasWater;
    }
}
