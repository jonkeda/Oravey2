namespace Oravey2.Core.World;

public sealed class TileMapData
{
    public int Width { get; }
    public int Height { get; }
    public TileType[,] Tiles { get; }

    public TileMapData(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new TileType[width, height];
    }

    public TileType GetTile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return TileType.Empty;
        return Tiles[x, y];
    }

    public void SetTile(int x, int y, TileType type)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
            Tiles[x, y] = type;
    }

    /// <summary>
    /// Returns true if the tile at (x, y) can be walked on.
    /// Out-of-bounds, Empty, Wall, and Water are not walkable.
    /// </summary>
    public bool IsWalkable(int x, int y)
    {
        var tile = GetTile(x, y);
        return tile is TileType.Ground or TileType.Road or TileType.Rubble;
    }

    /// <summary>
    /// Converts a world-space X/Z position to a tile index, given the tile size.
    /// Uses the same centering formula as TileMapRendererScript.
    /// </summary>
    public (int X, int Y) WorldToTile(float worldX, float worldZ, float tileSize = 1f)
    {
        // Inverse of: centerX = (x - Width/2f + 0.5f) * tileSize
        var tx = (int)MathF.Floor(worldX / tileSize + Width / 2f);
        var ty = (int)MathF.Floor(worldZ / tileSize + Height / 2f);
        return (tx, ty);
    }

    /// <summary>
    /// Converts a tile index to a world-space X/Z position (tile centre).
    /// </summary>
    public (float WorldX, float WorldZ) TileToWorld(int x, int y, float tileSize = 1f)
    {
        var wx = (x - Width / 2f + 0.5f) * tileSize;
        var wz = (y - Height / 2f + 0.5f) * tileSize;
        return (wx, wz);
    }

    /// <summary>
    /// Returns true if the world position is on a walkable tile.
    /// </summary>
    public bool IsWalkableAtWorld(float worldX, float worldZ, float tileSize = 1f)
    {
        var (tx, ty) = WorldToTile(worldX, worldZ, tileSize);
        return IsWalkable(tx, ty);
    }

    /// <summary>
    /// Creates a default test map with ground, roads, rubble, and some walls.
    /// </summary>
    public static TileMapData CreateDefault(int width = 16, int height = 16)
    {
        var map = new TileMapData(width, height);
        var rng = new Random(42); // Deterministic for testing

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Border walls
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    map.Tiles[x, y] = TileType.Wall;
                    continue;
                }

                // Road running through the middle
                if (x == width / 2 || y == height / 2)
                {
                    map.Tiles[x, y] = TileType.Road;
                    continue;
                }

                // Random rubble and water patches
                var roll = rng.NextDouble();
                if (roll < 0.1)
                    map.Tiles[x, y] = TileType.Rubble;
                else if (roll < 0.13)
                    map.Tiles[x, y] = TileType.Water;
                else
                    map.Tiles[x, y] = TileType.Ground;
            }
        }

        return map;
    }
}
