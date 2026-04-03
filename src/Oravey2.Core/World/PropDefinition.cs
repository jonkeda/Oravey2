namespace Oravey2.Core.World;

public sealed record PropDefinition(
    string Id,
    string MeshAssetPath,
    int ChunkX,
    int ChunkY,
    int LocalTileX,
    int LocalTileY,
    float RotationDegrees,
    float Scale,
    bool BlocksWalkability,
    (int X, int Y)[]? Footprint
);
