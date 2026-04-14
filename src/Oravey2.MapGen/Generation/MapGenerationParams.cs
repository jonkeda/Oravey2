namespace Oravey2.MapGen.Generation;

/// <summary>
/// Configurable parameters for town map generation via <see cref="TownMapCondenser"/>.
/// </summary>
public sealed class MapGenerationParams
{
    public GridSizeMode GridSize { get; set; } = GridSizeMode.Auto;
    public int CustomGridDimension { get; set; } = 32;
    public float ScaleFactor { get; set; } = 0.01f;
    public int PropDensityPercent { get; set; } = 70;
    public int MaxProps { get; set; } = 30;
    public int BuildingFillPercent { get; set; } = 40;
    public int? Seed { get; set; }
}

public enum GridSizeMode
{
    Auto,
    Small_16,
    Medium_32,
    Large_48,
    Custom,
}
