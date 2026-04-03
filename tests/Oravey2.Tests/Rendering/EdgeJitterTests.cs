using Oravey2.Core.World;
using Oravey2.Core.World.Rendering;

namespace Oravey2.Tests.Rendering;

public class EdgeJitterTests
{
    [Fact]
    public void SamePositionSameSeed_SameDisplacement()
    {
        var (dx1, dz1) = EdgeJitter.GetDisplacement(1.5f, 2.5f, 42);
        var (dx2, dz2) = EdgeJitter.GetDisplacement(1.5f, 2.5f, 42);
        Assert.Equal(dx1, dx2);
        Assert.Equal(dz1, dz2);
    }

    [Fact]
    public void DifferentSeeds_DifferentDisplacement()
    {
        var (dx1, dz1) = EdgeJitter.GetDisplacement(1.5f, 2.5f, 10);
        var (dx2, dz2) = EdgeJitter.GetDisplacement(1.5f, 2.5f, 200);
        // Very unlikely to be exactly equal for different seeds
        Assert.True(dx1 != dx2 || dz1 != dz2);
    }

    [Fact]
    public void DisplacementMagnitude_Within01()
    {
        // Test many positions to ensure magnitude is bounded
        for (byte seed = 0; seed < 50; seed++)
        {
            var (dx, dz) = EdgeJitter.GetDisplacement(seed * 1.1f, seed * 0.7f, seed);
            Assert.InRange(MathF.Abs(dx), 0f, 0.1f);
            Assert.InRange(MathF.Abs(dz), 0f, 0.1f);
        }
    }

    [Fact]
    public void InteriorVertex_NotBorder()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0));

        // Center of tile (0,0) → not a border vertex
        Assert.False(EdgeJitter.IsBorderVertex(map, 1, 1, 0f, 0f));
    }

    [Fact]
    public void BorderVertex_DifferentTypes_IsDisplaced()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0));

        // Make east neighbor different
        map.SetTileData(2, 1, new TileData(SurfaceType.Rock, 1, 0, 0, TileFlags.Walkable, 0));

        // Vertex on east edge of tile (1,1) → border
        Assert.True(EdgeJitter.IsBorderVertex(map, 1, 1, 0.4f, 0f));
    }

    [Fact]
    public void BorderVertex_SameTypes_NotBorder()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0));

        // East neighbor same type → not a border even on edge
        Assert.False(EdgeJitter.IsBorderVertex(map, 1, 1, 0.4f, 0f));
    }

    [Fact]
    public void MapEdge_CountsAsBorder()
    {
        var map = new TileMapData(3, 3);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map.SetTileData(x, y, new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0));

        // Vertex on the north edge of tile (1,0) → out of bounds → border
        Assert.True(EdgeJitter.IsBorderVertex(map, 1, 0, 0f, -0.4f));
    }
}
