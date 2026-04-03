namespace Oravey2.Core.World.Rendering;

public readonly record struct SubTileConfig(
    SubTileShape Shape,
    int RotationDegrees,
    SurfaceType Surface);

public static class SubTileSelector
{
    /// <summary>
    /// Computes the mesh configuration (shape + rotation) for one quadrant of a tile.
    /// </summary>
    public static SubTileConfig GetSubTileConfig(SubTileShape shape, Quadrant quadrant, SurfaceType surface)
    {
        int baseRotation = quadrant switch
        {
            Quadrant.NE => 0,
            Quadrant.SE => 90,
            Quadrant.SW => 180,
            Quadrant.NW => 270,
            _ => 0
        };

        return new SubTileConfig(shape, baseRotation, surface);
    }
}
