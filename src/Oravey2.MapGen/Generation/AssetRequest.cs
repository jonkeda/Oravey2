namespace Oravey2.MapGen.Generation;

/// <summary>
/// Represents a 3D asset that needs to be generated via Meshy API.
/// </summary>
public sealed record AssetRequest(
    string AssetId,
    string TownName,
    string LocationName,
    string VisualDescription,
    string SizeCategory,
    AssetStatus Status);

public enum AssetStatus { Pending, Generating, Ready, Failed }
