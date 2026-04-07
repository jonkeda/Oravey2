namespace Oravey2.Core.World.Liquids;

/// <summary>
/// A contiguous region of liquid tiles sharing the same LiquidType.
/// Produced by <see cref="LiquidRegionFinder.FindRegions"/> using flood-fill.
/// </summary>
public sealed class LiquidRegion
{
    public LiquidType Type { get; }
    public IReadOnlyList<(int X, int Y)> Tiles { get; }
    public float SurfaceY { get; }
    public IReadOnlyList<(int X, int Y)> ShoreTiles { get; }

    public LiquidRegion(
        LiquidType type,
        IReadOnlyList<(int X, int Y)> tiles,
        float surfaceY,
        IReadOnlyList<(int X, int Y)> shoreTiles)
    {
        Type = type;
        Tiles = tiles;
        SurfaceY = surfaceY;
        ShoreTiles = shoreTiles;
    }
}
