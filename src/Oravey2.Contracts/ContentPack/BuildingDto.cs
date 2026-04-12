namespace Oravey2.Contracts.ContentPack;

public sealed record BuildingDto(
    string Id,
    string Name,
    string MeshAsset,
    string Size,
    int[][]? Footprint,
    int Floors,
    float Condition,
    string? InteriorChunkId,
    PlacementDto? Placement);
