namespace Oravey2.Core.World;

public enum BuildingSize { Small, Large }

public sealed record BuildingDefinition(
    string Id,
    string Name,
    string MeshAssetPath,
    BuildingSize Size,
    (int X, int Y)[] Footprint,
    int Floors,
    float Condition,
    string? InteriorChunkId
);
