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
}
