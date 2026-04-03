namespace Oravey2.Core.World.Blueprint;

// --- Core blueprint model ---

public sealed record MapBlueprint(
    string Name,
    string Description,
    BlueprintSource Source,
    BlueprintDimensions Dimensions,
    TerrainBlueprint Terrain,
    WaterBlueprint? Water,
    RoadBlueprint[]? Roads,
    BuildingBlueprint[]? Buildings,
    PropBlueprint[]? Props,
    ZoneBlueprint[]? Zones
);

public sealed record BlueprintSource(string RealWorldLocation, string? Notes);

public sealed record BlueprintDimensions(int ChunksWide, int ChunksHigh);

// --- Terrain ---

public sealed record TerrainBlueprint(
    int BaseElevation,
    TerrainRegion[] Regions,
    SurfaceRule[] Surfaces
);

public sealed record TerrainRegion(
    string Id,
    string Type,
    int[][] Polygon,
    int MinHeight,
    int MaxHeight
);

public sealed record SurfaceRule(
    string RegionId,
    SurfaceAllocation[] Allocations
);

public sealed record SurfaceAllocation(string Surface, int Percent);

// --- Water ---

public sealed record WaterBlueprint(
    RiverBlueprint[]? Rivers,
    LakeBlueprint[]? Lakes
);

public sealed record RiverBlueprint(
    string Id,
    int[][] Path,
    int Width,
    int WaterLevel,
    BridgeBlueprint[]? Bridges
);

public sealed record BridgeBlueprint(int PathIndex, int DeckHeight);

public sealed record LakeBlueprint(
    string Id,
    int CenterX,
    int CenterY,
    int Radius,
    int WaterLevel,
    int DepthAtCenter
);

// --- Roads ---

public sealed record RoadBlueprint(
    string Id,
    int[][] Path,
    int Width,
    string SurfaceType,
    float Condition
);

// --- Buildings ---

public sealed record BuildingBlueprint(
    string Id,
    string Name,
    string MeshAsset,
    string Size,
    int TileX,
    int TileY,
    int FootprintWidth,
    int FootprintHeight,
    int Floors,
    float Condition,
    string? InteriorChunkId
);

// --- Props ---

public sealed record PropBlueprint(
    string Id,
    string MeshAsset,
    int TileX,
    int TileY,
    float Rotation,
    float Scale,
    bool BlocksWalkability,
    int FootprintWidth,
    int FootprintHeight
);

// --- Zones ---

public sealed record ZoneBlueprint(
    string Id,
    string Name,
    string Biome,
    float RadiationLevel,
    int EnemyDifficultyTier,
    bool IsFastTravelTarget,
    int ChunkStartX,
    int ChunkStartY,
    int ChunkEndX,
    int ChunkEndY
);
