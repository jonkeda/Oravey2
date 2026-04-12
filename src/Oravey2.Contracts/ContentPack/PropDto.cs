namespace Oravey2.Contracts.ContentPack;

public sealed record PropDto(
    string Id,
    string MeshAsset,
    PlacementDto? Placement,
    float Rotation,
    float Scale,
    bool BlocksWalkability,
    int[][]? Footprint);
