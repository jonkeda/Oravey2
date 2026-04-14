using Microsoft.Data.Sqlite;
using Oravey2.Core.Data;
using Oravey2.Core.World;
using Xunit;

namespace Oravey2.Tests.Data;

public class DeleteRegionCascadeTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly WorldMapStore _store;

    public DeleteRegionCascadeTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _store = new WorldMapStore(_conn);
    }

    public void Dispose()
    {
        _store.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public void DeleteRegion_RemovesRegionAndAllChildren()
    {
        var cid = _store.InsertContinent("TestContinent", null, 1, 1);
        var rid = _store.InsertRegion(cid, "TestRegion", 0, 0);

        // Add chunks with children
        var tileData = new byte[] { 1, 2, 3, 4 };
        var chunkId = _store.InsertChunk(rid, 0, 0, tileData);
        _store.InsertEntitySpawn(chunkId, new EntitySpawnInfo("npc:test", 1, 1, 0));
        _store.InsertChunkLayer(chunkId, MapLayer.Underground, tileData);

        // Add region-level children
        var poiId = _store.InsertPoi(rid, "TestPoi", "zone", 0, 0);
        _store.InsertLinearFeature(rid, new LinearFeature
        {
            Type = LinearFeatureType.Primary, Style = "asphalt", Width = 3,
            Nodes = [new LinearFeatureNode { Position = new System.Numerics.Vector2(0, 0) }],
        });

        _store.DeleteRegion(rid);

        Assert.Null(_store.GetRegion(rid));
        Assert.Null(_store.GetChunkByGrid(rid, 0, 0));
        Assert.Empty(_store.GetPois(rid));
        Assert.Empty(_store.GetLinearFeatures(rid));
        Assert.Empty(_store.GetEntitySpawns(chunkId));
        Assert.Empty(_store.GetChunkLayers(chunkId));
    }

    [Fact]
    public void DeleteRegion_RemovesOrphanContinent()
    {
        var cid = _store.InsertContinent("LoneContinent", null, 1, 1);
        var rid = _store.InsertRegion(cid, "OnlyRegion", 0, 0);

        _store.DeleteRegion(rid);

        Assert.Null(_store.GetContinent(cid));
    }

    [Fact]
    public void DeleteRegion_KeepsContinentWithOtherRegions()
    {
        var cid = _store.InsertContinent("SharedContinent", null, 2, 1);
        var rid1 = _store.InsertRegion(cid, "Region1", 0, 0);
        var rid2 = _store.InsertRegion(cid, "Region2", 1, 0);

        _store.DeleteRegion(rid1);

        Assert.Null(_store.GetRegion(rid1));
        Assert.NotNull(_store.GetRegion(rid2));
        Assert.NotNull(_store.GetContinent(cid));
    }

    [Fact]
    public void DeleteRegion_NoOpForMissingId()
    {
        // Should not throw
        _store.DeleteRegion(999);
    }
}
