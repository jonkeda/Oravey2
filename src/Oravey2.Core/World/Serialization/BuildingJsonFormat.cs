namespace Oravey2.Core.World.Serialization;

public sealed record PlacementJson(
    int ChunkX,
    int ChunkY,
    int LocalTileX,
    int LocalTileY
);

public sealed record BuildingJson(
    string Id,
    string Name,
    string MeshAsset,
    string Size,
    int[][] Footprint,
    int Floors,
    float Condition,
    string? InteriorChunkId,
    PlacementJson Placement
);

public sealed record PropJson(
    string Id,
    string MeshAsset,
    PlacementJson Placement,
    float Rotation,
    float Scale,
    bool BlocksWalkability,
    int[][]? Footprint
);
