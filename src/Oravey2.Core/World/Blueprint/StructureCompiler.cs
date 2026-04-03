namespace Oravey2.Core.World.Blueprint;

public static class StructureCompiler
{
    /// <summary>
    /// Places buildings and props into the grid, returning domain definitions.
    /// </summary>
    public static (BuildingDefinition[] Buildings, PropDefinition[] Props) CompileStructures(
        TileData[,] grid,
        BuildingBlueprint[]? buildings,
        PropBlueprint[]? props)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        var buildingDefs = new List<BuildingDefinition>();
        var propDefs = new List<PropDefinition>();

        if (buildings != null)
        {
            foreach (var b in buildings)
            {
                var footprint = new List<(int X, int Y)>();
                for (int dx = 0; dx < b.FootprintWidth; dx++)
                    for (int dy = 0; dy < b.FootprintHeight; dy++)
                        footprint.Add((b.TileX + dx, b.TileY + dy));

                var size = Enum.TryParse<BuildingSize>(b.Size, true, out var s)
                    ? s : BuildingSize.Small;

                var def = new BuildingDefinition(
                    b.Id, b.Name, b.MeshAsset, size,
                    footprint.ToArray(), b.Floors, b.Condition, b.InteriorChunkId);

                buildingDefs.Add(def);
                BuildingPlacer.ApplyFootprint(GridToMap(grid, width, height), def);

                // Also update the raw grid
                int structureId = b.Id.GetHashCode();
                if (structureId == 0) structureId = 1;
                foreach (var (fx, fy) in footprint)
                {
                    if (fx < 0 || fx >= width || fy < 0 || fy >= height) continue;
                    var current = grid[fx, fy];
                    grid[fx, fy] = new TileData(
                        current.Surface, current.HeightLevel, current.WaterLevel,
                        structureId, current.Flags & ~TileFlags.Walkable, current.VariantSeed);
                }
            }
        }

        if (props != null)
        {
            foreach (var p in props)
            {
                (int X, int Y)[]? footprint = null;
                if (p.BlocksWalkability && p.FootprintWidth > 0 && p.FootprintHeight > 0)
                {
                    var fp = new List<(int, int)>();
                    for (int dx = 0; dx < p.FootprintWidth; dx++)
                        for (int dy = 0; dy < p.FootprintHeight; dy++)
                            fp.Add((p.TileX + dx, p.TileY + dy));
                    footprint = fp.ToArray();

                    foreach (var (fx, fy) in footprint)
                    {
                        if (fx < 0 || fx >= width || fy < 0 || fy >= height) continue;
                        var current = grid[fx, fy];
                        grid[fx, fy] = new TileData(
                            current.Surface, current.HeightLevel, current.WaterLevel,
                            current.StructureId != 0 ? current.StructureId : 1,
                            current.Flags & ~TileFlags.Walkable, current.VariantSeed);
                    }
                }

                // Determine chunk from tile position
                int chunkX = p.TileX / ChunkData.Size;
                int chunkY = p.TileY / ChunkData.Size;
                int localX = p.TileX % ChunkData.Size;
                int localY = p.TileY % ChunkData.Size;

                propDefs.Add(new PropDefinition(
                    p.Id, p.MeshAsset, chunkX, chunkY, localX, localY,
                    p.Rotation, p.Scale, p.BlocksWalkability, footprint));
            }
        }

        return (buildingDefs.ToArray(), propDefs.ToArray());
    }

    private static TileMapData GridToMap(TileData[,] grid, int width, int height)
    {
        var map = new TileMapData(width, height);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                map.SetTileData(x, y, grid[x, y]);
        return map;
    }
}
