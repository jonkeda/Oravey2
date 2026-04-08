# Test Guide: World Creation

Covers **Steps 1 (Data Model), 2 (SQLite Storage), 8 (WorldTemplate Pipeline), 9 (Procedural Generation)**.

---

## Step 1 — Build the Solution

```powershell
dotnet build Oravey2.sln
```

**Expected:** Build succeeds with zero errors. All projects compile, including `Oravey2.WorldTemplateTool`.

---

## Step 2 — Run Unit Tests

```powershell
dotnet test tests/Oravey2.Tests
```

**Expected:** All 1200+ tests pass. This confirms the data model (TileData, LiquidType, ChunkData) and SQLite serialization layer work correctly.

---

## Step 3 — Create a WorldTemplate from Noord-Holland

### Required Data Files

| File | Location | Source |
|------|----------|--------|
| SRTM elevation tiles | `data/srtm/*.hgt` | [USGS EarthExplorer](https://earthexplorer.usgs.gov/) — download tiles covering 52°N 4°E to 53°N 5°E |
| OSM extract | `data/noordholland.osm.pbf` | [Geofabrik](https://download.geofabrik.de/europe/netherlands/noord-holland.html) |

### Run the WorldTemplate Tool

```powershell
dotnet run --project tools/Oravey2.WorldTemplateTool -- `
  --srtm data/srtm `
  --osm data/noordholland.osm.pbf `
  --output content/noordholland.worldtemplate `
  --name NoordHolland
```

### What to Verify

| # | Check | Expected |
|---|-------|----------|
| 1 | Tool runs without errors | Progress messages printed, completes in < 5 minutes |
| 2 | Summary output | Reports extracted town count (50+), road segments (1000+), water features (100+) |
| 3 | Output file created | `content/noordholland.worldtemplate` exists, size > 10 MB |
| 4 | Elevation data processed | Log mentions SRTM tile loading, min/max elevation values |

---

## Step 4 — Generate a Game World (New Game)

### Launch the Game

```powershell
dotnet run --project src/Oravey2.Windows
```

### Test Procedure

| # | Action | Expected |
|---|--------|----------|
| 1 | Game window opens | Stride engine window appears, start menu is shown |
| 2 | Select "New Game" | World generation begins using the active content pack (Post-Apocalyptic) |
| 3 | Wait for generation | Progress indicators show town placement, road generation, building placement |
| 4 | World loads | Player spawns in Purmerend (or nearest generated town), terrain visible |
| 5 | Press Escape → check game state | Should show "Exploring" — world is fully loaded |

### Direct Scenario Launch (Alternative)

To skip the start menu and load a pre-generated world from `world.db`:

```powershell
dotnet run --project src/Oravey2.Windows -- --scenario generated
```

### What to Verify

| # | Check | Expected |
|---|-------|----------|
| 1 | Towns exist | Walking around reveals named town areas with buildings and structures |
| 2 | Roads connect towns | Asphalt/concrete ribbons run between settlements |
| 3 | Water features present | Rivers, canals, and lakes visible on the terrain |
| 4 | Elevation matches real terrain | Noord-Holland is mostly flat with some polders below sea level |
| 5 | Content pack applied | Post-apocalyptic theme: ruined buildings, dead vegetation, hazard zones |

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| "No SRTM tiles found" | Missing or wrong path | Ensure `.hgt` files cover the OSM extract area |
| "OSM file not found" | Wrong `--osm` path | Check the `.osm.pbf` file exists at the specified path |
| World is empty/flat | WorldTemplate not in content pack | Ensure `.worldtemplate` is in `content/` folder and the content pack references it |
| Game crashes on New Game | Missing world.db | Run the WorldTemplate tool first, or use `--scenario terrain_test` for a test map |
