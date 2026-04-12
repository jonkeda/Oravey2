using Microsoft.Data.Sqlite;
using Oravey2.Core.Data;
using Oravey2.Core.UI.Stride;

namespace Oravey2.Tests.UI;

public class RegionDiscoveryTests : IDisposable
{
    private readonly SqliteConnection _worldConn;
    private readonly SqliteConnection _debugConn;
    private readonly WorldMapStore _worldStore;
    private readonly WorldMapStore _debugStore;

    public RegionDiscoveryTests()
    {
        _worldConn = new SqliteConnection("Data Source=:memory:");
        _worldConn.Open();
        _worldStore = new WorldMapStore(_worldConn);

        _debugConn = new SqliteConnection("Data Source=:memory:");
        _debugConn.Open();
        _debugStore = new WorldMapStore(_debugConn);
    }

    [Fact]
    public void DiscoverRegions_WithWorldStore_ReturnsRegions()
    {
        var cid = _worldStore.InsertContinent("TestContinent", null, 2, 2);
        _worldStore.InsertRegion(cid, "Haven", 0, 0, biome: "urban", description: "Safe zone");
        _worldStore.InsertRegion(cid, "Wasteland", 1, 0, biome: "desert", description: "Danger zone");

        var result = ScenarioSelectorScript.DiscoverRegions(_worldStore, null);

        Assert.Equal(2, result.Length);
        Assert.Contains(result, s => s.Id == "Haven" && s.Description == "Biome: urban");
        Assert.Contains(result, s => s.Id == "Wasteland" && s.Description == "Biome: desert");
    }

    [Fact]
    public void DiscoverRegions_WithDebugStore_TagsWithDebug()
    {
        var cid = _debugStore.InsertContinent("DebugContinent", null, 1, 1);
        _debugStore.InsertRegion(cid, "TestRegion", 0, 0, biome: "forest");

        var result = ScenarioSelectorScript.DiscoverRegions(null, _debugStore);

        Assert.Single(result);
        Assert.Equal("TestRegion", result[0].Id);
        Assert.StartsWith("[DEBUG]", result[0].Name);
    }

    [Fact]
    public void DiscoverRegions_BothStores_CombinesResults()
    {
        var wCid = _worldStore.InsertContinent("World", null, 2, 2);
        _worldStore.InsertRegion(wCid, "Region1", 0, 0, biome: "urban");
        _worldStore.InsertRegion(wCid, "Region2", 1, 0, biome: "desert");

        var dCid = _debugStore.InsertContinent("Debug", null, 1, 1);
        _debugStore.InsertRegion(dCid, "DebugRegion", 0, 0, biome: "swamp");

        var result = ScenarioSelectorScript.DiscoverRegions(_worldStore, _debugStore);

        Assert.Equal(3, result.Length);
        Assert.Equal(2, result.Count(s => !s.Name.StartsWith("[DEBUG]")));
        Assert.Single(result, s => s.Name.StartsWith("[DEBUG]"));
    }

    [Fact]
    public void DiscoverRegions_NoStores_ReturnsEmpty()
    {
        var result = ScenarioSelectorScript.DiscoverRegions(null, null);

        Assert.Empty(result);
    }

    public void Dispose()
    {
        _worldConn.Dispose();
        _debugConn.Dispose();
    }
}
