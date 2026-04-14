using Microsoft.Data.Sqlite;
using Oravey2.Core.Bootstrap;
using Oravey2.Core.Data;
using Oravey2.Core.World;

namespace Oravey2.Tests.Bootstrap;

public class RegionLoaderTests : IDisposable
{
    private readonly SqliteConnection _conn1;
    private readonly SqliteConnection _conn2;
    private readonly WorldMapStore _store1;
    private readonly WorldMapStore _store2;

    public RegionLoaderTests()
    {
        _conn1 = new SqliteConnection("Data Source=:memory:");
        _conn1.Open();
        _store1 = new WorldMapStore(_conn1);

        _conn2 = new SqliteConnection("Data Source=:memory:");
        _conn2.Open();
        _store2 = new WorldMapStore(_conn2);
    }

    public void Dispose()
    {
        _store1.Dispose();
        _store2.Dispose();
        _conn1.Dispose();
        _conn2.Dispose();
    }

    [Fact]
    public void FindRegion_ReturnsRegionFromFirstStore()
    {
        var cid = _store1.InsertContinent("World", null, 4, 4);
        _store1.InsertRegion(cid, "haven", 0, 0, "urban");

        var dispatcher = new EntitySpawnerDispatcher([]);
        var loader = new RegionLoader([_store1, _store2], null, dispatcher);

        var (store, region) = loader.FindRegion("haven");

        Assert.Equal("haven", region.Name);
        Assert.Same(_store1, store);
    }

    [Fact]
    public void FindRegion_SearchesSecondStore()
    {
        var cid1 = _store1.InsertContinent("World1", null, 4, 4);
        _store1.InsertRegion(cid1, "haven", 0, 0, "urban");

        var cid2 = _store2.InsertContinent("World2", null, 4, 4);
        _store2.InsertRegion(cid2, "wasteland", 1, 0, "desert");

        var dispatcher = new EntitySpawnerDispatcher([]);
        var loader = new RegionLoader([_store1, _store2], null, dispatcher);

        var (store, region) = loader.FindRegion("wasteland");

        Assert.Equal("wasteland", region.Name);
        Assert.Same(_store2, store);
    }

    [Fact]
    public void FindRegion_UnknownName_ThrowsInvalidOperationException()
    {
        var dispatcher = new EntitySpawnerDispatcher([]);
        var loader = new RegionLoader([_store1], null, dispatcher);

        var ex = Assert.Throws<InvalidOperationException>(() => loader.FindRegion("nonexistent"));
        Assert.Contains("nonexistent", ex.Message);
    }

    // ---- CompactChunkLayout ----

    [Fact]
    public void CompactChunkLayout_SingleCluster_RebasesToOrigin()
    {
        var records = new[]
        {
            MakeChunk(100, 200),
            MakeChunk(101, 200),
            MakeChunk(100, 201),
            MakeChunk(101, 201),
        };

        var map = RegionLoader.CompactChunkLayout(records);

        Assert.Equal(4, map.Count);
        Assert.Equal((0, 0), map[(100, 200)]);
        Assert.Equal((1, 0), map[(101, 200)]);
        Assert.Equal((0, 1), map[(100, 201)]);
        Assert.Equal((1, 1), map[(101, 201)]);
    }

    [Fact]
    public void CompactChunkLayout_TwoDistantClusters_PackedAdjacent()
    {
        // Cluster A: 2x2 at (0, 0)
        // Cluster B: 2x2 at (500, 300)
        var records = new[]
        {
            MakeChunk(0, 0), MakeChunk(1, 0), MakeChunk(0, 1), MakeChunk(1, 1),
            MakeChunk(500, 300), MakeChunk(501, 300), MakeChunk(500, 301), MakeChunk(501, 301),
        };

        var map = RegionLoader.CompactChunkLayout(records);

        // Both clusters should fit in a compact grid
        int maxX = map.Values.Max(v => v.NewX);
        int maxY = map.Values.Max(v => v.NewY);

        // Max extent should be small: 2 + 1 gap + 2 - 1 = 4
        Assert.True(maxX <= 4, $"maxX should be <= 4 but was {maxX}");
        Assert.True(maxY <= 1, $"maxY should be <= 1 but was {maxY}");
    }

    [Fact]
    public void CompactChunkLayout_Empty_ReturnsEmpty()
    {
        var map = RegionLoader.CompactChunkLayout([]);
        Assert.Empty(map);
    }

    private static ChunkRecord MakeChunk(int gx, int gy)
        => new(Id: 0, RegionId: 1, GridX: gx, GridY: gy,
               Mode: ChunkMode.Heightmap, Layer: MapLayer.Surface, TileData: []);
}
