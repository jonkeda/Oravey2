using Oravey2.Core.World;
using Oravey2.Core.World.Rendering;

namespace Oravey2.Tests.Rendering;

public class NeighborAnalyzerTests
{
    private static TileMapData CreateUniformMap(int w, int h, SurfaceType surface)
    {
        var map = new TileMapData(w, h);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                map.SetTileData(x, y, new TileData(surface, 1, 0, 0, TileFlags.Walkable, 0));
        return map;
    }

    // --- GetNeighbors ---

    [Fact]
    public void GetNeighbors_CenterTile_AllSameType()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SurfaceType.Dirt, info.Center);
        Assert.Equal(SurfaceType.Dirt, info.N);
        Assert.Equal(SurfaceType.Dirt, info.NE);
        Assert.Equal(SurfaceType.Dirt, info.E);
        Assert.Equal(SurfaceType.Dirt, info.SE);
        Assert.Equal(SurfaceType.Dirt, info.S);
        Assert.Equal(SurfaceType.Dirt, info.SW);
        Assert.Equal(SurfaceType.Dirt, info.W);
        Assert.Equal(SurfaceType.Dirt, info.NW);
    }

    [Fact]
    public void GetNeighbors_EdgeTile_OutOfBoundsAreSentinel()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        var info = NeighborAnalyzer.GetNeighbors(map, 0, 0);

        // Out-of-bounds neighbors should be sentinel (255)
        Assert.Equal((SurfaceType)255, info.N);
        Assert.Equal((SurfaceType)255, info.NW);
        Assert.Equal((SurfaceType)255, info.W);
        Assert.Equal((SurfaceType)255, info.NE);
        Assert.Equal((SurfaceType)255, info.SW);

        // In-bounds neighbors
        Assert.Equal(SurfaceType.Dirt, info.E);
        Assert.Equal(SurfaceType.Dirt, info.S);
        Assert.Equal(SurfaceType.Dirt, info.SE);
    }

    [Fact]
    public void GetNeighbors_MixedSurfaces_CorrectPerDirection()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(1, 0, new TileData(SurfaceType.Asphalt, 1, 0, 0, TileFlags.Walkable, 0)); // North
        map.SetTileData(2, 1, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0));    // East

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);
        Assert.Equal(SurfaceType.Dirt, info.Center);
        Assert.Equal(SurfaceType.Asphalt, info.N);
        Assert.Equal(SurfaceType.Rock, info.E);
        Assert.Equal(SurfaceType.Dirt, info.S);
    }

    // --- GetQuadrantShape: Fill ---

    [Fact]
    public void AllSameSurface_AllQuadrantsFill()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
    }

    // --- GetQuadrantShape: Edge ---

    [Fact]
    public void NorthDifferent_NEandNW_AreEdge()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(1, 0, new TileData(SurfaceType.Asphalt, 1, 0, 0, TileFlags.Walkable, 0)); // N different

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
        // South quadrants unaffected
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
    }

    [Fact]
    public void EastDifferent_NEandSE_AreEdge()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(2, 1, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0)); // E different

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
    }

    [Fact]
    public void SouthDifferent_SEandSW_AreEdge()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(1, 2, new TileData(SurfaceType.Concrete, 1, 0, 0, TileFlags.Walkable, 0)); // S different

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
    }

    [Fact]
    public void WestDifferent_NWandSW_AreEdge()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(0, 1, new TileData(SurfaceType.Sand, 1, 0, 0, TileFlags.Walkable, 0)); // W different

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
    }

    // --- GetQuadrantShape: OuterCorner ---

    [Fact]
    public void NorthAndEastDifferent_NE_IsOuterCorner()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(1, 0, new TileData(SurfaceType.Asphalt, 1, 0, 0, TileFlags.Walkable, 0)); // N
        map.SetTileData(2, 1, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0));     // E

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
    }

    [Fact]
    public void SouthAndEastDifferent_SE_IsOuterCorner()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(1, 2, new TileData(SurfaceType.Asphalt, 1, 0, 0, TileFlags.Walkable, 0)); // S
        map.SetTileData(2, 1, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0));     // E

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
    }

    [Fact]
    public void SouthAndWestDifferent_SW_IsOuterCorner()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(1, 2, new TileData(SurfaceType.Sand, 1, 0, 0, TileFlags.Walkable, 0)); // S
        map.SetTileData(0, 1, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0)); // W

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
    }

    [Fact]
    public void NorthAndWestDifferent_NW_IsOuterCorner()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(1, 0, new TileData(SurfaceType.Asphalt, 1, 0, 0, TileFlags.Walkable, 0)); // N
        map.SetTileData(0, 1, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0));     // W

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
    }

    // --- GetQuadrantShape: InnerCorner ---

    [Fact]
    public void OnlyNEDiagonalDifferent_NE_IsInnerCorner()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(2, 0, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0)); // NE diagonal

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.InnerCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
        // Other quadrants unaffected
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
    }

    [Fact]
    public void OnlySEDiagonalDifferent_SE_IsInnerCorner()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(2, 2, new TileData(SurfaceType.Metal, 1, 0, 0, TileFlags.Walkable, 0)); // SE

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.InnerCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
    }

    [Fact]
    public void OnlySWDiagonalDifferent_SW_IsInnerCorner()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(0, 2, new TileData(SurfaceType.Grass, 1, 0, 0, TileFlags.Walkable, 0)); // SW

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.InnerCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
    }

    [Fact]
    public void OnlyNWDiagonalDifferent_NW_IsInnerCorner()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(0, 0, new TileData(SurfaceType.Sand, 1, 0, 0, TileFlags.Walkable, 0)); // NW

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.InnerCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
    }

    // --- Edge tile (boundary) ---

    [Fact]
    public void CornerTile_OutOfBoundsCountsAsDifferent()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        // (0,0): N, NW, W, NE, SW all out of bounds → different
        var info = NeighborAnalyzer.GetNeighbors(map, 0, 0);

        // NW quadrant: N out-of-bounds (different), W out-of-bounds (different) → OuterCorner
        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
        // NE quadrant: N out-of-bounds (different), E same → Edge
        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
        // SW quadrant: S same, W out-of-bounds (different) → Edge
        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
        // SE quadrant: S same, E same, SE same → Fill
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
    }

    // --- Mixed: one side different + diagonal different on other side ---

    [Fact]
    public void NorthDifferentAndSWDiagonalDifferent_CorrectPerQuadrant()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        map.SetTileData(1, 0, new TileData(SurfaceType.Asphalt, 1, 0, 0, TileFlags.Walkable, 0)); // N
        map.SetTileData(0, 2, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0));     // SW diagonal

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));   // N different, E same → Edge
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));    // S same, E same, SE same → Fill
        Assert.Equal(SubTileShape.InnerCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW)); // S same, W same, SW different → InnerCorner
        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));   // N different, W same → Edge
    }

    // --- Quadrants computed independently ---

    [Fact]
    public void EachQuadrant_IndependentOfOthers()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        // Making N and W different → NW=OuterCorner, NE=Edge, SW=Edge, SE=Fill
        map.SetTileData(1, 0, new TileData(SurfaceType.Asphalt, 1, 0, 0, TileFlags.Walkable, 0)); // N
        map.SetTileData(0, 1, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0));     // W

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
        Assert.Equal(SubTileShape.Edge, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
        Assert.Equal(SubTileShape.Fill, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
    }

    // --- Multiple surface type transitions ---

    [Fact]
    public void MultipleDifferentNeighborTypes_StillCorrectShapes()
    {
        var map = CreateUniformMap(3, 3, SurfaceType.Dirt);
        // Each cardinal has a different surface
        map.SetTileData(1, 0, new TileData(SurfaceType.Asphalt, 1, 0, 0, TileFlags.Walkable, 0)); // N
        map.SetTileData(2, 1, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0));     // E
        map.SetTileData(1, 2, new TileData(SurfaceType.Sand, 1, 0, 0, TileFlags.Walkable, 0));     // S
        map.SetTileData(0, 1, new TileData(SurfaceType.Metal, 1, 0, 0, TileFlags.Walkable, 0));    // W

        var info = NeighborAnalyzer.GetNeighbors(map, 1, 1);

        // All corners: both cardinals different → OuterCorner
        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NE));
        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SE));
        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.SW));
        Assert.Equal(SubTileShape.OuterCorner, NeighborAnalyzer.GetQuadrantShape(info, Quadrant.NW));
    }
}
