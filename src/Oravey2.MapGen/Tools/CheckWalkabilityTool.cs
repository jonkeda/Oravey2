using System.Text.Json;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Spatial;

namespace Oravey2.MapGen.Tools;

public sealed class CheckWalkabilityTool
{
    public string Handle(
        int chunkX, int chunkY, int localTileX, int localTileY,
        int chunksWide, int chunksHigh,
        WaterBlueprint? water,
        BuildingFootprint[] buildings)
    {
        bool withinBounds = SpatialUtils.IsTileWithinBounds(
            chunkX, chunkY, localTileX, localTileY, chunksWide, chunksHigh);

        bool onWater = SpatialUtils.IsTileOnWater(
            chunkX, chunkY, localTileX, localTileY, water);

        bool onBuilding = SpatialUtils.IsTileOnBuilding(
            chunkX, chunkY, localTileX, localTileY, buildings);

        bool walkable = withinBounds && !onWater && !onBuilding;

        return JsonSerializer.Serialize(new
        {
            walkable,
            withinBounds,
            onWater,
            onBuilding
        });
    }
}
