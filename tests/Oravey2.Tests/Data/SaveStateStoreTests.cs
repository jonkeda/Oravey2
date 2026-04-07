using Microsoft.Data.Sqlite;
using Oravey2.Core.Data;

namespace Oravey2.Tests.Data;

public class SaveStateStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SaveStateStore _store;

    public SaveStateStoreTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _store = new SaveStateStore(_connection);
    }

    [Fact]
    public void SaveAndLoadParty_RoundTrips()
    {
        var json = """{"name":"Scout","hp":85,"level":3}""";

        _store.SaveParty(json);
        var result = _store.LoadParty();

        Assert.Equal(json, result);
    }

    [Fact]
    public void SaveChunkState_ThenGet_ReturnsDelta()
    {
        var tileOverrides = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var modifiedEntities = """{"raider_01":true}""";

        _store.SaveChunkState(1, 2, 3, tileOverrides, modifiedEntities);
        var result = _store.GetChunkState(1, 2, 3);

        Assert.NotNull(result);
        Assert.Equal(tileOverrides, result.Value.TileOverrides);
        Assert.Equal(modifiedEntities, result.Value.ModifiedEntities);
    }

    [Fact]
    public void DiscoverPoi_AppearsInList()
    {
        _store.DiscoverPoi(10);
        _store.DiscoverPoi(20);
        _store.DiscoverPoi(10); // duplicate — should be ignored

        var discovered = _store.GetDiscoveredPois();

        Assert.Equal(2, discovered.Count);
        Assert.Contains(10L, discovered);
        Assert.Contains(20L, discovered);
    }

    public void Dispose()
    {
        _store.Dispose();
        _connection.Dispose();
    }
}
