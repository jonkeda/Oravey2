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
                map.SetTileData(x, y, TileDataFactory.Ground());

        // Border walls (row 0, row 31, col 0, col 31)
        // except west gate at (0,17)
        for (int i = 0; i < 32; i++)
        {
            map.SetTileData(i, 0, TileDataFactory.Wall());    // top
            map.SetTileData(i, 31, TileDataFactory.Wall());    // bottom
            map.SetTileData(0, i, TileDataFactory.Wall());     // left
            map.SetTileData(31, i, TileDataFactory.Wall());    // right
        }

        // West gate — walkable tiles at (0,17) and (0,18)
        map.SetTileData(0, 17, TileDataFactory.Ground());
        map.SetTileData(0, 18, TileDataFactory.Ground());

        // Road strip (north-south center-left) — tiles (4..5, 2..27)
        for (int x = 4; x <= 5; x++)
            for (int y = 2; y <= 27; y++)
                map.SetTileData(x, y, TileDataFactory.Road());

        // Water/rubble obstacle (non-walkable) — tiles (12..15, 4..7)
        for (int x = 12; x <= 15; x++)
            for (int y = 4; y <= 7; y++)
                map.SetTileData(x, y, TileDataFactory.Water());

        // Ruins building — perimeter at (20..25, 8..13)
        SetBuildingWalls(map, 20, 8, 25, 13);

        return map;
    }

    private static void SetBuildingWalls(TileMapData map, int x1, int y1, int x2, int y2)
    {
        for (int x = x1; x <= x2; x++)
        {
            map.SetTileData(x, y1, TileDataFactory.Wall());
            map.SetTileData(x, y2, TileDataFactory.Wall());
        }
        for (int y = y1; y <= y2; y++)
        {
            map.SetTileData(x1, y, TileDataFactory.Wall());
            map.SetTileData(x2, y, TileDataFactory.Wall());
        }
    }
}
