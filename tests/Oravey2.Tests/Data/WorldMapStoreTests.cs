using System.Numerics;
using Microsoft.Data.Sqlite;
using Oravey2.Core.Data;
using Oravey2.Core.World;

namespace Oravey2.Tests.Data;

public class WorldMapStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WorldMapStore _store;

    public WorldMapStoreTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _store = new WorldMapStore(_connection);
    }

    [Fact]
    public void InsertAndGetContinent_RoundTrips()
    {
        var id = _store.InsertContinent("Cascadia", "Pacific Northwest wasteland", 4, 4);

        var result = _store.GetContinent(id);

        Assert.NotNull(result);
        Assert.Equal("Cascadia", result.Name);
        Assert.Equal("Pacific Northwest wasteland", result.Description);
        Assert.Equal(4, result.GridWidth);
        Assert.Equal(4, result.GridHeight);
    }

    [Fact]
    public void InsertAndGetRegion_RoundTrips()
    {
        var cid = _store.InsertContinent("Test", null, 2, 2);
        var rid = _store.InsertRegion(cid, "Downtown", 0, 1, biome: "urban", baseHeight: 12.5);

        var result = _store.GetRegion(rid);

        Assert.NotNull(result);
        Assert.Equal("Downtown", result.Name);
        Assert.Equal(0, result.GridX);
        Assert.Equal(1, result.GridY);
        Assert.Equal("urban", result.Biome);
        Assert.Equal(12.5, result.BaseHeight);
    }

    [Fact]
    public void InsertAndGetChunk_RoundTrips()
    {
        var cid = _store.InsertContinent("C", null, 1, 1);
        var rid = _store.InsertRegion(cid, "R", 0, 0);

        // Create a 16×16 grid with some interesting data
        var grid = new TileData[16, 16];
        grid[3, 7] = new TileData(
            Surface: SurfaceType.Concrete,
            HeightLevel: 5,
            WaterLevel: 0,
            StructureId: 42,
            Flags: TileFlags.Walkable | TileFlags.Destructible,
            VariantSeed: 3);
        var compressed = TileDataSerializer.SerializeTileGrid(grid);

        var chunkId = _store.InsertChunk(rid, 2, 3, compressed, ChunkMode.Hybrid, MapLayer.Underground);
        var result = _store.GetChunkByGrid(rid, 2, 3);

        Assert.NotNull(result);
        Assert.Equal(ChunkMode.Hybrid, result.Mode);
        Assert.Equal(MapLayer.Underground, result.Layer);

        // Verify tile data round-trips
        var resultGrid = TileDataSerializer.DeserializeTileGrid(result.TileData, 16, 16);
        Assert.Equal(grid[3, 7], resultGrid[3, 7]);
        Assert.Equal(TileData.Empty, resultGrid[0, 0]);
    }

    [Fact]
    public void GetChunkByGrid_NotFound_ReturnsNull()
    {
        var cid = _store.InsertContinent("C", null, 1, 1);
        var rid = _store.InsertRegion(cid, "R", 0, 0);

        var result = _store.GetChunkByGrid(rid, 99, 99);

        Assert.Null(result);
    }

    [Fact]
    public void InsertPoi_GetByRegion_ReturnsAll()
    {
        var cid = _store.InsertContinent("C", null, 1, 1);
        var rid = _store.InsertRegion(cid, "R", 0, 0);

        _store.InsertPoi(rid, "Bunker Alpha", "shelter", 2, 3, "Underground vault");
        _store.InsertPoi(rid, "Gas Station", "loot", 5, 1, icon: "gas");
        _store.InsertPoi(rid, "Radio Tower", "landmark", 7, 7);

        var pois = _store.GetPois(rid);

        Assert.Equal(3, pois.Count);
        Assert.Contains(pois, p => p.Name == "Bunker Alpha" && p.Description == "Underground vault");
        Assert.Contains(pois, p => p.Name == "Gas Station" && p.Icon == "gas");
        Assert.Contains(pois, p => p.Name == "Radio Tower");
    }

    [Fact]
    public void InsertLinearFeature_GetByRegion_ReturnsWithNodes()
    {
        var cid = _store.InsertContinent("C", null, 1, 1);
        var rid = _store.InsertRegion(cid, "R", 0, 0);

        var nodes = new List<LinearFeatureNode>
        {
            new(new Vector2(0, 0), 5.0f),
            new(new Vector2(10, 0), null),
            new(new Vector2(10, 10), 3.5f),
        };
        var feature = new LinearFeature(LinearFeatureType.Road, "asphalt_2lane", 4.0f, nodes);
        _store.InsertLinearFeature(rid, feature);

        var results = _store.GetLinearFeatures(rid);

        Assert.Single(results);
        var f = results[0];
        Assert.Equal(LinearFeatureType.Road, f.Type);
        Assert.Equal("asphalt_2lane", f.Style);
        Assert.Equal(4.0f, f.Width);
        Assert.Equal(3, f.Nodes.Count);
        Assert.Equal(new Vector2(0, 0), f.Nodes[0].Position);
        Assert.Equal(5.0f, f.Nodes[0].OverrideHeight);
        Assert.Null(f.Nodes[1].OverrideHeight);
        Assert.Equal(3.5f, f.Nodes[2].OverrideHeight);
    }

    public void Dispose()
    {
        _store.Dispose();
        _connection.Dispose();
    }
}
