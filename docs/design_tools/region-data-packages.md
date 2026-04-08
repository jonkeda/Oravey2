# Design: Region Data Packages

## Status: Draft

---

## Overview

Reorganize the flat `data/` directory into **per-region folders** so multiple regions can coexist cleanly. When a user picks "Netherlands" or "Caribbean" via the region picker, all downloaded and generated files land in a self-contained folder.

---

## Problem

Current layout is flat and only works for one region at a time:

```
data/
  srtm/N52E004.hgt           ← shared? which region?
  noordholland.osm.pbf        ← loose file
  presets/noordholland.regionpreset
content/
  noordholland.worldtemplate   ← output mixed with content packages
```

Adding a second region creates a mess:

```
data/
  srtm/N52E004.hgt
  srtm/N18W066.hgt
  noordholland.osm.pbf
  caribbean.osm.pbf           ← which srtm tiles go with which?
content/
  noordholland.worldtemplate
  caribbean.worldtemplate      ← mixed with Apocalyptic/Fantasy content
```

---

## Proposed Layout

```
data/
  regions/
    noord-holland/
      region.json              ← RegionPreset (bounds, URLs, cull settings)
      srtm/
        N52E004.hgt.gz
        N52E005.hgt.gz
      osm/
        noord-holland-latest.osm.pbf
      output/
        noord-holland.worldtemplate
    caribbean/
      region.json
      srtm/
        N18W066.hgt.gz
        N17W065.hgt.gz
      osm/
        caribbean-latest.osm.pbf
      output/
        caribbean.worldtemplate
    china/
      region.json
      srtm/
        N39E116.hgt.gz
        N39E117.hgt.gz
      osm/
        china-latest.osm.pbf
      output/
        china.worldtemplate
  cache/
    geofabrik-index-v1.json.gz   ← shared, not region-specific
```

### Key decisions

| Question | Decision | Rationale |
|----------|----------|-----------|
| .NET projects? | **No** | These are downloaded runtime data, not authored/built assets. A `.csproj` adds nothing. |
| SRTM tile sharing? | **Per-region copies** | Tiles are 1.7 MB compressed. Duplicating across 2-3 overlapping regions costs ~5 MB vs. complex shared-pool logic. Simplicity wins. |
| Presets stay in `data/presets/`? | **Move into region folder** as `region.json` | Preset, data, and output all live together. Self-contained. |
| Output `.worldtemplate` location? | **Inside region folder** | Currently goes to `content/` which is for game content packages, not generated data. |
| Geofabrik index cache? | **Shared `data/cache/`** | Global resource, not region-specific. |

---

## Region Folder Lifecycle

```
1. User picks "Noord-Holland" in region picker
   └─ Creates: data/regions/noord-holland/region.json

2. User clicks ⬇ SRTM
   └─ Downloads to: data/regions/noord-holland/srtm/N52E004.hgt.gz

3. User clicks ⬇ OSM
   └─ Downloads to: data/regions/noord-holland/osm/noord-holland-latest.osm.pbf

4. User clicks Parse → Build
   └─ Writes to: data/regions/noord-holland/output/noord-holland.worldtemplate

5. User can delete the entire folder to reclaim ~200 MB
```

---

## Data Model Changes

### RegionPreset changes

```csharp
public record RegionPreset
{
    // ... existing fields (Name, DisplayName, bounds, URLs, cull settings) ...

    // NEW: Computed paths based on region folder
    public string RegionDir => Path.Combine("data", "regions", Name);
    public string SrtmDir => Path.Combine(RegionDir, "srtm");
    public string OsmDir => Path.Combine(RegionDir, "osm");
    public string OutputDir => Path.Combine(RegionDir, "output");
    public string OsmFilePath => Path.Combine(OsmDir, OsmFileName ?? $"{Name}-latest.osm.pbf");
    public string OutputFilePath => Path.Combine(OutputDir, $"{Name}.worldtemplate");
    public string PresetFilePath => Path.Combine(RegionDir, "region.json");
}
```

Remove `DefaultSrtmDir` and `DefaultOutputDir` — they're now computed from `Name`.

### WorldTemplateViewModel.ApplyPreset changes

```csharp
private void ApplyPreset(RegionPreset preset)
{
    SrtmDirectory = preset.SrtmDir;           // was: preset.DefaultSrtmDir
    OsmFilePath = preset.OsmFilePath;          // was: Path.Combine("data", preset.OsmFileName)
    OutputPath = preset.OutputFilePath;        // was: Path.Combine(preset.DefaultOutputDir, ...)
    RegionName = preset.Name;
    // ... rest unchanged ...
}
```

---

## Region Discovery

On startup, scan `data/regions/` for folders containing `region.json`:

```csharp
public void LoadPresetsFromRegions()
{
    Presets.Clear();
    var regionsDir = Path.Combine("data", "regions");
    if (!Directory.Exists(regionsDir)) return;

    foreach (var dir in Directory.GetDirectories(regionsDir))
    {
        var presetPath = Path.Combine(dir, "region.json");
        if (File.Exists(presetPath))
            Presets.Add(RegionPreset.Load(presetPath));
    }
}
```

This replaces the current `LoadPresetsFromDirectory("data/presets")` approach.

---

## Migration

### Backward compatibility

1. Keep `LoadPresetsFromDirectory()` as fallback — scan `data/presets/*.regionpreset` for old-format presets
2. First time a legacy preset is selected, create the region folder and copy the preset as `region.json`
3. New regions from the region picker always create the new folder structure

### Migration steps

| Step | What | Breaking? |
|------|------|-----------|
| 1 | Add computed path properties to `RegionPreset` | No — additive |
| 2 | Update `ApplyPreset()` to use new paths | No — paths change but logic is the same |
| 3 | Add region folder creation on first download | No — creates dirs that don't exist yet |
| 4 | Update `LoadPresetsFromRegions()` to scan `data/regions/` | No — additive alongside old scan |
| 5 | Move CLI tool to accept `--region <name>` instead of separate paths | Minor — CLI args change |

---

## CLI Tool Changes

Current:
```bash
Oravey2.WorldTemplateTool --srtm data/srtm --osm data/noordholland.osm.pbf --output content/noordholland.worldtemplate
```

Proposed:
```bash
Oravey2.WorldTemplateTool --region noord-holland
```

Which resolves to:
- SRTM: `data/regions/noord-holland/srtm/`
- OSM: `data/regions/noord-holland/osm/noord-holland-latest.osm.pbf`
- Output: `data/regions/noord-holland/output/noord-holland.worldtemplate`

Keep explicit `--srtm`, `--osm`, `--output` as overrides.

---

## .gitignore

No changes needed — `data/` is already gitignored. The entire `data/regions/` tree is local-only.

---

## Disk Usage Estimate

| Region | SRTM tiles | OSM extract | Worldtemplate | Total |
|--------|-----------|-------------|---------------|-------|
| Noord-Holland | 2 × 1.7 MB = ~3 MB | ~181 MB | ~101 MB | ~285 MB |
| Caribbean (small island) | 4 × 1.7 MB = ~7 MB | ~50 MB | ~30 MB | ~87 MB |
| China (large) | 50+ × 1.7 MB = ~85 MB | ~1.5 GB | ~500 MB+ | ~2 GB |
| **Typical 3 regions** | | | | **~500 MB – 2 GB** |

---

## Implementation Steps

| # | Task | Project | Depends On |
|---|------|---------|------------|
| 1 | Add computed path properties to `RegionPreset` | Oravey2.MapGen | — |
| 2 | Update `ApplyPreset()` to use region folder paths | Oravey2.MapGen | 1 |
| 3 | Add `LoadPresetsFromRegions()` alongside existing loader | Oravey2.MapGen | 1 |
| 4 | Create region folder + `region.json` when picker selects a region | Oravey2.MapGen | 1 |
| 5 | Update `DataDownloadService` to use region-relative paths | Oravey2.MapGen | 1, 2 |
| 6 | Update CLI tool with `--region` flag | Oravey2.WorldTemplateTool | 1 |
| 7 | Unit tests for path resolution and region discovery | Oravey2.Tests | 1–3 |
