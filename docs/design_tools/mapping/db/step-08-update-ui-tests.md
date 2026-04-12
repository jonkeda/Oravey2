# Step 08 — Update UI Tests

## Goal

Adapt existing UI tests to the new world.db architecture. The MapGen export
tests check the new per-pack path, and the Stride smoke tests use the user
library path (via `ORAVEY2_WORLD_DB` env var for isolation).

## Deliverables

### 8.1 Update `NoordHollandExportTests` — world.db path

File: `tests/Oravey2.UITests/MapGen/NoordHollandExportTests.cs`

The `Assembly_ExportToDb_CreatesWorldDb` test currently checks for
`content/world.db`. Update it to verify the world.db was written inside the
content pack directory.

**Before:**
```csharp
var contentPackDir = Path.Combine(dir.FullName, "content");
var worldDbPath = Path.Combine(contentPackDir, "world.db");
```

**After:**
```csharp
var contentPackDir = Path.Combine(dir.FullName,
    "content", "Oravey2.Apocalyptic.NL.NH");
var worldDbPath = Path.Combine(contentPackDir, "world.db");
```

### 8.2 Update `NoordHollandTestFixture` — use env var for isolation

File: `tests/Oravey2.UITests/NoordHollandSmokeTests.cs`

The smoke test fixture currently writes `world.db` into the game output
directory. After step 06, the game reads from the user library path. Use
`ORAVEY2_WORLD_DB` to redirect to a test-specific location.

**Before (`EnsureWorldDb()`):**
```csharp
private void EnsureWorldDb()
{
    var solutionDir = FindSolutionDirectory();
    var gameOutputDir = Path.Combine(solutionDir,
        "src", "Oravey2.Windows", "bin", "Debug", "net10.0");
    var worldDbPath = Path.Combine(gameOutputDir, "world.db");
    var contentPackDir = Path.Combine(gameOutputDir,
        "ContentPacks", "Oravey2.Apocalyptic.NL.NH");
    // ...
    using var store = new WorldMapStore(worldDbPath);
    var importer = new ContentPackImporter(store);
    var result = importer.Import(contentPackDir);
    // ...
}
```

**After:**
```csharp
private void EnsureWorldDb()
{
    var solutionDir = FindSolutionDirectory();
    var contentPackDir = Path.Combine(solutionDir,
        "src", "Oravey2.Windows", "bin", "Debug", "net10.0",
        "ContentPacks", "Oravey2.Apocalyptic.NL.NH");

    if (!Directory.Exists(contentPackDir))
        throw new InvalidOperationException(
            $"Content pack not found at '{contentPackDir}'. Build Oravey2.Windows first.");

    // Use a test-specific world.db path via env var
    var testDbDir = Path.Combine(Path.GetTempPath(), "Oravey2-UITests");
    Directory.CreateDirectory(testDbDir);
    var worldDbPath = Path.Combine(testDbDir, "world.db");
    Environment.SetEnvironmentVariable("ORAVEY2_WORLD_DB", worldDbPath);

    // Check if region already imported
    using var checkStore = new WorldMapStore(worldDbPath);
    var existing = checkStore.GetRegionByName("Noord-Holland");
    if (existing != null)
        return;
    checkStore.Dispose();

    // Import from the content pack's per-pack world.db (step 04 output)
    var packDbPath = Path.Combine(contentPackDir, "world.db");
    if (File.Exists(packDbPath))
    {
        // Fast path: copy the pre-built world.db from the content pack
        File.Copy(packDbPath, worldDbPath, overwrite: true);
        return;
    }

    // Fallback: import from content pack source data
    using var store = new WorldMapStore(worldDbPath);
    var importer = new ContentPackImporter(store);
    var result = importer.Import(contentPackDir);

    if (result.ChunksWritten == 0)
        throw new InvalidOperationException(
            $"Content pack import produced 0 chunks. Warnings: {string.Join("; ", result.Warnings)}");
}
```

### 8.3 Pass env var to game process

The `ORAVEY2_WORLD_DB` env var must be visible to the child game process.
In `CreateOptions()`, add the env var to game arguments or environment:

```csharp
protected override StrideTestContextOptions CreateOptions()
{
    EnsureWorldDb();

    var options = base.CreateOptions();
    options.GameArguments = ["--automation", "--scenario", "Noord-Holland"];
    options.EnvironmentVariables["ORAVEY2_WORLD_DB"] =
        Environment.GetEnvironmentVariable("ORAVEY2_WORLD_DB")!;
    options.StartupTimeoutMs = 30000;
    options.ConnectionTimeoutMs = 15000;
    options.DefaultTimeoutMs = 5000;
    return options;
}
```

> **Note:** If `StrideTestContextOptions` doesn't have an
> `EnvironmentVariables` dictionary, add one and propagate it in the
> `StrideTestFixtureBase` when starting the process. Alternatively, since
> env vars inherit from the parent process, setting it via
> `Environment.SetEnvironmentVariable` before launch may be sufficient.

### 8.4 Update `MapGenTestFixture` — env var for export tests

File: `tests/Oravey2.UITests/MapGen/MapGenTestFixture.cs`

The MapGen export tests don't need a user world.db — they verify the per-pack
world.db. But the `MAPGEN_DATA_ROOT` env var is already set. No additional
changes expected unless the MapGen export path resolution uses
`WorldDbPaths`. In that case, no env var change is needed — MapGen writes to
`_state.ContentPackPath/world.db` regardless of `ORAVEY2_WORLD_DB`.

### 8.5 New UI tests for Import Region (Stride)

New test class for the Import Region button. These will be written after the
UI is implemented in step 07.

File: `tests/Oravey2.UITests/ImportRegionTests.cs`

```csharp
/// <summary>
/// Tests for the Import Region button on the scenario selector.
/// </summary>
public class ImportRegionTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void ImportRegion_ButtonIsVisible()
    {
        // Navigate to scenario selector (New Game)
        _fixture.Context.Click("New Game");
        var labels = _fixture.Context.Query<string[]>("GetButtonLabels");
        Assert.Contains("Import Region", labels);
    }

    [Fact]
    public void ImportRegion_ShowsImportOverlay()
    {
        _fixture.Context.Click("New Game");
        _fixture.Context.Click("Import Region");
        var labels = _fixture.Context.Query<string[]>("GetButtonLabels");
        Assert.Contains("Back", labels);
    }

    [Fact]
    public void ImportRegion_ListsNoordHolland()
    {
        _fixture.Context.Click("New Game");
        _fixture.Context.Click("Import Region");
        // The Import overlay should show a card for Noord-Holland
        // (requires the content pack to have a world.db from step 04)
        var labels = _fixture.Context.Query<string[]>("GetButtonLabels");
        Assert.Contains("Import Noord-Holland", labels);
    }
}
```

> **Note:** These tests depend on the content pack having a `world.db`
> (from step 04). If the pack doesn't have one yet, the test will need
> to create it as a prerequisite.

### 8.6 Cleanup old test artifacts

After all tests pass with the new architecture, clean up:
- Remove any leftover `content/world.db` file
- Remove `src/Oravey2.Windows/bin/Debug/net10.0/world.db` if it was manually
  created

## Dependencies

- Step 04 (MapGen export path — needed for the `CreatesWorldDb` test)
- Step 06 (game reads user library — needed for `ORAVEY2_WORLD_DB` support)
- Step 07 (Import Region UI — needed for `ImportRegionTests`)

## Estimated scope

- Modified files: 2 (`NoordHollandExportTests.cs`, `NoordHollandSmokeTests.cs`)
- New files: 1 (`ImportRegionTests.cs`)
