namespace Oravey2.Contracts.ContentPack;

public sealed class RoadDto
{
    public string Id { get; set; } = "";
    public string RoadClass { get; set; } = "";
    public float[][] Nodes { get; set; } = [];
    public string? FromTown { get; set; } = null;
    public string? ToTown { get; set; } = null;
}
