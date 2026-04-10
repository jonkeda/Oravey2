namespace Oravey2.MapGen.Generation;

/// <summary>
/// Describes the mesh status for a building or prop entry.
/// </summary>
public enum MeshStatus
{
    None,
    Primitive,
    Generating,
    Ready,
    Failed,
}

/// <summary>
/// Merged view of a town's design + placed buildings for the assets step.
/// </summary>
public sealed record TownAssetSummary(
    string TownName,
    string GameName,
    string LayoutStyle,
    List<BuildingAssetEntry> Buildings,
    List<PropAssetEntry> Props);

/// <summary>
/// A single building in the town asset view, combining design metadata with
/// placement data and current mesh status.
/// </summary>
public sealed record BuildingAssetEntry(
    string BuildingId,
    string Name,
    string Role,
    string SizeCategory,
    string VisualDescription,
    string CurrentMeshPath,
    MeshStatus MeshStatus,
    int Floors,
    float Condition);

/// <summary>
/// A single prop in the town asset view.
/// </summary>
public sealed record PropAssetEntry(
    string PropId,
    string CurrentMeshPath,
    MeshStatus MeshStatus);
