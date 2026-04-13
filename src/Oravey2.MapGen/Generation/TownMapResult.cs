namespace Oravey2.MapGen.Generation;

public sealed record TownMapResult(
    TownLayout Layout,
    List<PlacedBuilding> Buildings,
    List<PlacedProp> Props,
    List<TownZone> Zones,
    TownSpatialSpecification? SpatialSpec = null,
    string? SpatialSpecJson = null)
{
    /// <summary>
    /// Creates a TownMapResult with automatic serialization of the spatial spec to JSON.
    /// </summary>
    public static TownMapResult CreateWithSerializedSpec(
        TownLayout layout,
        List<PlacedBuilding> buildings,
        List<PlacedProp> props,
        List<TownZone> zones,
        TownSpatialSpecification? spatialSpec = null)
    {
        var serializedJson = spatialSpec != null
            ? SpatialSpecSerializer.SerializeToJson(spatialSpec)
            : null;

        return new TownMapResult(layout, buildings, props, zones, spatialSpec, serializedJson);
    }
}

public sealed record TownLayout(
    int Width,
    int Height,
    int[][] Surface,
    int[][]? Liquid = null);

public sealed record PlacedBuilding(
    string Id,
    string Name,
    string MeshAsset,
    string SizeCategory,
    int[][] Footprint,
    int Floors,
    float Condition,
    TilePlacement Placement);

public sealed record PlacedProp(
    string Id,
    string MeshAsset,
    TilePlacement Placement,
    float Rotation,
    float Scale,
    bool BlocksWalkability);

public sealed record TownZone(
    string Id,
    string Name,
    int Biome,
    float RadiationLevel,
    int EnemyDifficultyTier,
    bool IsFastTravelTarget,
    int ChunkStartX, int ChunkStartY,
    int ChunkEndX, int ChunkEndY);

public sealed record TilePlacement(
    int ChunkX, int ChunkY,
    int LocalTileX, int LocalTileY);
