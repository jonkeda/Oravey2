# Step 01 — CullSettings & Region Presets

**Work streams:** WS-Models (Data models & serialization)
**Depends on:** None
**User-testable result:** Unit tests pass for `CullSettings` and `RegionPreset` JSON round-trip. Noord-Holland preset file exists and deserializes correctly.

---

## Goals

1. Define a unified `CullSettings` record containing all town, road, and water culling parameters.
2. Define a `RegionPreset` record that bundles geographic bounds, download URLs, and default cull settings.
3. Ship a built-in Noord-Holland preset file.
4. JSON serialization round-trips correctly for both types.

---

## Problem

The culling pipeline needs configurable parameters for town, road, and water filtering. These must be serializable to JSON so users can save/load/share settings, and bundled into region presets so adding a new geographic region is a drop-in JSON file.

Currently `OsmParser`, `SrtmParser`, and `WorldTemplateBuilder` exist in `Oravey2.MapGen/WorldTemplate/` but there are no configurable culling parameters or region presets.

---

## Tasks

### 1.1 — CullSettings Record

File: `src/Oravey2.MapGen/WorldTemplate/CullSettings.cs`

- [ ] Create `CullSettings` record with `init` properties
- [ ] Town culling section:
  - `TownMinCategory` (`TownCategory`, default `Village`)
  - `TownMinPopulation` (`int`, default `1_000`)
  - `TownMinSpacingKm` (`double`, default `5.0`)
  - `TownMaxCount` (`int`, default `30`)
  - `TownPriority` (`CullPriority` enum, default `Category`)
  - `TownAlwaysKeepCities` (`bool`, default `true`)
  - `TownAlwaysKeepMetropolis` (`bool`, default `true`)
- [ ] Road culling section:
  - `RoadMinClass` (`RoadClass` enum, default `Primary`)
  - `RoadAlwaysKeepMotorways` (`bool`, default `true`)
  - `RoadKeepNearTowns` (`bool`, default `true`)
  - `RoadTownProximityKm` (`double`, default `2.0`)
  - `RoadRemoveDeadEnds` (`bool`, default `true`)
  - `RoadDeadEndMinKm` (`double`, default `1.0`)
  - `RoadSimplifyGeometry` (`bool`, default `true`)
  - `RoadSimplifyToleranceM` (`double`, default `50.0`)
- [ ] Water culling section:
  - `WaterMinAreaKm2` (`double`, default `0.1`)
  - `WaterMinRiverLengthKm` (`double`, default `2.0`)
  - `WaterAlwaysKeepSea` (`bool`, default `true`)
  - `WaterAlwaysKeepLakes` (`bool`, default `true`)
- [ ] `Load(string path)` static method — deserializes from JSON file
- [ ] `Save(string path)` method — serializes to JSON with `WriteIndented = true`

### 1.2 — Supporting Enums

File: `src/Oravey2.MapGen/WorldTemplate/CullPriority.cs`

- [ ] Create `CullPriority` enum: `Population`, `Category`, `Spacing`

File: `src/Oravey2.MapGen/WorldTemplate/RoadClass.cs`

- [ ] Create `RoadClass` enum: `Motorway`, `Trunk`, `Primary`, `Secondary`, `Tertiary`, `Residential`
- [ ] Verify ordering matches `OsmParser` highway tag classification
- [ ] Check if `RoadSegment` already has a classification field — if not, add `RoadClass Classification` property

### 1.3 — RegionPreset Record

File: `src/Oravey2.MapGen/WorldTemplate/RegionPreset.cs`

- [ ] Create `RegionPreset` record:
  - `Name` (string, required) — internal key
  - `DisplayName` (string, required) — UI label
  - `NorthLat`, `SouthLat`, `EastLon`, `WestLon` (double, required) — bounding box
  - `OsmDownloadUrl` (string, required) — Geofabrik URL
  - `OsmFileName` (string?) — suggested filename
  - `DefaultSrtmDir` (string, default `"data/srtm"`)
  - `DefaultOutputDir` (string, default `"content"`)
  - `DefaultCullSettings` (`CullSettings`, default `new()`)
- [ ] `Load(string path)` static method
- [ ] `Save(string path)` method

### 1.4 — Noord-Holland Preset File

File: `data/presets/noordholland.regionpreset`

- [ ] Create `data/presets/` directory
- [ ] Write JSON preset:
  ```json
  {
    "name": "NoordHolland",
    "displayName": "Noord-Holland",
    "northLat": 53.0,
    "southLat": 52.2,
    "eastLon": 5.5,
    "westLon": 4.0,
    "osmDownloadUrl": "https://download.geofabrik.de/europe/netherlands/noord-holland-latest.osm.pbf",
    "osmFileName": "noordholland.osm.pbf",
    "defaultSrtmDir": "data/srtm",
    "defaultOutputDir": "content",
    "defaultCullSettings": { ... }
  }
  ```

### 1.5 — Unit Tests

File: `tests/Oravey2.Tests/WorldTemplate/CullSettingsTests.cs`

- [ ] `DefaultValues_AreCorrect` — `new CullSettings()` has expected defaults
- [ ] `RoundTrip_Json_PreservesAllProperties` — serialize → deserialize → assert all fields equal
- [ ] `Load_FromFile_ReturnsExpectedValues` — write known JSON, load, verify
- [ ] `Save_CreatesValidJson` — save, read raw text, verify valid JSON

File: `tests/Oravey2.Tests/WorldTemplate/RegionPresetTests.cs`

- [ ] `NoordHolland_Preset_LoadsCorrectly` — load from `data/presets/noordholland.regionpreset`
- [ ] `RoundTrip_Json_PreservesAllFields` — serialize → deserialize
- [ ] `DefaultCullSettings_IsEmbedded` — preset's `DefaultCullSettings` deserializes with correct values

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~CullSettings|RegionPreset"
```

**User test:** Open `data/presets/noordholland.regionpreset` in a text editor — valid JSON with all fields populated. Build succeeds.
