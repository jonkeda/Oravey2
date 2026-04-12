namespace Oravey2.MapGen.Generation;

public sealed record TownMapResult(
    TownLayout Layout,
    List<PlacedBuilding> Buildings,
    List<PlacedProp> Props,
    List<TownZone> Zones);

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
