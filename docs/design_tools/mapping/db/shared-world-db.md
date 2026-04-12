# Shared world.db — Content Pack Import Architecture

## Problem

`world.db` currently lives in three different places depending on who writes it:

| Writer | Location | Survives rebuild? |
|---|---|---|
| Game (GameBootstrapper) | `AppContext.BaseDirectory/world.db` (bin/Debug/) | No |
| MapGen (AssemblyStepViewModel) | `content/../world.db` (content pack parent) | Yes, but game never reads it |
| UI Tests (NoordHollandTestFixture) | `bin/Debug/net10.0/world.db` | No |

This means:
- MapGen exports are invisible to the game
- Every `dotnet build` can wipe the game's world.db
- Noord-Holland doesn't appear in the scenario selector unless tests have been run first
- No way for a user to choose which regions to load

## Design

### Principle: Content packs are distributable, world.db is the user's library

```
┌─────────────────────────────────────┐
│  Content Packs (shippable)          │
│                                     │
│  content/Oravey2.Apocalyptic.NL.NH/ │
│    ├── manifest.json                │
│    ├── overworld/                   │
│    ├── towns/                       │
│    └── world.db  ◄── per-pack DB   │
│                      (exported by   │
│                       MapGen)       │
└──────────────┬──────────────────────┘
               │
               │  MSBuild copies to
               │  bin/.../ContentPacks/
               ▼
┌─────────────────────────────────────┐
│  Game (Oravey2.Windows)             │
│                                     │
│  ContentPacks/                      │
│    └── Oravey2.Apocalyptic.NL.NH/   │
│          └── world.db               │
│                                     │
│  Scenario Selector:                 │
│    ├── 5 built-in (debug.db)        │
│    ├── Noord-Holland (world.db) ────┼── imported regions
│    └── [Import Region...] button    │
│                                     │
│  %LOCALAPPDATA%/Oravey2/world.db    │
│    ◄── user's persistent library    │
└─────────────────────────────────────┘
```

### Three layers

| Layer | What | Where | Who writes | Who reads |
|---|---|---|---|---|
| **Content pack** | Source data + per-pack `world.db` | `content/{packId}/world.db` → copied to `ContentPacks/{packId}/world.db` | MapGen "Export to Game DB" | Game (import source) |
| **User library** | Accumulated imported regions | `%LOCALAPPDATA%/Oravey2/world.db` | Game "Import Region" button | Game (scenario list) |
| **Debug DB** | 5 built-in scenarios | `AppContext.BaseDirectory/debug.db` | Auto-seeded on first launch | Game (scenario list) |

### Why per-pack world.db?

Each content pack includes its own `world.db` as a pre-compiled artifact:

1. **Shippable** — users download a content pack folder, the world.db is ready to import
2. **No runtime compilation** — the game doesn't need to parse `overworld/`, `towns/`, etc.
3. **Versioned** — re-exporting from MapGen updates the pack's world.db in source control
4. **Offline** — no build step or tool dependency at game runtime

### Why user-location world.db?

The user's imported regions live in `%LOCALAPPDATA%/Oravey2/world.db`:

1. **Survives rebuilds** — not in `bin/Debug/`
2. **User choice** — only regions the user explicitly imports appear
3. **Accumulates** — import Noord-Holland today, Zeeland next month, both stay
4. **Resettable** — delete the file to start fresh

## Changes

### 1. New: `WorldDbPaths` helper (Oravey2.Core)

Single source of truth for the user library path:

```csharp
// Oravey2.Core/Data/WorldDbPaths.cs
namespace Oravey2.Core.Data;

public static class WorldDbPaths
{
    /// <summary>
    /// User's persistent world.db in LocalApplicationData.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public static string GetUserWorldDbPath()
    {
        var envOverride = Environment.GetEnvironmentVariable("ORAVEY2_WORLD_DB");
        if (!string.IsNullOrEmpty(envOverride))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(envOverride)!);
            return envOverride;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Oravey2");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "world.db");
    }

    /// <summary>
    /// Finds the per-pack world.db inside a content pack directory.
    /// Returns null if the pack has no world.db (not yet exported).
    /// </summary>
    public static string? GetPackWorldDbPath(string contentPackDir)
    {
        var path = Path.Combine(contentPackDir, "world.db");
        return File.Exists(path) ? path : null;
    }
}
```

### 2. MapGen: Export writes world.db into the content pack

**Before** (AssemblyStepViewModel):
```csharp
var dbPath = Path.Combine(
    Path.GetDirectoryName(_state.ContentPackPath)!,
    "world.db");
```

**After:**
```csharp
var dbPath = Path.Combine(_state.ContentPackPath, "world.db");
```

The world.db lands inside the content pack directory itself (e.g., `content/Oravey2.Apocalyptic.NL.NH/world.db`). MSBuild's `ContentPacks` glob already copies `**\*` from `content/` to the game output — so the world.db is automatically included. No csproj changes needed.

### 3. ContentPackImporter: Upsert instead of insert

Currently `Import()` always inserts a new continent + region. Re-importing creates duplicates.

Add a replace check:

```csharp
public ImportResult Import(string contentPackPath)
{
    var result = new ImportResult();
    var manifest = ReadJsonOrDefault<ManifestJson>(manifestPath, result);
    result.RegionName = manifest.Name;

    // Replace existing region if re-importing
    var existing = _store.GetRegionByName(manifest.Name);
    if (existing != null)
    {
        _store.DeleteRegion(existing.Id);
        result.Warnings.Add($"Replaced existing region '{manifest.Name}'.");
    }

    var continentId = _store.InsertContinent(manifest.Name, null, 1, 1);
    var regionId = _store.InsertRegion(continentId, manifest.Name, 0, 0);
    // ... rest unchanged
}
```

Requires new `WorldMapStore.DeleteRegion(long regionId)` that cascades to chunks, entity_spawn, poi, linear_feature, etc.

### 4. Game: Read from user library, not AppContext.BaseDirectory

**Before** (GameBootstrapper):
```csharp
var worldDbPath = Path.Combine(AppContext.BaseDirectory, "world.db");
```

**After:**
```csharp
var worldDbPath = WorldDbPaths.GetUserWorldDbPath();
```

### 5. New: `ContentPackImportService` (Oravey2.Core)

Bridges content packs → user world.db. Discovers which content packs have a `world.db` ready to import, and performs the import:

```csharp
// Oravey2.Core/Content/ContentPackImportService.cs
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
    /// </summary>
    public IReadOnlyList<ImportableRegion> GetImportableRegions()
    {
        var regions = new List<ImportableRegion>();
        foreach (var pack in _packs.Packs)
        {
            var packDbPath = WorldDbPaths.GetPackWorldDbPath(pack.Directory);
            if (packDbPath == null) continue;

            // Read region names from the pack's world.db
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
    /// Imports all regions from a content pack's world.db into the user's world.db.
    /// Uses upsert — re-importing replaces existing regions.
    /// </summary>
    public ImportResult ImportRegion(string contentPackDir)
    {
        var userDbPath = WorldDbPaths.GetUserWorldDbPath();
        using var userStore = new WorldMapStore(userDbPath);
        var importer = new ContentPackImporter(userStore);
        return importer.Import(contentPackDir);
    }
}

public sealed record ImportableRegion(
    string PackId,
    string PackName,
    string RegionName,
    string Description,
    string PackDirectory);
```

### 6. New: "Import Region" button on Scenario Selector

Add an **Import Region** button to the bottom bar of `ScenarioSelectorScript`, next to Start and Cancel:

```
┌──────────────────────────────────────────────────────┐
│                 SELECT SCENARIO                       │
│                                                       │
│  ┌───────────────────┐  ┌──────────────────────────┐ │
│  │ Haven Town        │  │ Noord-Holland             │ │
│  │ Scorched Outskirts│  │                           │ │
│  │ Combat Arena      │  │ Post-apocalyptic Noord-   │ │
│  │ Empty World       │  │ Holland region...          │ │
│  │ Terrain Test      │  │                           │ │
│  │ ──────────────    │  │ Biome: wasteland          │ │
│  │ Noord-Holland  ◄──│  │ Towns: 1                  │ │
│  │                   │  │                           │ │
│  └───────────────────┘  └──────────────────────────┘ │
│                                                       │
│  [ Import Region ]       [ Start ]    [ Cancel ]      │
│                                                       │
└──────────────────────────────────────────────────────┘
```

Clicking **Import Region** opens a sub-overlay listing available content packs:

```
┌──────────────────────────────────────────────────────┐
│                 IMPORT REGION                         │
│                                                       │
│  Available content packs:                             │
│                                                       │
│  ┌──────────────────────────────────────────────┐    │
│  │ Noord-Holland                                 │    │
│  │ Post-apocalyptic Noord-Holland region.        │    │
│  │ Pack: oravey2.apocalyptic.nl.nh              │    │
│  │                            [ Import ]         │    │
│  └──────────────────────────────────────────────┘    │
│                                                       │
│  ┌──────────────────────────────────────────────┐    │
│  │ (no world.db)                                 │    │
│  │ Pack: oravey2.fantasy                         │    │
│  │ Run MapGen to export this region first.       │    │
│  └──────────────────────────────────────────────┘    │
│                                                       │
│                              [ Back ]                 │
└──────────────────────────────────────────────────────┘
```

Clicking **Import** on a pack:
1. Calls `ContentPackImportService.ImportRegion(packDir)`
2. Shows brief status: "Imported 'Noord-Holland': 1 town, 4 chunks, 12 entity spawns."
3. Returns to scenario selector, which rebuilds its list — Noord-Holland now appears

## End-to-end flows

### Content creator flow (MapGen)

```
1. Run MapGen MAUI app
2. Complete pipeline steps 1–7 for Noord-Holland
3. Step 8 Assembly: Click "Export to Game DB"
4. MapGen writes content/Oravey2.Apocalyptic.NL.NH/world.db
5. Commit content pack (including world.db) to source control
6. Ship/distribute the content pack folder
```

### Player flow (Game)

```
1. Launch Oravey2
2. Click "New Game" → scenario selector
3. Only built-in scenarios shown (first launch)
4. Click "Import Region"
5. See list: "Noord-Holland" from Oravey2.Apocalyptic.NL.NH pack
6. Click "Import" → region copied to %LOCALAPPDATA%/Oravey2/world.db
7. Status: "Imported 'Noord-Holland': 1 town, 4 chunks, 12 entity spawns."
8. Back to scenario selector → Noord-Holland now in list
9. Select Noord-Holland → Start
```

### Re-import flow (updated content)

```
1. Content creator updates Noord-Holland, re-exports in MapGen
2. User gets updated content pack (git pull, download, etc.)
3. Game launches, user clicks "Import Region"
4. Clicks "Import" on Noord-Holland
5. ContentPackImporter detects existing region → deletes old data → inserts new
6. Status: "Replaced existing region 'Noord-Holland'. 1 town, 4 chunks, ..."
```

## Implementation steps

| # | Change | Files | Depends on |
|---|---|---|---|
| 1 | Add `WorldDbPaths` helper | `Oravey2.Core/Data/WorldDbPaths.cs` (new) | — |
| 2 | Add `DeleteRegion` cascade to store | `WorldMapStore.cs` | — |
| 3 | Add upsert to `ContentPackImporter.Import` | `ContentPackImporter.cs` | 2 |
| 4 | MapGen exports world.db into content pack | `AssemblyStepViewModel.cs` | — |
| 5 | Add `ContentPackImportService` | `Oravey2.Core/Content/ContentPackImportService.cs` (new) | 1, 2, 3 |
| 6 | Game reads user library path | `GameBootstrapper.cs` | 1 |
| 7 | Add "Import Region" UI to scenario selector | `ScenarioSelectorScript.cs` | 5, 6 |
| 8 | Update UI tests for new paths | `NoordHollandSmokeTests.cs`, `NoordHollandExportTests.cs` | 1, 4 |

Steps 1–3 are the foundation. Steps 4–6 are independent consumers. Step 7 is the UI. Step 8 adapts existing tests.

## Test strategy

### Unit tests

- `WorldDbPaths.GetUserWorldDbPath()` returns `%LOCALAPPDATA%/Oravey2/world.db`
- `WorldDbPaths.GetUserWorldDbPath()` respects `ORAVEY2_WORLD_DB` env var
- `WorldDbPaths.GetPackWorldDbPath()` returns null when no world.db exists
- `ContentPackImporter.Import` replaces existing region (upsert)
- `ContentPackImportService.GetImportableRegions` finds packs with world.db
- `ContentPackImportService.GetImportableRegions` skips packs without world.db

### Game UI tests (Stride)

- Import Region button visible on scenario selector
- Import overlay lists available content packs
- Importing a region adds it to scenario list
- Re-importing replaces without duplicates

### MapGen UI tests

- Export writes world.db inside content pack directory
- Existing `NoordHollandExportTests` updated to check new path

## Out of scope

- **Remove individual regions** — users can delete `%LOCALAPPDATA%/Oravey2/world.db` to reset all. A per-region remove button is a future enhancement.
- **Content pack versioning** — no version comparison on import. Re-import always replaces.
- **Multi-user / cloud sync** — LocalApplicationData is per-machine, per-user. Fine for single-player.
- **Content pack download / marketplace** — future feature. For now, packs are distributed as folders.
