namespace Oravey2.MapGen.Generation;

/// <summary>
/// Represents a 3D asset that needs to be generated via Meshy API.
/// </summary>
public sealed class AssetRequest
{
    public string AssetId { get; set; } = "";
    public string TownName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string VisualDescription { get; set; } = "";
    public string SizeCategory { get; set; } = "";
    public AssetStatus Status { get; set; }
}

public enum AssetStatus { Pending, Generating, Ready, Failed }
