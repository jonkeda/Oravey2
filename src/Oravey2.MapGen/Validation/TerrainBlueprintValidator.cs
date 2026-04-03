using Oravey2.Core.World;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Assets;

namespace Oravey2.MapGen.Validation;

public sealed class TerrainBlueprintValidator : IBlueprintValidator
{
    private readonly IAssetRegistry _assets;

    public TerrainBlueprintValidator(IAssetRegistry assets)
    {
        _assets = assets;
    }

    public ValidationResult Validate(MapBlueprint blueprint)
    {
        var errors = new List<ValidationError>();

        ValidateDimensions(blueprint, errors);

        if (blueprint.Dimensions.ChunksWide <= 0 || blueprint.Dimensions.ChunksHigh <= 0)
            return new ValidationResult(false, errors.ToArray());

        int totalTilesX = blueprint.Dimensions.ChunksWide * ChunkData.Size;
        int totalTilesY = blueprint.Dimensions.ChunksHigh * ChunkData.Size;

        ValidateTerrainRegions(blueprint, totalTilesX, totalTilesY, errors);
        ValidateSurfaces(blueprint, errors);
        ValidateBuildings(blueprint, totalTilesX, totalTilesY, errors);
        ValidateBuildingOverlaps(blueprint, errors);
        ValidateRoads(blueprint, totalTilesX, totalTilesY, errors);
        ValidateZones(blueprint, errors);

        return new ValidationResult(errors.Count == 0, errors.ToArray());
    }

    private static void ValidateDimensions(MapBlueprint blueprint, List<ValidationError> errors)
    {
        if (blueprint.Dimensions.ChunksWide <= 0 || blueprint.Dimensions.ChunksHigh <= 0)
            errors.Add(new("INVALID_DIMENSIONS",
                "ChunksWide and ChunksHigh must be > 0.",
                $"ChunksWide={blueprint.Dimensions.ChunksWide}, ChunksHigh={blueprint.Dimensions.ChunksHigh}"));
    }

    private static void ValidateTerrainRegions(MapBlueprint blueprint, int totalTilesX, int totalTilesY, List<ValidationError> errors)
    {
        if (blueprint.Terrain.Regions is null) return;

        foreach (var region in blueprint.Terrain.Regions)
        {
            foreach (var point in region.Polygon)
            {
                if (point.Length < 2) continue;
                if (point[0] < 0 || point[0] >= totalTilesX || point[1] < 0 || point[1] >= totalTilesY)
                    errors.Add(new("REGION_OUT_OF_BOUNDS",
                        $"Region '{region.Id}' has point ({point[0]},{point[1]}) outside map bounds.",
                        region.Id));
            }
        }
    }

    private void ValidateSurfaces(MapBlueprint blueprint, List<ValidationError> errors)
    {
        if (blueprint.Terrain.Surfaces is null) return;

        foreach (var rule in blueprint.Terrain.Surfaces)
        {
            foreach (var alloc in rule.Allocations)
            {
                if (!_assets.Exists("surface", alloc.Surface))
                    errors.Add(new("UNKNOWN_SURFACE",
                        $"Surface type '{alloc.Surface}' in region '{rule.RegionId}' is not in the asset registry.",
                        rule.RegionId));
            }
        }
    }

    private void ValidateBuildings(MapBlueprint blueprint, int totalTilesX, int totalTilesY, List<ValidationError> errors)
    {
        if (blueprint.Buildings is null) return;

        foreach (var building in blueprint.Buildings)
        {
            if (building.TileX < 0 || building.TileY < 0 ||
                building.TileX + building.FootprintWidth > totalTilesX ||
                building.TileY + building.FootprintHeight > totalTilesY)
                errors.Add(new("BUILDING_OUT_OF_BOUNDS",
                    $"Building '{building.Id}' footprint exceeds map bounds.",
                    building.Id));

            if (!string.IsNullOrEmpty(building.MeshAsset) && !_assets.Exists("building", building.MeshAsset))
                errors.Add(new("UNKNOWN_BUILDING_MESH",
                    $"Building '{building.Id}' mesh '{building.MeshAsset}' is not in the asset registry.",
                    building.Id));
        }
    }

    private static void ValidateBuildingOverlaps(MapBlueprint blueprint, List<ValidationError> errors)
    {
        if (blueprint.Buildings is null) return;

        var footprints = new HashSet<(int, int)>();
        foreach (var building in blueprint.Buildings)
        {
            for (int dx = 0; dx < building.FootprintWidth; dx++)
            {
                for (int dy = 0; dy < building.FootprintHeight; dy++)
                {
                    var tile = (building.TileX + dx, building.TileY + dy);
                    if (!footprints.Add(tile))
                        errors.Add(new("BUILDING_OVERLAP",
                            $"Building '{building.Id}' overlaps at ({tile.Item1},{tile.Item2}).",
                            building.Id));
                }
            }
        }
    }

    private static void ValidateRoads(MapBlueprint blueprint, int totalTilesX, int totalTilesY, List<ValidationError> errors)
    {
        if (blueprint.Roads is null) return;

        foreach (var road in blueprint.Roads)
        {
            foreach (var point in road.Path)
            {
                if (point.Length < 2) continue;
                if (point[0] < 0 || point[0] >= totalTilesX || point[1] < 0 || point[1] >= totalTilesY)
                    errors.Add(new("ROAD_OUT_OF_BOUNDS",
                        $"Road '{road.Id}' has point ({point[0]},{point[1]}) outside map bounds.",
                        road.Id));
            }
        }
    }

    private static void ValidateZones(MapBlueprint blueprint, List<ValidationError> errors)
    {
        if (blueprint.Zones is null) return;

        foreach (var zone in blueprint.Zones)
        {
            if (zone.ChunkStartX < 0 || zone.ChunkEndX >= blueprint.Dimensions.ChunksWide ||
                zone.ChunkStartY < 0 || zone.ChunkEndY >= blueprint.Dimensions.ChunksHigh)
                errors.Add(new("ZONE_OUT_OF_BOUNDS",
                    $"Zone '{zone.Id}' chunk range exceeds dimensions.",
                    zone.Id));
        }
    }
}
