using Microsoft.Data.Sqlite;
using Oravey2.Core.Data;
using Oravey2.Core.World;

namespace Oravey2.Tests.Data;

public class MapDataProviderTests : IDisposable
{
    private readonly SqliteConnection _worldConn;
    private readonly SqliteConnection _saveConn;
    private readonly WorldMapStore _worldStore;
    private readonly SaveStateStore _saveStore;
    private readonly long _regionId;

    public MapDataProviderTests()
    {
        _worldConn = new SqliteConnection("Data Source=:memory:");
        _worldConn.Open();
        _worldStore = new WorldMapStore(_worldConn);

        _saveConn = new SqliteConnection("Data Source=:memory:");
        _saveConn.Open();
        _saveStore = new SaveStateStore(_saveConn);

        var cid = _worldStore.InsertContinent("Test", null, 1, 1);
        _regionId = _worldStore.InsertRegion(cid, "Region0", 0, 0);
    }

    [Fact]
    public void GetChunkData_NoSaveDelta_ReturnsWorldData()
    {
        var grid = CreateTestGrid(SurfaceType.Grass, TileFlags.Walkable);
        var compressed = TileDataSerializer.SerializeTileGrid(grid);
        _worldStore.InsertChunk(_regionId, 0, 0, compressed);

        var provider = new MapDataProvider(_worldStore, _saveStore);
        var chunk = provider.GetChunkData(_regionId, 0, 0);

        Assert.NotNull(chunk);
        Assert.Equal(0, chunk.ChunkX);
        Assert.Equal(0, chunk.ChunkY);
        var tile = chunk.Tiles.GetTileData(5, 5);
        Assert.Equal(SurfaceType.Grass, tile.Surface);
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void GetChunkData_WithSaveDelta_MergesOverrides()
    {
        // World: all grass/walkable
        var worldGrid = CreateTestGrid(SurfaceType.Grass, TileFlags.Walkable);
        var worldCompressed = TileDataSerializer.SerializeTileGrid(worldGrid);
        _worldStore.InsertChunk(_regionId, 0, 0, worldCompressed);

        // Save override: tile (3,3) changed to concrete/irradiated
        var overrideGrid = new TileData[ChunkData.Size, ChunkData.Size];
        overrideGrid[3, 3] = new TileData(
            Surface: SurfaceType.Concrete,
            HeightLevel: 1,
            WaterLevel: 0,
            StructureId: 0,
            Flags: TileFlags.Walkable | TileFlags.Irradiated,
            VariantSeed: 0);
        var overrideCompressed = TileDataSerializer.SerializeTileGrid(overrideGrid);
        _saveStore.SaveChunkState(_regionId, 0, 0, overrideCompressed);

        var provider = new MapDataProvider(_worldStore, _saveStore);
        var chunk = provider.GetChunkData(_regionId, 0, 0);

        Assert.NotNull(chunk);
        // Overridden tile
        var tile33 = chunk.Tiles.GetTileData(3, 3);
        Assert.Equal(SurfaceType.Concrete, tile33.Surface);
        Assert.True(tile33.Flags.HasFlag(TileFlags.Irradiated));
        // Non-overridden tile preserved from world
        var tile55 = chunk.Tiles.GetTileData(5, 5);
        Assert.Equal(SurfaceType.Grass, tile55.Surface);
    }

    [Fact]
    public void GetChunkData_MissingChunk_ReturnsNull()
    {
        var provider = new MapDataProvider(_worldStore, _saveStore);
        var chunk = provider.GetChunkData(_regionId, 99, 99);

        Assert.Null(chunk);
    }

    private static TileData[,] CreateTestGrid(SurfaceType surface, TileFlags flags)
    {
        var grid = new TileData[ChunkData.Size, ChunkData.Size];
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                grid[x, y] = new TileData(
                    Surface: surface,
                    HeightLevel: 1,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: flags,
                    VariantSeed: 0);
        return grid;
    }

    public void Dispose()
    {
        _worldStore.Dispose();
        _saveStore.Dispose();
        _worldConn.Dispose();
        _saveConn.Dispose();
    }
}
