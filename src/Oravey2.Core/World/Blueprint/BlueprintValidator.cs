namespace Oravey2.Core.World.Blueprint;

public sealed record ValidationResult(bool IsValid, ValidationError[] Errors);
public sealed record ValidationError(string Code, string Message, string? Context);

public static class BlueprintValidator
{
    public static ValidationResult Validate(MapBlueprint blueprint)
    {
        var errors = new List<ValidationError>();

        // Dimensions
        if (blueprint.Dimensions.ChunksWide <= 0 || blueprint.Dimensions.ChunksHigh <= 0)
            errors.Add(new("INVALID_DIMENSIONS",
                "ChunksWide and ChunksHigh must be > 0.",
                $"ChunksWide={blueprint.Dimensions.ChunksWide}, ChunksHigh={blueprint.Dimensions.ChunksHigh}"));

        int totalTilesX = blueprint.Dimensions.ChunksWide * ChunkData.Size;
        int totalTilesY = blueprint.Dimensions.ChunksHigh * ChunkData.Size;

        // Terrain regions
        if (blueprint.Terrain.Regions != null)
        {
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

        // Buildings
        if (blueprint.Buildings != null)
        {
            var footprints = new HashSet<(int, int)>();
            foreach (var building in blueprint.Buildings)
            {
                if (string.IsNullOrEmpty(building.MeshAsset))
                    errors.Add(new("MISSING_MESH_ASSET",
                        $"Building '{building.Id}' has empty MeshAsset.",
                        building.Id));

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

        // Zones
        if (blueprint.Zones != null)
        {
            foreach (var zone in blueprint.Zones)
            {
                if (zone.ChunkStartX < 0 || zone.ChunkEndX >= blueprint.Dimensions.ChunksWide ||
                    zone.ChunkStartY < 0 || zone.ChunkEndY >= blueprint.Dimensions.ChunksHigh)
                    errors.Add(new("ZONE_OUT_OF_BOUNDS",
                        $"Zone '{zone.Id}' chunk range exceeds dimensions.",
                        zone.Id));
            }
        }

        // Roads
        if (blueprint.Roads != null)
        {
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

        return new ValidationResult(errors.Count == 0, errors.ToArray());
    }
}
