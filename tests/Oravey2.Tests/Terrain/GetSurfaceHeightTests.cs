using System.Numerics;
using Oravey2.Core.World;
using Oravey2.Core.World.Rendering;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Tests.Terrain;

public class GetSurfaceHeightTests
{
    private static (float[,] Heights, int VertsPerSide, float ChunkWorldSize) CreateFlatHeights(byte heightLevel = 4)
    {
        var tiles = new TileData[ChunkData.Size, ChunkData.Size];
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                tiles[x, y] = new TileData(SurfaceType.Grass, heightLevel, 0, 0, TileFlags.Walkable, 0);

        int tilesPerSide = ChunkData.Size;
        int subdivision = 1;
        int vertsPerSide = tilesPerSide * subdivision + 1;
        float chunkWorldSize = tilesPerSide * HeightmapMeshGenerator.TileWorldSize;

        var heights = HeightmapMeshGenerator.SampleHeights(tiles, null, vertsPerSide, tilesPerSide, subdivision);
        return (heights, vertsPerSide, chunkWorldSize);
    }

    private static (float[,] Heights, int VertsPerSide, float ChunkWorldSize) CreateSlopedHeights()
    {
        var tiles = new TileData[ChunkData.Size, ChunkData.Size];
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                tiles[x, y] = new TileData(SurfaceType.Grass, (byte)(x + 1), 0, 0, TileFlags.Walkable, 0);

        int tilesPerSide = ChunkData.Size;
        int subdivision = 1;
        int vertsPerSide = tilesPerSide * subdivision + 1;
        float chunkWorldSize = tilesPerSide * HeightmapMeshGenerator.TileWorldSize;

        var heights = HeightmapMeshGenerator.SampleHeights(tiles, null, vertsPerSide, tilesPerSide, subdivision);
        return (heights, vertsPerSide, chunkWorldSize);
    }

    [Fact]
    public void FlatTerrain_ReturnsConstantHeight()
    {
        var (heights, vertsPerSide, chunkWorldSize) = CreateFlatHeights(heightLevel: 4);
        float expectedY = 4 * HeightmapMeshGenerator.HeightStep; // 1.0

        // Sample multiple points across the chunk
        var points = new[]
        {
            new Vector2(5f, 5f),
            new Vector2(16f, 16f),
            new Vector2(0.5f, 0.5f),
            new Vector2(31f, 31f),
        };

        foreach (var p in points)
        {
            float y = HeightmapMeshGenerator.GetSurfaceHeight(p, heights, vertsPerSide, chunkWorldSize);
            Assert.Equal(expectedY, y, 0.01f);
        }
    }

    [Fact]
    public void SlopedTerrain_InterpolatesBetweenVertices()
    {
        var (heights, vertsPerSide, chunkWorldSize) = CreateSlopedHeights();

        // Left side (x=0) should be lower than right side (x=31)
        float yLeft = HeightmapMeshGenerator.GetSurfaceHeight(
            new Vector2(1f, 16f), heights, vertsPerSide, chunkWorldSize);
        float yRight = HeightmapMeshGenerator.GetSurfaceHeight(
            new Vector2(31f, 16f), heights, vertsPerSide, chunkWorldSize);

        Assert.True(yRight > yLeft,
            $"Expected right ({yRight:F3}) > left ({yLeft:F3}) for X-sloped terrain");

        // Mid-point should be between left and right
        float yMid = HeightmapMeshGenerator.GetSurfaceHeight(
            new Vector2(16f, 16f), heights, vertsPerSide, chunkWorldSize);
        Assert.True(yMid > yLeft && yMid < yRight,
            $"Expected mid ({yMid:F3}) between left ({yLeft:F3}) and right ({yRight:F3})");
    }

    [Fact]
    public void OutOfBounds_Clamps()
    {
        var (heights, vertsPerSide, chunkWorldSize) = CreateFlatHeights(heightLevel: 4);
        float expectedY = 4 * HeightmapMeshGenerator.HeightStep;

        // Negative coordinates should clamp to edge
        float yNeg = HeightmapMeshGenerator.GetSurfaceHeight(
            new Vector2(-10f, -10f), heights, vertsPerSide, chunkWorldSize);
        Assert.Equal(expectedY, yNeg, 0.01f);

        // Far beyond chunk bounds should clamp to edge
        float yFar = HeightmapMeshGenerator.GetSurfaceHeight(
            new Vector2(100f, 100f), heights, vertsPerSide, chunkWorldSize);
        Assert.Equal(expectedY, yFar, 0.01f);
    }
}
