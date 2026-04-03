namespace Oravey2.Core.World;

public static class BuildingPlacer
{
    /// <summary>
    /// Stamps a building's footprint onto the tile map: sets StructureId and clears Walkable.
    /// </summary>
    public static void ApplyFootprint(TileMapData map, BuildingDefinition building)
    {
        int structureId = building.Id.GetHashCode();
        if (structureId == 0) structureId = 1; // Ensure non-zero

        foreach (var (fx, fy) in building.Footprint)
        {
            if (fx < 0 || fx >= map.Width || fy < 0 || fy >= map.Height)
                continue;

            var existing = map.GetTileData(fx, fy);
            var updated = new TileData(
                existing.Surface,
                existing.HeightLevel,
                existing.WaterLevel,
                structureId,
                existing.Flags & ~TileFlags.Walkable,
                existing.VariantSeed);
            map.SetTileData(fx, fy, updated);
        }
    }

    /// <summary>
    /// Stamps a blocking prop's footprint onto the tile map.
    /// Non-blocking props leave the map unchanged.
    /// </summary>
    public static void ApplyPropFootprint(TileMapData map, PropDefinition prop)
    {
        if (!prop.BlocksWalkability || prop.Footprint is null)
            return;

        foreach (var (fx, fy) in prop.Footprint)
        {
            if (fx < 0 || fx >= map.Width || fy < 0 || fy >= map.Height)
                continue;

            var existing = map.GetTileData(fx, fy);
            var updated = new TileData(
                existing.Surface,
                existing.HeightLevel,
                existing.WaterLevel,
                existing.StructureId != 0 ? existing.StructureId : 1,
                existing.Flags & ~TileFlags.Walkable,
                existing.VariantSeed);
            map.SetTileData(fx, fy, updated);
        }
    }

    /// <summary>
    /// Checks whether a footprint can be placed: all tiles in bounds and no existing StructureId.
    /// </summary>
    public static bool ValidatePlacement(TileMapData map, (int X, int Y)[] footprint)
    {
        foreach (var (fx, fy) in footprint)
        {
            if (fx < 0 || fx >= map.Width || fy < 0 || fy >= map.Height)
                return false;

            if (map.GetTileData(fx, fy).StructureId != 0)
                return false;
        }
        return true;
    }
}
