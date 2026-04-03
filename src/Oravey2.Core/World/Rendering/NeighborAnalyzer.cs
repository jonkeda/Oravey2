namespace Oravey2.Core.World.Rendering;

public readonly record struct NeighborInfo(
    SurfaceType Center,
    SurfaceType N, SurfaceType NE, SurfaceType E, SurfaceType SE,
    SurfaceType S, SurfaceType SW, SurfaceType W, SurfaceType NW);

public enum Quadrant { NE, SE, SW, NW }

public enum SubTileShape { Fill, Edge, OuterCorner, InnerCorner }

public static class NeighborAnalyzer
{
    /// <summary>
    /// Gathers the surface types of all 8 neighbors plus the center tile.
    /// Out-of-bounds tiles are treated as a sentinel value (SurfaceType)255
    /// so they count as "different" from any real surface type.
    /// </summary>
    public static NeighborInfo GetNeighbors(TileMapData map, int x, int y)
    {
        var center = GetSurface(map, x, y);
        return new NeighborInfo(
            Center: center,
            N: GetSurface(map, x, y - 1),
            NE: GetSurface(map, x + 1, y - 1),
            E: GetSurface(map, x + 1, y),
            SE: GetSurface(map, x + 1, y + 1),
            S: GetSurface(map, x, y + 1),
            SW: GetSurface(map, x - 1, y + 1),
            W: GetSurface(map, x - 1, y),
            NW: GetSurface(map, x - 1, y - 1));
    }

    /// <summary>
    /// Determines the sub-tile shape for a specific quadrant based on neighbor info.
    /// </summary>
    public static SubTileShape GetQuadrantShape(NeighborInfo info, Quadrant quadrant)
    {
        // Each quadrant checks its two adjacent cardinal neighbors and the diagonal between them.
        var (cardinal1, cardinal2, diagonal) = quadrant switch
        {
            Quadrant.NE => (info.N, info.E, info.NE),
            Quadrant.SE => (info.S, info.E, info.SE),
            Quadrant.SW => (info.S, info.W, info.SW),
            Quadrant.NW => (info.N, info.W, info.NW),
            _ => (info.N, info.E, info.NE)
        };

        bool card1Same = cardinal1 == info.Center;
        bool card2Same = cardinal2 == info.Center;
        bool diagSame = diagonal == info.Center;

        return (card1Same, card2Same, diagSame) switch
        {
            (false, false, _) => SubTileShape.OuterCorner,
            (true, false, _) or (false, true, _) => SubTileShape.Edge,
            (true, true, false) => SubTileShape.InnerCorner,
            (true, true, true) => SubTileShape.Fill,
        };
    }

    private static SurfaceType GetSurface(TileMapData map, int x, int y)
    {
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
            return (SurfaceType)255; // Sentinel for out-of-bounds
        return map.GetTileData(x, y).Surface;
    }
}
