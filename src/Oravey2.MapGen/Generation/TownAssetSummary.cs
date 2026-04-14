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
public sealed class TownAssetSummary
{
    public string TownName { get; set; } = "";
    public string GameName { get; set; } = "";
    public string LayoutStyle { get; set; } = "";
    public List<BuildingAssetEntry> Buildings { get; set; } = [];
    public List<PropAssetEntry> Props { get; set; } = [];
}

/// <summary>
/// A single building in the town asset view, combining design metadata with
/// placement data and current mesh status.
/// </summary>
public sealed class BuildingAssetEntry
{
    public string BuildingId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string SizeCategory { get; set; } = "";
    public string VisualDescription { get; set; } = "";
    public string CurrentMeshPath { get; set; } = "";
    public MeshStatus MeshStatus { get; set; }
    public int Floors { get; set; }
    public float Condition { get; set; }
}

/// <summary>
/// A single prop in the town asset view.
/// </summary>
public sealed class PropAssetEntry
{
    public string PropId { get; set; } = "";
    public string CurrentMeshPath { get; set; } = "";
    public MeshStatus MeshStatus { get; set; }
}
