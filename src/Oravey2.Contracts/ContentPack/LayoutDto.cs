namespace Oravey2.Contracts.ContentPack;

public sealed class LayoutDto
{
    public int Width { get; set; } = 0;
    public int Height { get; set; } = 0;
    public int[][]? Surface { get; set; } = null;
    public int[][]? Liquid { get; set; } = null;
}
