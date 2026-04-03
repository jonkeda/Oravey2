namespace Oravey2.Core.World.Blueprint;

public static class RoadCompiler
{
    /// <summary>
    /// Carves roads into the terrain grid by modifying tiles along each road path.
    /// </summary>
    public static void CompileRoads(TileData[,] grid, RoadBlueprint[] roads)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        foreach (var road in roads)
        {
            if (road.Path.Length < 2) continue;

            var surfaceType = Enum.TryParse<SurfaceType>(road.SurfaceType, true, out var st)
                ? st : SurfaceType.Asphalt;

            var pathTiles = InterpolatePath(road.Path);
            var halfWidth = road.Width / 2;

            foreach (var (px, py) in pathTiles)
            {
                // Expand to road width
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    for (int dy = -halfWidth; dy <= halfWidth; dy++)
                    {
                        int tx = px + dx;
                        int ty = py + dy;
                        if (tx < 0 || tx >= width || ty < 0 || ty >= height)
                            continue;

                        var current = grid[tx, ty];
                        var surface = ShouldDegrade(road.Condition, tx, ty)
                            ? SurfaceType.Rock  // Rubble
                            : surfaceType;

                        grid[tx, ty] = new TileData(
                            surface,
                            current.HeightLevel,
                            current.WaterLevel,
                            current.StructureId,
                            current.Flags | TileFlags.Walkable,
                            current.VariantSeed);
                    }
                }
            }

            // Smooth height along road center
            SmoothHeightAlongPath(grid, pathTiles, width, height);
        }
    }

    internal static List<(int X, int Y)> InterpolatePath(int[][] path)
    {
        var result = new List<(int, int)>();
        for (int i = 0; i < path.Length - 1; i++)
        {
            int x0 = path[i][0], y0 = path[i][1];
            int x1 = path[i + 1][0], y1 = path[i + 1][1];
            BresenhamLine(x0, y0, x1, y1, result);
        }
        return result;
    }

    private static void BresenhamLine(int x0, int y0, int x1, int y1, List<(int, int)> result)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            if (result.Count == 0 || result[^1] != (x0, y0))
                result.Add((x0, y0));
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static bool ShouldDegrade(float condition, int x, int y)
    {
        if (condition >= 1f) return false;
        int hash = TerrainCompiler.HashPosition(x, y) % 100;
        return hash >= (int)(condition * 100);
    }

    private static void SmoothHeightAlongPath(TileData[,] grid, List<(int X, int Y)> path, int width, int height)
    {
        if (path.Count < 2) return;

        // Linearly interpolate height along path
        byte startH = grid[path[0].X, path[0].Y].HeightLevel;
        byte endH = grid[path[^1].X, path[^1].Y].HeightLevel;

        for (int i = 0; i < path.Count; i++)
        {
            var (px, py) = path[i];
            float t = path.Count > 1 ? (float)i / (path.Count - 1) : 0;
            byte smoothH = (byte)Math.Clamp(startH + (endH - startH) * t, 0, 255);

            var current = grid[px, py];
            grid[px, py] = new TileData(
                current.Surface, smoothH, current.WaterLevel,
                current.StructureId, current.Flags, current.VariantSeed);
        }
    }
}
