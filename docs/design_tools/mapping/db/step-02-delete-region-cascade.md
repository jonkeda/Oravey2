# Step 02 — DeleteRegion Cascade

## Goal

Add a `DeleteRegion` method to `WorldMapStore` that removes a region and all
its dependent data (chunks, entity spawns, POIs, linear features, terrain
modifiers, chunk layers, interiors). Required for the upsert pattern in
step 03.

## Deliverables

### 2.1 `WorldMapStore.DeleteRegion(long regionId)`

Add to `src/Oravey2.Core/Data/WorldMapStore.cs`, in the `Region` section:

```csharp
/// <summary>
/// Deletes a region and all dependent data: chunks, chunk_layers,
/// entity_spawn, terrain_modifier, linear_feature, poi, interior.
/// Also deletes the parent continent if it has no other regions.
/// </summary>
public void DeleteRegion(long regionId)
{
    // Get the continent before deleting
    var region = GetRegion(regionId);
    if (region == null) return;

    // Order matters: delete children before parents to respect FK constraints.
    // entity_spawn, chunk_layer, terrain_modifier → chunk → region
    // interior → poi → region
    // linear_feature → region

    using var tx = _connection.BeginTransaction();
    try
    {
        // Delete entity_spawn, chunk_layer, terrain_modifier via chunk
        Exec("DELETE FROM entity_spawn WHERE chunk_id IN (SELECT id FROM chunk WHERE region_id = $rid);", regionId);
        Exec("DELETE FROM chunk_layer WHERE chunk_id IN (SELECT id FROM chunk WHERE region_id = $rid);", regionId);
        Exec("DELETE FROM terrain_modifier WHERE chunk_id IN (SELECT id FROM chunk WHERE region_id = $rid);", regionId);
        Exec("DELETE FROM chunk WHERE region_id = $rid;", regionId);

        // Delete interior via poi
        Exec("DELETE FROM interior WHERE poi_id IN (SELECT id FROM poi WHERE region_id = $rid);", regionId);
        Exec("DELETE FROM poi WHERE region_id = $rid;", regionId);

        // Delete linear features
        Exec("DELETE FROM linear_feature WHERE region_id = $rid;", regionId);

        // Delete the region itself
        Exec("DELETE FROM region WHERE id = $rid;", regionId);

        // Delete orphan continent (if no other regions reference it)
        ExecWithParam(
            "DELETE FROM continent WHERE id = $cid AND NOT EXISTS (SELECT 1 FROM region WHERE continent_id = $cid);",
            "$cid", region.ContinentId);

        tx.Commit();
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}

private void Exec(string sql, long regionId)
{
    using var cmd = _connection.CreateCommand();
    cmd.CommandText = sql;
    cmd.Parameters.AddWithValue("$rid", regionId);
    cmd.ExecuteNonQuery();
}

private void ExecWithParam(string sql, string param, long value)
{
    using var cmd = _connection.CreateCommand();
    cmd.CommandText = sql;
    cmd.Parameters.AddWithValue(param, value);
    cmd.ExecuteNonQuery();
}
```

### 2.2 Cascade coverage

The delete must cascade through the full schema dependency tree:

```
continent
  └── region
        ├── chunk
        │     ├── entity_spawn
        │     ├── chunk_layer
        │     └── terrain_modifier
        ├── linear_feature
        └── poi
              └── interior
```

Tables NOT affected (no region FK): `world_meta`, `sync_log`,
`location_description`.

### 2.3 Unit tests

New file: `tests/Oravey2.Tests/Data/DeleteRegionTests.cs`

```csharp
public class DeleteRegionTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly WorldMapStore _store;

    public DeleteRegionTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _store = new WorldMapStore(_conn);
    }

    public void Dispose() => _conn.Dispose();

    [Fact]
    public void DeleteRegion_RemovesRegionAndChunks()
    {
        var cid = _store.InsertContinent("test", null, 1, 1);
        var rid = _store.InsertRegion(cid, "TestRegion", 0, 0);
        _store.InsertChunk(rid, 0, 0, new byte[] { 1, 2, 3 });

        _store.DeleteRegion(rid);

        Assert.Null(_store.GetRegion(rid));
        Assert.Null(_store.GetChunkByGrid(rid, 0, 0));
    }

    [Fact]
    public void DeleteRegion_CascadesToEntitySpawns()
    {
        var cid = _store.InsertContinent("test", null, 1, 1);
        var rid = _store.InsertRegion(cid, "TestRegion", 0, 0);
        var chunkId = _store.InsertChunk(rid, 0, 0, new byte[] { 1, 2, 3 });
        _store.InsertEntitySpawn(chunkId, new EntitySpawnInfo("npc:test", 1, 1, 0));

        _store.DeleteRegion(rid);

        Assert.Empty(_store.GetEntitySpawns(chunkId));
    }

    [Fact]
    public void DeleteRegion_CascadesToLinearFeatures()
    {
        var cid = _store.InsertContinent("test", null, 1, 1);
        var rid = _store.InsertRegion(cid, "TestRegion", 0, 0);
        var feature = new LinearFeature(
            LinearFeatureType.Road, "residential", 1f,
            [new(new(0, 0)), new(new(10, 10))]);
        _store.InsertLinearFeature(rid, feature);

        _store.DeleteRegion(rid);

        Assert.Empty(_store.GetLinearFeatures(rid));
    }

    [Fact]
    public void DeleteRegion_CascadesToPois()
    {
        var cid = _store.InsertContinent("test", null, 1, 1);
        var rid = _store.InsertRegion(cid, "TestRegion", 0, 0);
        _store.InsertPoi(rid, "Town A", "town", 0, 0);

        _store.DeleteRegion(rid);

        Assert.Empty(_store.GetPois(rid));
    }

    [Fact]
    public void DeleteRegion_RemovesOrphanContinent()
    {
        var cid = _store.InsertContinent("test", null, 1, 1);
        var rid = _store.InsertRegion(cid, "TestRegion", 0, 0);

        _store.DeleteRegion(rid);

        Assert.Null(_store.GetContinent(cid));
    }

    [Fact]
    public void DeleteRegion_KeepsContinentWithOtherRegions()
    {
        var cid = _store.InsertContinent("test", null, 2, 1);
        var rid1 = _store.InsertRegion(cid, "Region1", 0, 0);
        var rid2 = _store.InsertRegion(cid, "Region2", 1, 0);

        _store.DeleteRegion(rid1);

        Assert.Null(_store.GetRegion(rid1));
        Assert.NotNull(_store.GetRegion(rid2));
        Assert.NotNull(_store.GetContinent(cid));
    }

    [Fact]
    public void DeleteRegion_NoOpForMissingRegion()
    {
        _store.DeleteRegion(99999); // should not throw
    }
}
```

## Dependencies

None — this is a self-contained store method.

## Estimated scope

- Modified files: 1 (`WorldMapStore.cs`)
- New files: 1 (`DeleteRegionTests.cs`)
