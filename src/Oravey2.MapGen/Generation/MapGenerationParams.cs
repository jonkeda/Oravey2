namespace Oravey2.MapGen.Generation;

/// <summary>
/// Configurable parameters for town map generation via <see cref="TownMapCondenser"/>.
/// </summary>
public sealed record MapGenerationParams
{
    public GridSizeMode GridSize { get; init; } = GridSizeMode.Auto;
    public int CustomGridDimension { get; init; } = 32;
    public float ScaleFactor { get; init; } = 0.01f;
    public int PropDensityPercent { get; init; } = 70;
    public int MaxProps { get; init; } = 30;
    public int BuildingFillPercent { get; init; } = 40;
    public int? Seed { get; init; }
}

public enum GridSizeMode
{
    Auto,
    Small_16,
    Medium_32,
    Large_48,
    Custom,
}
