# Step 20 — Region Folder Layout & RegionPreset Paths

**Work streams:** WS-Models (Data models), WS-FileLayout (Directory restructure)
**Depends on:** Step 01 (RegionPreset)
**User-testable result:** `RegionPreset` computes all paths from its `Name`. The old `DefaultSrtmDir`/`DefaultOutputDir` properties are removed. Unit tests pass for path resolution.

---

## Goals

1. Replace the flat `data/srtm` + `content/` layout with per-region folders under `data/regions/<name>/`.
2. `RegionPreset` computes all paths (srtm, osm, output) from its `Name` — no more configurable default directories.
3. Each region is fully self-contained: preset, downloads, and output live in one folder.
4. Shared cache (`data/cache/`) stays separate.

---

## Problem

The current flat layout only works for one region. Adding a second region creates ambiguity — SRTM tiles, OSM files, and world templates are mixed together. `DefaultSrtmDir` and `DefaultOutputDir` are configurable per-preset but always point to the same shared directories.

---

## Target Layout

```
data/
  regions/
    noord-holland/
      region.json              ← RegionPreset serialized
      srtm/
        N52E004.hgt.gz
        N52E005.hgt.gz
      osm/
        noord-holland-latest.osm.pbf
      output/
        noord-holland.worldtemplate
  cache/
    geofabrik-index-v1.json.gz   ← shared, not region-specific
```

---

## Tasks

### 20.1 — Update RegionPreset Record

File: `src/Oravey2.MapGen/WorldTemplate/RegionPreset.cs`

- [ ] Remove `DefaultSrtmDir` property
- [ ] Remove `DefaultOutputDir` property
- [ ] Remove `OsmFileName` property (now computed from `Name`)
- [ ] Add computed path properties:
  ```csharp
  // Base directory for this region
  [JsonIgnore]
  public string RegionDir => Path.Combine("data", "regions", Name);

  [JsonIgnore]
  public string SrtmDir => Path.Combine(RegionDir, "srtm");

  [JsonIgnore]
  public string OsmDir => Path.Combine(RegionDir, "osm");

  [JsonIgnore]
  public string OutputDir => Path.Combine(RegionDir, "output");

  [JsonIgnore]
  public string OsmFilePath => Path.Combine(OsmDir, $"{Name}-latest.osm.pbf");

  [JsonIgnore]
  public string OutputFilePath => Path.Combine(OutputDir, $"{Name}.worldtemplate");

  [JsonIgnore]
  public string PresetFilePath => Path.Combine(RegionDir, "region.json");
  ```
- [ ] Add `EnsureDirectories()` method:
  ```csharp
  public void EnsureDirectories()
  {
      Directory.CreateDirectory(SrtmDir);
      Directory.CreateDirectory(OsmDir);
      Directory.CreateDirectory(OutputDir);
  }
  ```

### 20.2 — Update Noord-Holland Preset

File: `data/presets/noordholland.regionpreset`

- [ ] Remove `defaultSrtmDir`, `defaultOutputDir`, and `osmFileName` from JSON
- [ ] Keep: `name`, `displayName`, bounds, `osmDownloadUrl`, `defaultCullSettings`

### 20.3 — Add Shared Cache Directory Constant

File: `src/Oravey2.MapGen/WorldTemplate/RegionPreset.cs` (or a new `DataPaths` static class)

- [ ] Add constant for shared cache:
  ```csharp
  public static string CacheDir => Path.Combine("data", "cache");
  ```

### 20.4 — Unit Tests

File: `tests/Oravey2.Tests/WorldTemplate/RegionPresetTests.cs`

- [ ] `RegionDir_ComputesFromName` — `Name = "noord-holland"` → `data/regions/noord-holland`
- [ ] `SrtmDir_IsUnderRegionDir` — verify `data/regions/noord-holland/srtm`
- [ ] `OsmFilePath_UsesName` — verify `data/regions/noord-holland/osm/noord-holland-latest.osm.pbf`
- [ ] `OutputFilePath_UsesName` — verify `data/regions/noord-holland/output/noord-holland.worldtemplate`
- [ ] `PresetFilePath_IsRegionJson` — verify `data/regions/noord-holland/region.json`
- [ ] `RoundTrip_Json_OmitsComputedPaths` — serialize/deserialize, computed paths not in JSON
- [ ] `EnsureDirectories_CreatesAllSubdirs` — call in temp dir, verify 3 subdirs exist

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~RegionPreset"
```

Build succeeds. Computed paths resolve correctly regardless of working directory.
