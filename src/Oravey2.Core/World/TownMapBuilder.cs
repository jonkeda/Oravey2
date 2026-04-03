namespace Oravey2.Core.World;

/// <summary>
/// Builds the 32×32 town tile map for Haven.
/// Layout matches M1_Phase2_Town.md section 1.2.
/// </summary>
public static class TownMapBuilder
{
    public static TileMapData CreateTownMap()
    {
        var map = new TileMapData(32, 32);

        // Fill everything with ground first
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 32; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());

        // Border walls (row 0, row 31, col 0, col 31)
        for (int i = 0; i < 32; i++)
        {
            map.SetTileData(i, 0, TileDataFactory.Wall());   // top
            map.SetTileData(i, 31, TileDataFactory.Wall());   // bottom — note: design row 0 = tile y=0
            map.SetTileData(0, i, TileDataFactory.Wall());    // left
            map.SetTileData(31, i, TileDataFactory.Wall());   // right
        }

        // Road strip (center) — tiles (8..15, 7..10)
        // Painted before buildings so building walls take priority
        for (int x = 8; x <= 15; x++)
            for (int y = 7; y <= 10; y++)
                map.SetTileData(x, y, TileDataFactory.Road());

        // Elder's House (top-left building) — perimeter at tiles (2..7, 2..7)
        SetBuildingWalls(map, 2, 2, 7, 7);

        // Second building (top-center) — perimeter at tiles (13..18, 2..7)
        SetBuildingWalls(map, 13, 2, 18, 7);

        // Civilian House 1 (bottom-left) — perimeter at tiles (2..7, 12..17)
        SetBuildingWalls(map, 2, 12, 7, 17);

        // Civilian House 2 (bottom-right) — perimeter at tiles (25..30, 12..17)
        SetBuildingWalls(map, 25, 12, 30, 17);

        // Gate tiles at east exit (30, 17) and (30, 18) — walkable ground
        // Already ground from initial fill; ensure they're not overwritten
        map.SetTileData(30, 17, TileDataFactory.Ground());
        map.SetTileData(30, 18, TileDataFactory.Ground());

        return map;
    }

    /// <summary>
    /// Sets the perimeter of a rectangular region to Wall tiles.
    /// Interior tiles remain as-is (Ground).
    /// </summary>
    private static void SetBuildingWalls(TileMapData map, int x1, int y1, int x2, int y2)
    {
        for (int x = x1; x <= x2; x++)
        {
            map.SetTileData(x, y1, TileDataFactory.Wall()); // top edge
            map.SetTileData(x, y2, TileDataFactory.Wall()); // bottom edge
        }
        for (int y = y1; y <= y2; y++)
        {
            map.SetTileData(x1, y, TileDataFactory.Wall()); // left edge
            map.SetTileData(x2, y, TileDataFactory.Wall()); // right edge
        }
    }
}
