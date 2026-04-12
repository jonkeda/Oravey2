# Smoke Test: Noord-Holland Pipeline → Game

End-to-end test of the MapGen content pack export and in-game loading
for the `Oravey2.Apocalyptic.NL.NH` (Noord-Holland) content pack.

## Prerequisites

```powershell
cd E:\repos\Private\Oravey2
dotnet build src/Oravey2.Core/Oravey2.Core.csproj
dotnet build src/Oravey2.MapGen/Oravey2.MapGen.csproj
dotnet build src/Oravey2.Windows/Oravey2.Windows.csproj
```

All must build with 0 errors.

### Content pack location

The Noord-Holland pack is at:
```
content/Oravey2.Apocalyptic.NL.NH/
├── manifest.json          → id: oravey2.apocalyptic.nl.nh
├── overworld/
│   ├── world.json         → region "noord-holland", 1×1 chunks
│   ├── roads.json         → [] (empty)
│   └── water.json         → [] (empty)
└── towns/
    ├── Island Haven/      → 32×32 layout, 6 buildings, props, 2 zones
    └── The Spire/         → design.json only (no layout yet)
```

---

## Test 1 — Export to Game DB (via ContentPackExporter)

### Option A: MapGen app UI

1. Run the MapGen MAUI app:
   ```powershell
   dotnet run --project src/Oravey2.MapGen.App
   ```
2. Complete pipeline steps 1–7 (or load a saved state that reaches
   step 8 Assembly)
3. Click **Export to Game DB**
4. The status bar should show something like:
   ```
   Exported 'Noord-Holland' to world.db: 1 towns, N chunks, ...
   ```
5. Verify `world.db` was created next to the content pack:
   ```powershell
   Test-Path content/Oravey2.Apocalyptic.NL.NH/world.db
   ```

### Option B: Script (bypass UI)

```csharp
#r "src/Oravey2.Core/bin/Debug/net10.0/Oravey2.Core.dll"
#r "src/Oravey2.MapGen/bin/Debug/net10.0/Oravey2.MapGen.dll"
using Oravey2.MapGen.Pipeline;

var exporter = new ContentPackExporter();
var result = exporter.Export(
    @"content\Oravey2.Apocalyptic.NL.NH",
    @"content\Oravey2.Apocalyptic.NL.NH\world.db");
Console.WriteLine($"Region: {result.RegionName}");
Console.WriteLine($"Towns: {result.TownsImported}");
Console.WriteLine($"Chunks: {result.ChunksWritten}");
Console.WriteLine($"Entity spawns: {result.EntitySpawnsInserted}");
Console.WriteLine($"Warnings: {result.Warnings.Count}");
```

### Pass criteria

- `world.db` is created
- `RegionName` = "Noord-Holland"
- `TownsImported` ≥ 1 (Island Haven; The Spire has no layout.json)
- `ChunksWritten` ≥ 1
- No unhandled exceptions

---

## Test 2 — Verify DB contents

After export, inspect the database:

```powershell
# Requires sqlite3 on PATH or use dotnet-script
sqlite3 content/Oravey2.Apocalyptic.NL.NH/world.db "
  SELECT 'Continents:', count(*) FROM continent;
  SELECT 'Regions:', count(*) FROM region;
  SELECT 'Chunks:', count(*) FROM chunk;
  SELECT 'Entity spawns:', count(*) FROM entity_spawn;
  SELECT 'Linear features:', count(*) FROM linear_feature;
  SELECT 'POIs:', count(*) FROM poi;
"
```

### Pass criteria

| Table | Expected |
|-------|----------|
| continent | 1 ("Noord-Holland") |
| region | 1 ("Noord-Holland") |
| chunk | ≥ 1 (from Island Haven 32×32 layout → at least 4 16×16 chunks) |
| entity_spawn | ≥ 6 (buildings) + props count |
| linear_feature | 0 (roads.json and water.json are empty) |
| poi | ≥ 1 (Island Haven as curated town POI) |

---

## Test 3 — Copy DB to game output and launch

```powershell
$gameOut = "src/Oravey2.Windows/bin/Debug/net10.0"
Copy-Item content/Oravey2.Apocalyptic.NL.NH/world.db $gameOut/world.db -Force
dotnet run --project src/Oravey2.Windows
```

### Steps

1. On the Start Menu, click **New Game**
2. The scenario selector should show:
   - The 5 built-in scenarios (Haven Town, etc.)
   - **Noord-Holland** from `world.db`
3. Select **Noord-Holland**

### Pass criteria

- "Noord-Holland" appears in the scenario list (not tagged `[DEBUG]`)
- Selecting it loads without crash
- Player entity spawns on terrain
- Camera follows player
- HUD is visible

---

## Test 4 — Terrain verification

After loading Noord-Holland:

### What to check

| Element | Expected |
|---------|----------|
| Terrain mesh | Visible, not flat black (tile data from layout.json) |
| Tile count | 32×32 = 1024 tiles split into 16×16 chunks |
| Surface types | Mix of Dirt (0), Grass (1), Sand (2) per layout surface data |
| Height | Varies if layout.json has height data |

### Pass criteria

- Terrain renders with visible surface color variation
- Player can walk (WASD) across the terrain
- No Z-fighting or missing terrain patches

---

## Test 5 — Entity spawns

### What to check

| Entity type | Expected |
|-------------|----------|
| Buildings | ≥ 6 entities (The Beacon, etc. from buildings.json) |
| Props | Multiple sphere primitives from props.json |
| Player | 1 (capsule at spawn position) |

### Pass criteria

- Building entities are visible at their footprint positions
- Prop entities are visible
- No entities at (0,0,0) pileup (each has correct local tile offset)

---

## Test 6 — Save/Load round-trip

1. Walk to a non-default position in Noord-Holland
2. Press **F5** (QuickSave)
3. Press **Escape** → **Quit to Menu**
4. Click **Continue**

### Pass criteria

- Game loads back into Noord-Holland (not Haven Town)
- Player position is near where you saved
- `SaveStateStore.GetCurrentRegion()` returns "Noord-Holland"

---

## Test 7 — Re-export idempotency

1. Run the export again (same content pack → same `world.db`)
2. Launch the game

### Pass criteria

- Export does not crash (duplicate continent/region handling)
- Game still shows one "Noord-Holland" entry (not duplicated)
- Loading still works

**Note:** Currently `ContentPackImporter` always inserts new rows.
If this test fails with a unique constraint error, that's a known
gap — the importer needs upsert logic or a "drop and recreate"
approach.

---

## Troubleshooting

| Symptom | Likely cause |
|---------|-------------|
| "Region 'Noord-Holland' not found" | `world.db` not in game output dir |
| Noord-Holland not in selector | `world.db` exists but has no region rows — re-export |
| Terrain is flat/black | layout.json surface data not imported correctly |
| No entity spawns visible | buildings.json/props.json import failed — check warnings |
| Crash on export | Content pack structure mismatch — verify manifest.json exists |
