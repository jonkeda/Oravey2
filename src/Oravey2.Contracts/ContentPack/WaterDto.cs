namespace Oravey2.Contracts.ContentPack;

public sealed class WaterDto
{
    public string Id { get; set; } = "";
    public string WaterType { get; set; } = "";
    public float[][] Geometry { get; set; } = [];
}
