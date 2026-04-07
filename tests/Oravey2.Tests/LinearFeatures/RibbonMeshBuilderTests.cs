using System.Numerics;
using Oravey2.Core.World;
using Oravey2.Core.World.LinearFeatures;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Tests.LinearFeatures;

public class RibbonMeshBuilderTests
{
    private static float[,] CreateFlatHeightGrid(int vertsPerSide, float height = 1f)
    {
        var heights = new float[vertsPerSide, vertsPerSide];
        for (int x = 0; x < vertsPerSide; x++)
            for (int y = 0; y < vertsPerSide; y++)
                heights[x, y] = height;
        return heights;
    }

    [Fact]
    public void StraightRoad_VertexCount_MatchesSamples()
    {
        // Straight road from (5,5) to (25,25) in a 32m chunk
        var feature = new LinearFeature(
            LinearFeatureType.Road, "asphalt", 3f,
            [new LinearFeatureNode(new Vector2(5, 5)), new LinearFeatureNode(new Vector2(25, 25))]);

        int vertsPerSide = 33; // Medium quality
        float chunkWorldSize = 32f;
        var heights = CreateFlatHeightGrid(vertsPerSide);

        var ribbon = RibbonMeshBuilder.Build(feature, heights, vertsPerSide, chunkWorldSize, samplesPerSegment: 8);

        // 1 segment × 8 samples + 1 = 9 spline samples → 18 vertices (2 per sample)
        Assert.Equal(9 * 2, ribbon.Vertices.Length);
    }

    [Fact]
    public void StraightRoad_UVs_TileCorrectly()
    {
        var feature = new LinearFeature(
            LinearFeatureType.Road, "asphalt", 3f,
            [new LinearFeatureNode(new Vector2(5, 5)), new LinearFeatureNode(new Vector2(25, 5))]);

        int vertsPerSide = 33;
        float chunkWorldSize = 32f;
        var heights = CreateFlatHeightGrid(vertsPerSide);

        var ribbon = RibbonMeshBuilder.Build(feature, heights, vertsPerSide, chunkWorldSize);

        // V (arc length) should increase monotonically along the ribbon
        for (int i = 2; i < ribbon.Vertices.Length; i += 2)
        {
            float prevV = ribbon.Vertices[i - 2].TexCoord.Y;
            float currV = ribbon.Vertices[i].TexCoord.Y;
            Assert.True(currV >= prevV,
                $"V coordinate should increase: index {i - 2}={prevV} vs {i}={currV}");
        }

        // U should be 0 (left) and 1 (right) for each pair
        for (int i = 0; i < ribbon.Vertices.Length; i += 2)
        {
            Assert.Equal(0f, ribbon.Vertices[i].TexCoord.X, 0.001f);
            Assert.Equal(1f, ribbon.Vertices[i + 1].TexCoord.X, 0.001f);
        }
    }

    [Fact]
    public void RibbonWidth_MatchesFeatureWidth()
    {
        float expectedWidth = 4f;
        var feature = new LinearFeature(
            LinearFeatureType.Road, "asphalt", expectedWidth,
            [new LinearFeatureNode(new Vector2(10, 16)), new LinearFeatureNode(new Vector2(22, 16))]);

        int vertsPerSide = 33;
        float chunkWorldSize = 32f;
        var heights = CreateFlatHeightGrid(vertsPerSide);

        var ribbon = RibbonMeshBuilder.Build(feature, heights, vertsPerSide, chunkWorldSize);

        // Check width at each vertex pair (left-right distance should match feature width)
        for (int i = 0; i < ribbon.Vertices.Length; i += 2)
        {
            var left = ribbon.Vertices[i].Position;
            var right = ribbon.Vertices[i + 1].Position;
            float width = Vector3.Distance(
                new Vector3(left.X, 0, left.Z),
                new Vector3(right.X, 0, right.Z));
            Assert.InRange(width, expectedWidth * 0.9f, expectedWidth * 1.1f);
        }
    }

    [Fact]
    public void BridgeSegment_VerticesAtOverrideHeight()
    {
        float overrideHeight = 5f;
        var feature = new LinearFeature(
            LinearFeatureType.Road, "concrete", 3f,
            [
                new LinearFeatureNode(new Vector2(5, 16), overrideHeight),
                new LinearFeatureNode(new Vector2(27, 16), overrideHeight),
            ]);

        int vertsPerSide = 33;
        float chunkWorldSize = 32f;
        var heights = CreateFlatHeightGrid(vertsPerSide, height: 1f);

        var ribbon = RibbonMeshBuilder.Build(feature, heights, vertsPerSide, chunkWorldSize);

        // All vertices should be at the override height, not terrain height
        foreach (var v in ribbon.Vertices)
        {
            Assert.Equal(overrideHeight, v.Position.Y, 0.01f);
        }
    }

    [Fact]
    public void FlatTerrain_RibbonVertices_AreAboveTerrain()
    {
        float terrainHeight = 2f;
        var feature = new LinearFeature(
            LinearFeatureType.Road, "asphalt", 3f,
            [new LinearFeatureNode(new Vector2(8, 16)), new LinearFeatureNode(new Vector2(24, 16))]);

        int vertsPerSide = 33;
        float chunkWorldSize = 32f;
        var heights = CreateFlatHeightGrid(vertsPerSide, terrainHeight);

        var ribbon = RibbonMeshBuilder.Build(feature, heights, vertsPerSide, chunkWorldSize);

        foreach (var v in ribbon.Vertices)
        {
            Assert.True(v.Position.Y >= terrainHeight,
                $"Ribbon vertex Y={v.Position.Y} should be >= terrain height {terrainHeight}");
        }
    }

    [Fact]
    public void EmptyFeature_ProducesEmptyMesh()
    {
        var feature = new LinearFeature(
            LinearFeatureType.Road, "asphalt", 3f,
            [new LinearFeatureNode(new Vector2(10, 10))]);

        int vertsPerSide = 33;
        float chunkWorldSize = 32f;
        var heights = CreateFlatHeightGrid(vertsPerSide);

        var ribbon = RibbonMeshBuilder.Build(feature, heights, vertsPerSide, chunkWorldSize);

        Assert.Empty(ribbon.Vertices);
        Assert.Empty(ribbon.Indices);
    }

    [Fact]
    public void IndexBuffer_HasCorrectTriangleCount()
    {
        var feature = new LinearFeature(
            LinearFeatureType.Road, "asphalt", 3f,
            [new LinearFeatureNode(new Vector2(5, 5)), new LinearFeatureNode(new Vector2(25, 25))]);

        int vertsPerSide = 33;
        float chunkWorldSize = 32f;
        var heights = CreateFlatHeightGrid(vertsPerSide);

        var ribbon = RibbonMeshBuilder.Build(feature, heights, vertsPerSide, chunkWorldSize, samplesPerSegment: 8);

        int sampleCount = ribbon.Vertices.Length / 2;
        int expectedQuads = sampleCount - 1;
        Assert.Equal(expectedQuads * 6, ribbon.Indices.Length);
    }
}
