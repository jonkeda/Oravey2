namespace Oravey2.Core.World;

/// <summary>
/// Builds the 32×32 wasteland tile map for the Scorched Outskirts.
/// Layout matches M1_Phase3_Combat_Quests.md section 1.2.
/// </summary>
public static class WastelandMapBuilder
{
    public static TileMapData CreateWastelandMap()
    {
        var map = new TileMapData(32, 32);

        // Fill everything with ground first
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 32; y++)
                map.SetTile(x, y, TileType.Ground);

        // Border walls (row 0, row 31, col 0, col 31)
        // except west gate at (0,17)
        for (int i = 0; i < 32; i++)
        {
            map.SetTile(i, 0, TileType.Wall);    // top
            map.SetTile(i, 31, TileType.Wall);    // bottom
            map.SetTile(0, i, TileType.Wall);     // left
            map.SetTile(31, i, TileType.Wall);    // right
        }

        // West gate — walkable tiles at (0,17) and (0,18)
        map.SetTile(0, 17, TileType.Ground);
        map.SetTile(0, 18, TileType.Ground);

        // Road strip (north-south center-left) — tiles (4..5, 2..27)
        for (int x = 4; x <= 5; x++)
            for (int y = 2; y <= 27; y++)
                map.SetTile(x, y, TileType.Road);

        // Water/rubble obstacle (non-walkable) — tiles (12..15, 4..7)
        for (int x = 12; x <= 15; x++)
            for (int y = 4; y <= 7; y++)
                map.SetTile(x, y, TileType.Water);

        // Ruins building — perimeter at (20..25, 8..13)
        SetBuildingWalls(map, 20, 8, 25, 13);

        return map;
    }

    private static void SetBuildingWalls(TileMapData map, int x1, int y1, int x2, int y2)
    {
        for (int x = x1; x <= x2; x++)
        {
            map.SetTile(x, y1, TileType.Wall);
            map.SetTile(x, y2, TileType.Wall);
        }
        for (int y = y1; y <= y2; y++)
        {
            map.SetTile(x1, y, TileType.Wall);
            map.SetTile(x2, y, TileType.Wall);
        }
    }
}
