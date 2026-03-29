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
