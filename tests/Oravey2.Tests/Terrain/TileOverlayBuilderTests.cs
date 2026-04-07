using Oravey2.Core.World;
using Oravey2.Core.World.Rendering;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Tests.Terrain;

public class TileOverlayBuilderTests
{
    private static ChunkData CreateHybridChunk()
    {
        const int size = ChunkData.Size;
        var tiles = new TileMapData(size, size);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                tiles.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Concrete,
                    HeightLevel: 4,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: 0));
            }
        }

        return new ChunkData(0, 0, tiles, mode: ChunkMode.Hybrid);
    }

    private static ChunkData CreateHeightmapChunk()
    {
        const int size = ChunkData.Size;
        var tiles = new TileMapData(size, size);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                tiles.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Grass,
                    HeightLevel: 4,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: 0));
            }
        }

        return new ChunkData(0, 0, tiles, mode: ChunkMode.Heightmap);
    }

    private static (float[,] Heights, int VertsPerSide, float ChunkWorldSize) BuildHeights(ChunkData chunk)
    {
        int tilesPerSide = ChunkData.Size;
        int subdivision = 1; // Low quality for fast tests
        int vertsPerSide = tilesPerSide * subdivision + 1;
        float chunkWorldSize = tilesPerSide * HeightmapMeshGenerator.TileWorldSize;

        var heights = HeightmapMeshGenerator.SampleHeights(
            chunk.Tiles.TileDataGrid, null, vertsPerSide, tilesPerSide, subdivision);

        return (heights, vertsPerSide, chunkWorldSize);
    }

    [Fact]
    public void HybridChunk_ProducesOverlay_NotNull()
    {
        var chunk = CreateHybridChunk();
        var (heights, vertsPerSide, chunkWorldSize) = BuildHeights(chunk);

        var overlay = TileOverlayBuilder.Build(chunk, heights, vertsPerSide, chunkWorldSize);

        Assert.NotNull(overlay);
        Assert.True(overlay.FloorVertices.Length > 0);
        Assert.True(overlay.FloorIndices.Length > 0);
    }

    [Fact]
    public void HeightmapChunk_ProducesOverlay_Null()
    {
        var chunk = CreateHeightmapChunk();
        var (heights, vertsPerSide, chunkWorldSize) = BuildHeights(chunk);

        var overlay = TileOverlayBuilder.Build(chunk, heights, vertsPerSide, chunkWorldSize);

        Assert.Null(overlay);
    }

    [Fact]
    public void OverlayQuads_SnapToHeightmapSurface()
    {
        var chunk = CreateHybridChunk();
        var (heights, vertsPerSide, chunkWorldSize) = BuildHeights(chunk);

        var overlay = TileOverlayBuilder.Build(chunk, heights, vertsPerSide, chunkWorldSize)!;

        // Each floor vertex Y should match GetSurfaceHeight + OverlayOffset
        foreach (var v in overlay.FloorVertices)
        {
            float expectedY = HeightmapMeshGenerator.GetSurfaceHeight(
                new System.Numerics.Vector2(v.Position.X, v.Position.Z),
                heights, vertsPerSide, chunkWorldSize) + TileOverlayBuilder.OverlayOffset;

            Assert.Equal(expectedY, v.Position.Y, 0.001f);
        }
    }

    [Fact]
    public void StructurePlacement_ReadsStructureId()
    {
        const int size = ChunkData.Size;
        var tiles = new TileMapData(size, size);

        // Fill with grass (no overlay)
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                tiles.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Grass, HeightLevel: 4, WaterLevel: 0,
                    StructureId: 0, Flags: TileFlags.Walkable, VariantSeed: 0));

        // Place a structure at tile (5, 5)
        tiles.SetTileData(5, 5, new TileData(
            Surface: SurfaceType.Concrete, HeightLevel: 4, WaterLevel: 0,
            StructureId: 42, Flags: TileFlags.Walkable, VariantSeed: 0));

        var chunk = new ChunkData(0, 0, tiles, mode: ChunkMode.Hybrid);
        var (heights, vertsPerSide, chunkWorldSize) = BuildHeights(chunk);

        var overlay = TileOverlayBuilder.Build(chunk, heights, vertsPerSide, chunkWorldSize)!;

        Assert.Contains(overlay.Structures, s => s.StructureId == 42);
    }
}
