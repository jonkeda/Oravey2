using Oravey2.Core.World;
using Oravey2.Core.World.Blueprint;

namespace Oravey2.MapGen.Spatial;

public static class SpatialUtils
{
    public static IReadOnlyList<(string A, string B)> FindOverlaps(
        IReadOnlyList<BuildingFootprint> buildings)
    {
        var overlaps = new List<(string, string)>();

        for (int i = 0; i < buildings.Count; i++)
        {
            for (int j = i + 1; j < buildings.Count; j++)
            {
                if (buildings[i].Overlaps(buildings[j]))
                    overlaps.Add((buildings[i].Id, buildings[j].Id));
            }
        }

        return overlaps;
    }

    public static bool IsTileWithinBounds(
        int chunkX, int chunkY, int localTileX, int localTileY,
        int chunksWide, int chunksHigh, int tilesPerChunk = ChunkData.Size)
    {
        if (chunkX < 0 || chunkX >= chunksWide || chunkY < 0 || chunkY >= chunksHigh)
            return false;

        if (localTileX < 0 || localTileX >= tilesPerChunk || localTileY < 0 || localTileY >= tilesPerChunk)
            return false;

        return true;
    }

    public static bool IsTileOnWater(
        int chunkX, int chunkY, int localTileX, int localTileY,
        WaterBlueprint? water)
    {
        if (water is null) return false;

        int globalX = chunkX * ChunkData.Size + localTileX;
        int globalY = chunkY * ChunkData.Size + localTileY;

        if (water.Rivers is not null)
        {
            foreach (var river in water.Rivers)
            {
                foreach (var point in river.Path)
                {
                    if (point.Length < 2) continue;
                    int halfWidth = river.Width / 2;
                    if (Math.Abs(globalX - point[0]) <= halfWidth &&
                        Math.Abs(globalY - point[1]) <= halfWidth)
                        return true;
                }
            }
        }

        if (water.Lakes is not null)
        {
            foreach (var lake in water.Lakes)
            {
                int dx = globalX - lake.CenterX;
                int dy = globalY - lake.CenterY;
                if (dx * dx + dy * dy <= lake.Radius * lake.Radius)
                    return true;
            }
        }

        return false;
    }

    public static bool IsTileOnBuilding(
        int chunkX, int chunkY, int localTileX, int localTileY,
        IReadOnlyList<BuildingFootprint> buildings)
    {
        int globalX = chunkX * ChunkData.Size + localTileX;
        int globalY = chunkY * ChunkData.Size + localTileY;

        foreach (var building in buildings)
        {
            if (globalX >= building.TileX && globalX < building.TileX + building.Width &&
                globalY >= building.TileY && globalY < building.TileY + building.Height)
                return true;
        }

        return false;
    }

    public static bool IsWalkable(
        int chunkX, int chunkY, int localTileX, int localTileY,
        int chunksWide, int chunksHigh,
        WaterBlueprint? water,
        IReadOnlyList<BuildingFootprint> buildings)
    {
        if (!IsTileWithinBounds(chunkX, chunkY, localTileX, localTileY, chunksWide, chunksHigh))
            return false;

        if (IsTileOnWater(chunkX, chunkY, localTileX, localTileY, water))
            return false;

        if (IsTileOnBuilding(chunkX, chunkY, localTileX, localTileY, buildings))
            return false;

        return true;
    }
}
