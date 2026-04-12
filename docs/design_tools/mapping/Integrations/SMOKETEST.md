# Integration Smoke Test Guide

How to manually verify the integration refactors (steps i01–i13).

## Prerequisites

```powershell
cd E:\repos\Private\Oravey2
dotnet build src/Oravey2.Core/Oravey2.Core.csproj
dotnet build src/Oravey2.Windows/Oravey2.Windows.csproj
dotnet test tests/Oravey2.Tests/Oravey2.Tests.csproj
```

All three must succeed. The test run should show 1749+ passed (3
pre-existing failures in `RegionPresetTests` are expected).

---

## Test 1 — Scenario selector shows DB regions

### Setup: seed debug.db

The game auto-creates `debug.db` if it doesn't exist (via
`WorldDbSeeder` in `GameBootstrapper`). If it doesn't, seed manually:

```csharp
// quick script or add temporarily to Program.cs
using var store = new WorldMapStore("debug.db");
new WorldDbSeeder(store).SeedAll();
```

Or copy the `debug.db` into the game's output directory:
`src/Oravey2.Windows/bin/Debug/net10.0/`

### Steps

1. Launch the game:
   ```powershell
   dotnet run --project src/Oravey2.Windows
   ```
2. On the Start Menu, click **New Game**
3. The scenario selector should show:
   - The 5 hardcoded entries (Haven Town, Scorched Outskirts, etc.)
   - Any DB regions from `world.db` (if present)
   - DB regions from `debug.db` tagged with `[DEBUG]` prefix

### Pass criteria
- Selector opens without crash
- DB regions appear in the list alongside hardcoded ones
- Selecting any scenario loads without crash

---

## Test 2 — Built-in scenarios still work via RegionLoader

The `LoadAndWireScenario` in `GameBootstrapper` now calls
`RegionLoader.LoadRegion()` → `ScenarioLoader.SyncFromRegion()`.

### Steps

For each of these scenarios, select from the scenario list and verify:

| Scenario | What to check |
|----------|--------------|
| **Haven Town** | Player spawns, 4 NPCs visible, zone exit gate present |
| **Scorched Outskirts** | Player spawns, radrat enemies visible, combat works |
| **Combat Arena** | Player spawns, 3 enemies, can engage combat |
| **Empty World** | Player spawns on flat terrain, no NPCs or enemies |
| **Terrain Test** | Height variation visible, roads/river rendered |

### Pass criteria
- Each scenario loads without crash
- Player entity appears and can move (WASD)
- Camera follows player
- HUD is visible

---

## Test 3 — Zone transition (Town ↔ Wasteland)

### Steps

1. Start **Haven Town**
2. Walk to the zone exit gate (east side of map)
3. Step into the exit trigger

### Pass criteria
- Screen transitions to the Wasteland zone
- Player spawns at the wasteland entry point
- Enemies are present in wasteland
- Walking back to the town exit in wasteland returns to town

---

## Test 4 — Save/Load with region tracking

### Steps

1. Start **Haven Town**, walk to a non-default position
2. Press **F5** (QuickSave)
3. Press **Escape** → **Quit to Menu**
4. Click **Continue**

### Pass criteria
- Game loads back into Haven Town (not a crash or wrong scenario)
- Player position is near where you saved
- If `SaveStateStore.GetCurrentRegion()` was set, the region-aware
  path is used; otherwise the legacy "town" fallback loads

---

## Test 5 — Content pack import (pipeline output)

### Prerequisites
Run the MapGen pipeline to produce a content pack, or use an existing
one from `content/Oravey2.Apocalyptic.NL.NH/` (if populated).

### Steps

1. Launch the game
2. Look for the content pack in the scenario list (it should appear
   if `world.db` was populated via the Export button in the tool)
3. Select the imported region

**Alternative** — test the tool-side export:
1. Run the MapGen MAUI app
2. Complete pipeline step 8 (Assembly)
3. Click **Export to Game DB**
4. Verify the status shows "Exported N towns, M chunks"
5. Copy the resulting `world.db` to the game output directory
6. Launch the game — the exported region should appear in the selector

### Pass criteria
- Export completes without error
- The region appears in the game's scenario list
- Selecting it loads terrain and buildings

---

## Test 6 — ContentPackImporter (game-side auto-import)

### Steps

1. Place a content pack folder in `ContentPacks/` in the game output
   directory (must have `manifest.json`, `towns/`, etc.)
2. Launch the game
3. The auto-import should detect the pack and import into `world.db`

### Pass criteria
- No crash on startup
- The imported region appears in the scenario selector
- Loading it shows the town terrain

---

## Test 7 — Unit test verification

Run the focused test suites for all new code:

```powershell
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~LinearFeatureTypeTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~SurfaceTypeTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~DtoCompatibilityTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~ChunkSplitterTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~WorldMapStoreQueryTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~WorldDbSeederTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~EntitySpawnerTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~RegionLoaderTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~ContentPackImporterTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~ContentPackExporterTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~RegionDiscoveryTests"
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~ZoneTransitionTests"
```

Or run all at once:

```powershell
dotnet test tests/Oravey2.Tests
```

### Pass criteria
- All new tests pass (80+ tests across the suites above)
- No regressions in existing tests (1749+ passing)

---

## Known issues

- The 3 `RegionPresetTests` failures are pre-existing (missing
  `data/presets/` directory) — not related to integration work
- The game needs `debug.db` in the output directory for DB-sourced
  debug scenarios to appear. If it's missing, only the hardcoded
  `Scenarios` array shows (this is the intended fallback)
- Entity spawner factories create placeholder entities (basic
  positioned entities without full gameplay components like
  `NpcComponent`). Full component wiring is future work.
- `ContentPackImporter` auto-import only runs if content packs
  exist in the `ContentPacks/` directory with a valid `manifest.json`
