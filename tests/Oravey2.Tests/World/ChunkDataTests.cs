using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class ChunkDataTests
{
    [Fact]
    public void Constructor_DefaultTiles_16x16()
    {
        var chunk = new ChunkData(3, 4);
        Assert.Equal(ChunkData.Size, chunk.Tiles.Width);
        Assert.Equal(ChunkData.Size, chunk.Tiles.Height);
    }

    [Fact]
    public void ChunkXY_SetFromConstructor()
    {
        var chunk = new ChunkData(3, 4);
        Assert.Equal(3, chunk.ChunkX);
        Assert.Equal(4, chunk.ChunkY);
    }

    [Fact]
    public void WorldTileX_ChunkSizeMultiple()
    {
        var chunk = new ChunkData(2, 3);
        Assert.Equal(32, chunk.WorldTileX);
        Assert.Equal(48, chunk.WorldTileY);
    }

    [Fact]
    public void GetWorldTile_MapsToLocal()
    {
        var chunk = new ChunkData(1, 0);
        // Local tile (0, 5) = world tile (16, 5)
        chunk.Tiles.SetTile(0, 5, TileType.Road);
        Assert.Equal(TileType.Road, chunk.GetWorldTile(16, 5));
    }

    [Fact]
    public void GetWorldTile_OutOfBounds_Empty()
    {
        var chunk = new ChunkData(0, 0);
        Assert.Equal(TileType.Empty, chunk.GetWorldTile(20, 20));
    }

    [Fact]
    public void MarkModified_FlagsEntity()
    {
        var chunk = new ChunkData(0, 0);
        chunk.MarkModified("npc_01");
        Assert.True(chunk.IsModified("npc_01"));
    }

    [Fact]
    public void IsModified_UnknownId_False()
    {
        var chunk = new ChunkData(0, 0);
        Assert.False(chunk.IsModified("xyz"));
    }

    [Fact]
    public void CreateDefault_AllGround()
    {
        var chunk = ChunkData.CreateDefault(1, 1);
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                Assert.Equal(TileType.Ground, chunk.Tiles.GetTile(x, y));
    }

    [Fact]
    public void ChunkData_DefaultMode_IsHeightmap()
    {
        var chunk = new ChunkData(0, 0);
        Assert.Equal(ChunkMode.Heightmap, chunk.Mode);
    }

    [Fact]
    public void ChunkData_DefaultLayer_IsSurface()
    {
        var chunk = new ChunkData(0, 0);
        Assert.Equal(MapLayer.Surface, chunk.Layer);
    }

    [Fact]
    public void ChunkData_DefaultTerrainModifiers_Empty()
    {
        var chunk = new ChunkData(0, 0);
        Assert.Empty(chunk.TerrainModifiers);
    }

    [Fact]
    public void ChunkData_DefaultLinearFeatures_Empty()
    {
        var chunk = new ChunkData(0, 0);
        Assert.Empty(chunk.LinearFeatures);
    }

    [Fact]
    public void ChunkData_WithMode_SetsCorrectly()
    {
        var chunk = new ChunkData(0, 0, mode: ChunkMode.Hybrid, layer: MapLayer.Underground);
        Assert.Equal(ChunkMode.Hybrid, chunk.Mode);
        Assert.Equal(MapLayer.Underground, chunk.Layer);
    }
}
