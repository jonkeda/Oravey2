using Oravey2.Core.World;
using Oravey2.Core.World.Rendering;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Tests.Terrain;

public class HeightmapMeshGeneratorTests
{
    private static TileData[,] CreateFlatGrid(byte heightLevel = 4, SurfaceType surface = SurfaceType.Grass)
    {
        var tiles = new TileData[ChunkData.Size, ChunkData.Size];
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                tiles[x, y] = new TileData(surface, heightLevel, 0, 0, TileFlags.Walkable, 0);
        return tiles;
    }

    private static TileData[,] CreateSlopedGrid()
    {
        var tiles = new TileData[ChunkData.Size, ChunkData.Size];
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                tiles[x, y] = new TileData(SurfaceType.Grass, (byte)(x + 1), 0, 0, TileFlags.Walkable, 0);
        return tiles;
    }

    [Fact]
    public void FlatChunk_ProducesCorrectVertexCount_Low()
    {
        var tiles = CreateFlatGrid();
        var (vertices, _) = HeightmapMeshGenerator.Generate(tiles, null, QualityPreset.Low);
        Assert.Equal(17 * 17, vertices.Length); // 289
    }

    [Fact]
    public void FlatChunk_ProducesCorrectVertexCount_Medium()
    {
        var tiles = CreateFlatGrid();
        var (vertices, _) = HeightmapMeshGenerator.Generate(tiles, null, QualityPreset.Medium);
        Assert.Equal(33 * 33, vertices.Length); // 1089
    }

    [Fact]
    public void FlatChunk_ProducesCorrectVertexCount_High()
    {
        var tiles = CreateFlatGrid();
        var (vertices, _) = HeightmapMeshGenerator.Generate(tiles, null, QualityPreset.High);
        Assert.Equal(65 * 65, vertices.Length); // 4225
    }

    [Fact]
    public void FlatChunk_AllNormalsPointUp()
    {
        var tiles = CreateFlatGrid();
        var (vertices, _) = HeightmapMeshGenerator.Generate(tiles, null, QualityPreset.Low);

        foreach (var v in vertices)
        {
            // Normal should be approximately (0, 1, 0) for flat terrain
            Assert.InRange(v.Normal.X, -0.01f, 0.01f);
            Assert.InRange(v.Normal.Y, 0.99f, 1.01f);
            Assert.InRange(v.Normal.Z, -0.01f, 0.01f);
        }
    }

    [Fact]
    public void AdjacentFlatChunks_SeamVertices_HaveMatchingPositions()
    {
        var tiles = CreateFlatGrid();

        // Create a neighbor provider that returns the same flat data
        var neighbor = new ConstantNeighborProvider(
            new TileData(SurfaceType.Grass, 4, 0, 0, TileFlags.Walkable, 0));

        var (verticesA, _) = HeightmapMeshGenerator.Generate(tiles, neighbor, QualityPreset.Low);
        var (verticesB, _) = HeightmapMeshGenerator.Generate(tiles, neighbor, QualityPreset.Low);

        // Right edge of chunk A = left edge of chunk B
        int vertsPerSide = 17;
        for (int row = 0; row < vertsPerSide; row++)
        {
            var edgeA = verticesA[row * vertsPerSide + (vertsPerSide - 1)];
            var edgeB = verticesB[row * vertsPerSide + 0];

            Assert.Equal(edgeA.Position.Y, edgeB.Position.Y, 0.001f);
        }
    }

    [Fact]
    public void AdjacentFlatChunks_SeamVertices_HaveMatchingNormals()
    {
        var tiles = CreateFlatGrid();
        var neighbor = new ConstantNeighborProvider(
            new TileData(SurfaceType.Grass, 4, 0, 0, TileFlags.Walkable, 0));

        var (verticesA, _) = HeightmapMeshGenerator.Generate(tiles, neighbor, QualityPreset.Low);
        var (verticesB, _) = HeightmapMeshGenerator.Generate(tiles, neighbor, QualityPreset.Low);

        int vertsPerSide = 17;
        for (int row = 0; row < vertsPerSide; row++)
        {
            var edgeA = verticesA[row * vertsPerSide + (vertsPerSide - 1)];
            var edgeB = verticesB[row * vertsPerSide + 0];

            Assert.Equal(edgeA.Normal.X, edgeB.Normal.X, 0.01f);
            Assert.Equal(edgeA.Normal.Y, edgeB.Normal.Y, 0.01f);
            Assert.Equal(edgeA.Normal.Z, edgeB.Normal.Z, 0.01f);
        }
    }

    [Fact]
    public void SlopedChunk_Normals_AreNotAllUp()
    {
        var tiles = CreateSlopedGrid();
        var (vertices, _) = HeightmapMeshGenerator.Generate(tiles, null, QualityPreset.Low);

        // At least some normals should have non-zero X component (slope along X)
        bool hasNonVerticalNormal = false;
        foreach (var v in vertices)
        {
            if (MathF.Abs(v.Normal.X) > 0.05f || MathF.Abs(v.Normal.Z) > 0.05f)
            {
                hasNonVerticalNormal = true;
                break;
            }
        }

        Assert.True(hasNonVerticalNormal, "Sloped terrain should have non-vertical normals");
    }

    [Fact]
    public void CraterModifier_DepressesVertices_BelowOriginalHeight()
    {
        var tiles = CreateFlatGrid(heightLevel: 4);
        float baseHeight = 4 * HeightmapMeshGenerator.HeightStep; // 1.0

        var chunk = new ChunkData(0, 0, new TileMapData(ChunkData.Size, ChunkData.Size),
            terrainModifiers: new[]
            {
                new Crater(
                    Centre: new System.Numerics.Vector2(16f, 16f),
                    Radius: 8f,
                    Depth: 2f)
            });

        // Fill tiles
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                chunk.Tiles.SetTileData(x, y, tiles[x, y]);

        var terrain = ChunkTerrainBuilder.Build(chunk, quality: QualityPreset.Low);

        // Some vertices near the centre should be below base height
        bool hasDepressed = false;
        foreach (var v in terrain.Vertices)
        {
            if (v.Position.Y < baseHeight - 0.1f)
            {
                hasDepressed = true;
                break;
            }
        }

        Assert.True(hasDepressed, "Crater should depress vertices below original height");
    }

    /// <summary>
    /// Test helper: returns a fixed TileData for any coordinate.
    /// </summary>
    private sealed class ConstantNeighborProvider : IChunkNeighborProvider
    {
        private readonly TileData _tile;

        public ConstantNeighborProvider(TileData tile) => _tile = tile;

        public TileData GetTileAt(int localX, int localY) => _tile;
    }
}
