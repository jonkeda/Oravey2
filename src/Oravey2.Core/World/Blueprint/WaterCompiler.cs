namespace Oravey2.Core.World.Blueprint;

public static class WaterCompiler
{
    /// <summary>
    /// Applies rivers and lakes to the terrain grid.
    /// </summary>
    public static void CompileWater(TileData[,] grid, WaterBlueprint water)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        if (water.Rivers != null)
        {
            foreach (var river in water.Rivers)
                CompileRiver(grid, river, width, height);
        }

        if (water.Lakes != null)
        {
            foreach (var lake in water.Lakes)
                CompileLake(grid, lake, width, height);
        }
    }

    private static void CompileRiver(TileData[,] grid, RiverBlueprint river, int width, int height)
    {
        var pathTiles = RoadCompiler.InterpolatePath(river.Path);
        int halfWidth = river.Width / 2;
        byte waterLevel = (byte)Math.Clamp(river.WaterLevel, 0, 255);

        // Collect bridge tiles
        var bridgeTiles = new HashSet<(int, int)>();
        if (river.Bridges != null)
        {
            foreach (var bridge in river.Bridges)
            {
                if (bridge.PathIndex >= 0 && bridge.PathIndex < pathTiles.Count)
                {
                    var (bx, by) = pathTiles[bridge.PathIndex];
                    for (int dx = -halfWidth; dx <= halfWidth; dx++)
                        for (int dy = -halfWidth; dy <= halfWidth; dy++)
                            bridgeTiles.Add((bx + dx, by + dy));
                }
            }
        }

        foreach (var (px, py) in pathTiles)
        {
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                for (int dy = -halfWidth; dy <= halfWidth; dy++)
                {
                    int tx = px + dx;
                    int ty = py + dy;
                    if (tx < 0 || tx >= width || ty < 0 || ty >= height)
                        continue;

                    if (bridgeTiles.Contains((tx, ty)))
                    {
                        // Bridge: set walkable at deck height, keep water underneath
                        var bridge = river.Bridges!.First(b =>
                            b.PathIndex >= 0 && b.PathIndex < pathTiles.Count);
                        byte deckHeight = (byte)Math.Clamp(bridge.DeckHeight, 0, 255);
                        var current = grid[tx, ty];
                        grid[tx, ty] = new TileData(
                            SurfaceType.Concrete, deckHeight, waterLevel,
                            current.StructureId, TileFlags.Walkable, current.VariantSeed);
                    }
                    else
                    {
                        // River: carve channel and set water
                        var current = grid[tx, ty];
                        byte channelHeight = (byte)Math.Max(0, waterLevel - 2);
                        grid[tx, ty] = new TileData(
                            SurfaceType.Mud, channelHeight, waterLevel,
                            current.StructureId, TileFlags.None, current.VariantSeed);
                    }
                }
            }
        }
    }

    private static void CompileLake(TileData[,] grid, LakeBlueprint lake, int width, int height)
    {
        byte waterLevel = (byte)Math.Clamp(lake.WaterLevel, 0, 255);
        int r2 = lake.Radius * lake.Radius;

        for (int x = lake.CenterX - lake.Radius; x <= lake.CenterX + lake.Radius; x++)
        {
            for (int y = lake.CenterY - lake.Radius; y <= lake.CenterY + lake.Radius; y++)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                    continue;

                int dx = x - lake.CenterX;
                int dy = y - lake.CenterY;
                if (dx * dx + dy * dy > r2)
                    continue;

                // Bowl shape: center deeper than edges
                float distRatio = MathF.Sqrt(dx * dx + dy * dy) / lake.Radius;
                int depthReduction = (int)(lake.DepthAtCenter * (1f - distRatio));
                byte terrainHeight = (byte)Math.Clamp(waterLevel - depthReduction, 0, 255);

                var current = grid[x, y];
                grid[x, y] = new TileData(
                    SurfaceType.Mud, terrainHeight, waterLevel,
                    current.StructureId, TileFlags.None, current.VariantSeed);
            }
        }
    }
}
