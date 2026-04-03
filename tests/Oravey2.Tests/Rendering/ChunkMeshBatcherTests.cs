using Oravey2.Core.World;
using Oravey2.Core.World.Rendering;

namespace Oravey2.Tests.Rendering;

public class ChunkMeshBatcherTests
{
    [Fact]
    public void AllGround_OneBatch_256Tiles()
    {
        var chunk = ChunkData.CreateDefault(0, 0); // 16×16 all ground
        var quality = QualitySettings.FromPreset(QualityPreset.Low);

        var batches = ChunkMeshBatcher.BatchChunk(chunk, quality);

        Assert.Single(batches.Batches);
        Assert.Equal(1, batches.TotalDrawCalls);
        Assert.True(batches.Batches.ContainsKey(SurfaceType.Dirt));
        Assert.Equal(256, batches.Batches[SurfaceType.Dirt].Count);
    }

    [Fact]
    public void MixedSurfaces_MultipleBatches()
    {
        var chunk = ChunkData.CreateDefault(0, 0);
        // Replace some tiles with Road and Water
        chunk.Tiles.SetTileData(0, 0, TileDataFactory.Road());
        chunk.Tiles.SetTileData(1, 0, TileDataFactory.Road());
        chunk.Tiles.SetTileData(2, 0, TileDataFactory.Water());
        var quality = QualitySettings.FromPreset(QualityPreset.Low);

        var batches = ChunkMeshBatcher.BatchChunk(chunk, quality);

        Assert.Equal(3, batches.TotalDrawCalls); // Dirt, Asphalt, Mud
        Assert.True(batches.Batches.ContainsKey(SurfaceType.Dirt));
        Assert.True(batches.Batches.ContainsKey(SurfaceType.Asphalt));
        Assert.True(batches.Batches.ContainsKey(SurfaceType.Mud));
    }

    [Fact]
    public void EmptyTiles_NotIncluded()
    {
        var tiles = new TileMapData(4, 4);
        // Leave all as default (TileData.Empty)
        tiles.SetTileData(0, 0, TileDataFactory.Ground());
        tiles.SetTileData(1, 1, TileDataFactory.Road());

        var chunk = new ChunkData(0, 0, tiles);
        var quality = QualitySettings.FromPreset(QualityPreset.Low);

        var batches = ChunkMeshBatcher.BatchChunk(chunk, quality);

        int total = batches.Batches.Values.Sum(b => b.Count);
        Assert.Equal(2, total);
    }

    [Fact]
    public void DrawCallCount_MatchesUniqueSurfaceTypes()
    {
        var chunk = ChunkData.CreateDefault(0, 0);
        // Add 3 different surface types
        chunk.Tiles.SetTileData(0, 0, TileDataFactory.Road());
        chunk.Tiles.SetTileData(1, 0, TileDataFactory.Rubble());
        var quality = QualitySettings.FromPreset(QualityPreset.Low);

        var batches = ChunkMeshBatcher.BatchChunk(chunk, quality);

        // Dirt (ground), Asphalt (road), Rock (rubble)
        Assert.Equal(3, batches.TotalDrawCalls);
    }

    [Fact]
    public void MediumQuality_ReturnsEmptyBatches()
    {
        var chunk = ChunkData.CreateDefault(0, 0);
        var quality = QualitySettings.FromPreset(QualityPreset.Medium);

        var batches = ChunkMeshBatcher.BatchChunk(chunk, quality);

        Assert.Empty(batches.Batches);
        Assert.Equal(0, batches.TotalDrawCalls);
    }

    [Fact]
    public void HighQuality_ReturnsEmptyBatches()
    {
        var chunk = ChunkData.CreateDefault(0, 0);
        var quality = QualitySettings.FromPreset(QualityPreset.High);

        var batches = ChunkMeshBatcher.BatchChunk(chunk, quality);

        Assert.Empty(batches.Batches);
        Assert.Equal(0, batches.TotalDrawCalls);
    }
}
