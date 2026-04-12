# Step 03 — ContentPackImporter Upsert

## Goal

Make `ContentPackImporter.Import()` idempotent. Re-importing the same region
replaces the old data instead of creating duplicates.

## Deliverables

### 3.1 Add upsert logic to `Import()`

File: `src/Oravey2.Core/Data/ContentPackImporter.cs`

Insert the following block after `result.RegionName = manifest.Name;` and
before the `InsertContinent` call:

```csharp
// Replace existing region if re-importing (upsert)
var existing = _store.GetRegionByName(manifest.Name);
if (existing != null)
{
    _store.DeleteRegion(existing.Id);
    result.Warnings.Add($"Replaced existing region '{manifest.Name}'.");
}
```

**Before:**
```csharp
result.RegionName = manifest.Name;

// b. Create continent + region
var continentId = _store.InsertContinent(manifest.Name, null, 1, 1);
```

**After:**
```csharp
result.RegionName = manifest.Name;

// b. Replace existing region if re-importing
var existing = _store.GetRegionByName(manifest.Name);
if (existing != null)
{
    _store.DeleteRegion(existing.Id);
    result.Warnings.Add($"Replaced existing region '{manifest.Name}'.");
}

// c. Create continent + region
var continentId = _store.InsertContinent(manifest.Name, null, 1, 1);
```

### 3.2 Unit tests

New file: `tests/Oravey2.Tests/Data/ContentPackImporterUpsertTests.cs`

These tests use the in-memory `WorldMapStore` constructor and a temp content
pack directory on disk:

```csharp
public class ContentPackImporterUpsertTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly WorldMapStore _store;
    private readonly string _packDir;

    public ContentPackImporterUpsertTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _store = new WorldMapStore(_conn);

        // Create minimal content pack on disk
        _packDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_packDir);
        File.WriteAllText(Path.Combine(_packDir, "manifest.json"),
            """{"id":"test-pack","name":"TestRegion","version":"1.0.0"}""");
    }

    public void Dispose()
    {
        _conn.Dispose();
        if (Directory.Exists(_packDir))
            Directory.Delete(_packDir, true);
    }

    [Fact]
    public void Import_FirstTime_InsertsRegion()
    {
        var importer = new ContentPackImporter(_store);
        var result = importer.Import(_packDir);

        Assert.Equal("TestRegion", result.RegionName);
        Assert.NotNull(_store.GetRegionByName("TestRegion"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("Replaced"));
    }

    [Fact]
    public void Import_SecondTime_ReplacesRegion()
    {
        var importer = new ContentPackImporter(_store);
        importer.Import(_packDir);
        var result = importer.Import(_packDir);

        // Only one region should exist
        var regions = _store.GetAllRegions();
        Assert.Single(regions, r => r.Name == "TestRegion");
        Assert.Contains(result.Warnings, w => w.Contains("Replaced"));
    }

    [Fact]
    public void Import_SecondTime_ClearsOldChunks()
    {
        // Add a town with layout so chunks are created
        var townDir = Path.Combine(_packDir, "towns", "TestTown");
        Directory.CreateDirectory(townDir);
        File.WriteAllText(Path.Combine(townDir, "layout.json"),
            """{"width":16,"height":16,"surface":[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]}""");

        var importer = new ContentPackImporter(_store);
        var r1 = importer.Import(_packDir);
        Assert.True(r1.ChunksWritten > 0);

        var r2 = importer.Import(_packDir);
        Assert.True(r2.ChunksWritten > 0);

        // Should still have the same number of chunks, not double
        var region = _store.GetRegionByName("TestRegion")!;
        // Verify chunk at 0,0 exists (no duplicates)
        Assert.NotNull(_store.GetChunkByGrid(region.Id, 0, 0));
    }
}
```

## Dependencies

- Step 02 (`WorldMapStore.DeleteRegion`)

## Estimated scope

- Modified files: 1 (`ContentPackImporter.cs` — 6 lines added)
- New files: 1 (`ContentPackImporterUpsertTests.cs`)
