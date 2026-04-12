# Step 05 — ContentPackImportService

## Goal

Create a service that bridges discovered content packs to the user's
persistent world.db. It lists which packs have a `world.db` ready to import
and performs the import into the user library.

## Deliverables

### 5.1 `ImportableRegion` record

New file: `src/Oravey2.Core/Content/ContentPackImportService.cs`

```csharp
namespace Oravey2.Core.Content;

/// <summary>
/// A region available for import from a content pack's world.db.
/// </summary>
public sealed record ImportableRegion(
    string PackId,
    string PackName,
    string RegionName,
    string Description,
    string PackDirectory);
```

### 5.2 `ContentPackImportService`

Same file:

```csharp
using Oravey2.Core.Data;

namespace Oravey2.Core.Content;

public sealed class ContentPackImportService
{
    private readonly ContentPackService _packs;

    public ContentPackImportService(ContentPackService packs)
    {
        _packs = packs;
    }

    /// <summary>
    /// Returns content packs that have a world.db ready to import.
    /// Opens each pack's world.db read-only to list regions.
    /// </summary>
    public IReadOnlyList<ImportableRegion> GetImportableRegions()
    {
        var regions = new List<ImportableRegion>();
        foreach (var pack in _packs.Packs)
        {
            var packDbPath = WorldDbPaths.GetPackWorldDbPath(pack.Directory);
            if (packDbPath == null) continue;

            using var packStore = new WorldMapStore(packDbPath);
            foreach (var region in packStore.GetAllRegions())
            {
                regions.Add(new ImportableRegion(
                    PackId: pack.Manifest.Id,
                    PackName: pack.Manifest.Name,
                    RegionName: region.Name,
                    Description: region.Description ?? pack.Manifest.Description,
                    PackDirectory: pack.Directory));
            }
        }
        return regions;
    }

    /// <summary>
    /// Imports all regions from a content pack into the user's world.db.
    /// Uses upsert — re-importing replaces existing regions.
    /// </summary>
    public ImportResult ImportRegion(string contentPackDir)
    {
        var userDbPath = WorldDbPaths.GetUserWorldDbPath();
        using var userStore = new WorldMapStore(userDbPath);
        var importer = new ContentPackImporter(userStore);
        return importer.Import(contentPackDir);
    }

    /// <summary>
    /// Checks whether a region is already imported in the user's world.db.
    /// </summary>
    public bool IsRegionImported(string regionName)
    {
        var userDbPath = WorldDbPaths.GetUserWorldDbPath();
        if (!File.Exists(userDbPath)) return false;
        using var store = new WorldMapStore(userDbPath);
        return store.GetRegionByName(regionName) != null;
    }
}
```

### 5.3 Unit tests

New file: `tests/Oravey2.Tests/Content/ContentPackImportServiceTests.cs`

Tests use temp directories and in-memory stores:

```csharp
public class ContentPackImportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _userDbPath;

    public ContentPackImportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _userDbPath = Path.Combine(_tempDir, "user-world.db");
        Environment.SetEnvironmentVariable("ORAVEY2_WORLD_DB", _userDbPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ORAVEY2_WORLD_DB", null);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void GetImportableRegions_FindsPacksWithWorldDb()
    {
        // Create a content pack with a world.db
        var packDir = CreateContentPackWithWorldDb("test-pack", "TestRegion");
        var service = CreateService(packDir);

        var regions = service.GetImportableRegions();

        Assert.Single(regions);
        Assert.Equal("TestRegion", regions[0].RegionName);
    }

    [Fact]
    public void GetImportableRegions_SkipsPacksWithoutWorldDb()
    {
        // Create a content pack WITHOUT world.db
        var packDir = Path.Combine(_tempDir, "ContentPacks", "empty-pack");
        Directory.CreateDirectory(packDir);
        File.WriteAllText(Path.Combine(packDir, "manifest.json"),
            """{"id":"empty","name":"Empty","version":"1.0.0"}""");

        var service = CreateService(packDir);

        // Will find 0 importable regions (pack has no world.db)
        var regions = service.GetImportableRegions();
        Assert.Empty(regions);
    }

    [Fact]
    public void ImportRegion_WritesToUserWorldDb()
    {
        var packDir = CreateContentPackWithManifest("test-pack", "TestRegion");
        var service = CreateService(packDir);

        service.ImportRegion(packDir);

        Assert.True(File.Exists(_userDbPath));
        using var store = new WorldMapStore(_userDbPath);
        Assert.NotNull(store.GetRegionByName("TestRegion"));
    }

    [Fact]
    public void IsRegionImported_ReturnsFalseWhenNotImported()
    {
        var packDir = CreateContentPackWithManifest("test-pack", "TestRegion");
        var service = CreateService(packDir);

        Assert.False(service.IsRegionImported("TestRegion"));
    }

    [Fact]
    public void IsRegionImported_ReturnsTrueAfterImport()
    {
        var packDir = CreateContentPackWithManifest("test-pack", "TestRegion");
        var service = CreateService(packDir);

        service.ImportRegion(packDir);

        Assert.True(service.IsRegionImported("TestRegion"));
    }

    // Helper: creates a content pack dir with manifest.json
    private string CreateContentPackWithManifest(string id, string name)
    {
        var dir = Path.Combine(_tempDir, "ContentPacks", id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "manifest.json"),
            $$"""{"id":"{{id}}","name":"{{name}}","version":"1.0.0"}""");
        return dir;
    }

    // Helper: creates a content pack with a pre-built world.db containing a region
    private string CreateContentPackWithWorldDb(string id, string regionName)
    {
        var dir = CreateContentPackWithManifest(id, regionName);
        var dbPath = Path.Combine(dir, "world.db");
        using var store = new WorldMapStore(dbPath);
        var cid = store.InsertContinent(regionName, null, 1, 1);
        store.InsertRegion(cid, regionName, 0, 0);
        return dir;
    }

    private ContentPackImportService CreateService(string packDir)
    {
        // ContentPackService expects a ContentPacks/ parent dir
        var contentPackService = new ContentPackService();
        // We can't easily mock DiscoverPacks, so set up the directory structure
        // under ContentPacks/ and call DiscoverPacks
        contentPackService.DiscoverPacks(Path.GetDirectoryName(packDir)!);
        return new ContentPackImportService(contentPackService);
    }
}
```

> **Note:** `ContentPackService.DiscoverPacks()` currently scans
> `AppContext.BaseDirectory/ContentPacks`. Add an overload
> `DiscoverPacks(string contentPacksDir)` for testability:
>
> ```csharp
> public void DiscoverPacks(string contentPacksDir) { /* same body, use param */ }
> public void DiscoverPacks() => DiscoverPacks(
>     Path.Combine(AppContext.BaseDirectory, "ContentPacks"));
> ```

### 5.4 `ContentPackService.DiscoverPacks` overload

File: `src/Oravey2.Core/Content/ContentPackService.cs`

Extract the directory parameter:

**Before:**
```csharp
public void DiscoverPacks()
{
    var contentPacksDir = Path.Combine(AppContext.BaseDirectory, "ContentPacks");
    if (!Directory.Exists(contentPacksDir))
    // ...
}
```

**After:**
```csharp
public void DiscoverPacks() =>
    DiscoverPacks(Path.Combine(AppContext.BaseDirectory, "ContentPacks"));

public void DiscoverPacks(string contentPacksDir)
{
    if (!Directory.Exists(contentPacksDir))
    // ... (existing body, unchanged)
}
```

No callers change — the parameterless overload delegates to the new one.

## Dependencies

- Step 01 (`WorldDbPaths`)
- Step 02 (`DeleteRegion` — via `ContentPackImporter` upsert in step 03)
- Step 03 (`ContentPackImporter` upsert)

## Estimated scope

- New files: 2 (`ContentPackImportService.cs`, `ContentPackImportServiceTests.cs`)
- Modified files: 1 (`ContentPackService.cs` — extract overload)
