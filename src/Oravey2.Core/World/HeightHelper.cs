namespace Oravey2.Core.World;

public enum SlopeType
{
    Flat,
    Gentle,
    Steep,
    Cliff
}

public static class HeightHelper
{
    public const float HeightStep = 0.25f;
    public const int CliffThreshold = 7;

    public static int GetHeightDelta(TileMapData map, int x1, int y1, int x2, int y2)
    {
        var from = map.GetTileData(x1, y1);
        var to = map.GetTileData(x2, y2);
        return to.HeightLevel - from.HeightLevel;
    }

    public static SlopeType GetSlopeType(int delta)
    {
        var abs = Math.Abs(delta);
        if (abs == 0) return SlopeType.Flat;
        if (abs <= 2) return SlopeType.Gentle;
        if (abs < CliffThreshold) return SlopeType.Steep;
        return SlopeType.Cliff;
    }

    public static bool IsPassable(int delta)
        => Math.Abs(delta) < CliffThreshold;

    public static float GetSlopeMovementCost(int delta)
    {
        var abs = Math.Abs(delta);
        if (abs == 0) return 1.0f;
        if (abs <= 2) return 1.0f + abs * 0.25f;
        if (abs < CliffThreshold) return 1.0f + abs * 0.5f;
        return float.PositiveInfinity;
    }

    /// <summary>
    /// Checks line of sight between two tiles using Bresenham's line algorithm.
    /// A unit at height H1 can see over tiles with height &lt; H1.
    /// Obstacles (non-walkable tiles taller than the observer) block sight.
    /// </summary>
    public static bool HasLineOfSight(TileMapData map, int fromX, int fromY, int toX, int toY)
    {
        var fromData = map.GetTileData(fromX, fromY);
        int observerHeight = fromData.HeightLevel;

        int dx = Math.Abs(toX - fromX);
        int dy = Math.Abs(toY - fromY);
        int sx = fromX < toX ? 1 : -1;
        int sy = fromY < toY ? 1 : -1;
        int err = dx - dy;

        int cx = fromX;
        int cy = fromY;

        while (true)
        {
            // Skip start and end tiles — only check intermediate tiles
            if ((cx != fromX || cy != fromY) && (cx != toX || cy != toY))
            {
                var tileData = map.GetTileData(cx, cy);
                // A tile blocks LOS if it's taller than the observer
                if (tileData.HeightLevel > observerHeight)
                    return false;
            }

            if (cx == toX && cy == toY)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                cx += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                cy += sy;
            }
        }

        return true;
    }
}
