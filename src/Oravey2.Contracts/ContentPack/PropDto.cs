namespace Oravey2.Contracts.ContentPack;

public sealed class PropDto
{
    public string Id { get; set; } = "";
    public string MeshAsset { get; set; } = "";
    public PlacementDto? Placement { get; set; } = null;
    public float Rotation { get; set; } = 0f;
    public float Scale { get; set; } = 0f;
    public bool BlocksWalkability { get; set; } = false;
    public int[][]? Footprint { get; set; } = null;
}
