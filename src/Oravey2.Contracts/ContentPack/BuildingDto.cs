namespace Oravey2.Contracts.ContentPack;

public sealed class BuildingDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string MeshAsset { get; set; } = "";
    public string Size { get; set; } = "";
    public int[][]? Footprint { get; set; } = null;
    public int Floors { get; set; } = 0;
    public float Condition { get; set; } = 0f;
    public string? InteriorChunkId { get; set; } = null;
    public PlacementDto? Placement { get; set; } = null;
}
