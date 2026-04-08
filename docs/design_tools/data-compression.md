# Design: Data Compression Strategy

## Status: Draft

---

## Overview

The project stores and downloads several large data files across `data/`, `content/`, and a runtime cache. This document evaluates which files benefit from compression, what formats to use, and what code changes are needed.

---

## Current State

### File inventory (measured)

| Location | File | Uncompressed | Notes |
|----------|------|-------------:|-------|
| `data/srtm/` | `N52E004.hgt` | 25.3 MB | Binary elevation grid (3601×3601 × 2 bytes) |
| `data/srtm/` | `N52E004.hgt.gz` | 1.7 MB | Already present — **93% reduction** |
| `data/` | `noordholland.osm.pbf` | 181.4 MB | Protocol Buffers binary — already compressed internally |
| `content/` | `noordholland.worldtemplate` | 101.0 MB | Custom binary format (magic `OWTP`) |
| `cache/` | `geofabrik-index-v1.json` | ~25 MB | GeoJSON downloaded at runtime, cached 7 days |
| `data/presets/` | `*.regionpreset` | ~1 KB | Tiny JSON — not worth compressing |

### Content package JSON files (committed to Git)

| Location | Files | Total Size | Largest |
|----------|-------|-----------|---------|
| `Oravey2.Apocalyptic/maps/portland/chunks/` | 4 chunk files | ~69 KB | `0_0.json` (17 KB) |
| `Oravey2.Apocalyptic/data/` | enemies, items, NPCs, dialogues, quests | ~12 KB | `elder_dialogue.json` (5.4 KB) |
| `Oravey2.Apocalyptic/` | catalog, manifest, blueprints, scenarios | ~10 KB | `catalog.json` (6.3 KB) |
| `Oravey2.Fantasy/data/` | enemies, items, NPCs, dialogues, quests | ~15 KB | `elder_miriel_dialogue.json` (5.6 KB) |
| `Oravey2.Fantasy/` | catalog, manifest, scenarios | ~4 KB | `catalog.json` (3.2 KB) |

### How downloads work today

| Source | Format Downloaded | Post-processing |
|--------|-------------------|-----------------|
| USGS SRTM | `.hgt.zip` | Extracted to `.hgt` via `ZipFile`, temp deleted |
| Geofabrik OSM | `.osm.pbf` direct | Stored as-is |
| Geofabrik index | `.json` direct | Stored as-is in cache dir |

---

## Analysis

### SRTM `.hgt` → `.hgt.gz` — **High value**

SRTM tiles compress extremely well (25.3 MB → 1.7 MB, 93% reduction) because elevation data has spatial locality and many repeated/similar values.

**Current problem**: USGS serves `.hgt.zip`, we extract to raw `.hgt`, and never compress again. The `.hgt.gz` file already exists in `data/srtm/` but the `SrtmParser` only reads raw `.hgt`.

**Recommendation**: After extracting from USGS zip, immediately gzip the `.hgt` to `.hgt.gz` and delete the raw `.hgt`. Modify `SrtmParser` to read `.hgt.gz` directly via `GZipStream`. This saves ~23 MB per tile on disk.

### OSM `.osm.pbf` — **No change needed**

PBF (Protocol Buffers Binary Format) is already a compressed format. Gzipping a `.osm.pbf` file typically achieves only 5-15% additional reduction because the data is already encoded efficiently. The 181 MB file would shrink to maybe ~160 MB — not worth the decompression overhead when OsmSharp reads PBF natively.

### `.worldtemplate` — **Medium value, future consideration**

At 101 MB, this is the largest single file. The custom binary format (`OWTP`) stores elevation grids, town/road/water data. Since it contains raw float arrays (elevation) that compress well, gzipping could reduce size significantly (estimated 60-70% reduction).

However, this is a generated output file, not stored in Git, and is consumed by the game runtime. Compressing it requires changes to both `WorldTemplateBuilder` (write) and the game's loader (read). **Defer to a separate design**.

### Geofabrik index JSON — **Medium value**

The ~25 MB JSON cache could be stored as `.json.gz` to save ~22 MB on disk. Since it's parsed infrequently (once per session), the decompression cost is negligible.

### Content package JSON — **Not worth it**

All JSON files in `Oravey2.Apocalyptic` and `Oravey2.Fantasy` total ~110 KB combined. Compression overhead (code complexity, build pipeline changes) far outweighs the ~80 KB savings. These files are also committed to Git, which already compresses objects internally.

**When this changes**: If map generation produces large chunk files (hundreds of chunks, MB-scale each), revisit. The current 4 chunks × 17 KB is trivial.

---

## Recommendation Summary

| File Type | Action | Compression | Estimated Savings | Priority |
|-----------|--------|-------------|-------------------|----------|
| SRTM `.hgt` | Store as `.hgt.gz`, read via `GZipStream` | gzip | ~23 MB per tile (93%) | **High** |
| Geofabrik index cache | Store as `.json.gz` | gzip | ~22 MB (90%) | **Medium** |
| `.worldtemplate` | Defer — needs game runtime changes | gzip | ~60-70 MB est. | Low (separate design) |
| `.osm.pbf` | No change — already compressed | — | — | None |
| Content JSON | No change — files are tiny | — | — | None |
| `.regionpreset` | No change — ~1 KB each | — | — | None |

---

## Implementation Plan

### Step 1: SRTM — store and read `.hgt.gz`

**`DataDownloadService.cs`** — after extracting `.hgt` from USGS zip, gzip it:

```csharp
// After ExtractHgtFromZip:
var hgtPath = Path.Combine(targetDir, $"{tile}.hgt");
var gzPath = Path.Combine(targetDir, $"{tile}.hgt.gz");

await using (var input = File.OpenRead(hgtPath))
await using (var output = File.Create(gzPath))
await using (var gz = new GZipStream(output, CompressionLevel.Optimal))
    await input.CopyToAsync(gz, ct);

File.Delete(hgtPath);
```

**`SrtmParser.cs`** — read `.hgt.gz` transparently:

```csharp
public float[,] ParseHgtFile(string path)
{
    Stream stream = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
        ? new GZipStream(File.OpenRead(path), CompressionMode.Decompress)
        : File.OpenRead(path);

    using (stream)
    {
        // ... existing parsing logic using stream instead of File.ReadAllBytes
    }
}
```

**`DataDownloadService.GetExistingSrtmTiles()`** — detect both formats:

```csharp
public List<string> GetExistingSrtmTiles(string directory)
{
    if (!Directory.Exists(directory)) return [];

    return Directory.GetFiles(directory)
        .Where(f => f.EndsWith(".hgt") || f.EndsWith(".hgt.gz"))
        .Select(f => Path.GetFileNameWithoutExtension(
            f.EndsWith(".hgt.gz") ? Path.GetFileNameWithoutExtension(f) + ".hgt" : f))
        .Select(Path.GetFileNameWithoutExtension!)
        .Distinct()
        .ToList();
}
```

### Step 2: Geofabrik index — cache as `.json.gz`

**`GeofabrikService.cs`** — write compressed, read compressed:

```csharp
private const string CacheFileName = "geofabrik-index-v1.json.gz";

// Write:
await using var output = File.Create(cachePath);
await using var gz = new GZipStream(output, CompressionLevel.Optimal);
await using var writer = new StreamWriter(gz);
await writer.WriteAsync(json);

// Read:
await using var input = File.OpenRead(cachePath);
await using var gz = new GZipStream(input, CompressionMode.Decompress);
using var reader = new StreamReader(gz);
var json = await reader.ReadToEndAsync();
```

---

## Migration

- **SRTM**: Detect both `.hgt` and `.hgt.gz` at read time. Existing raw `.hgt` files continue to work. New downloads produce `.hgt.gz` only.
- **Geofabrik cache**: Change cache filename to `.json.gz`. Old `.json` cache will be ignored (triggers fresh download, which is fine since it's a cache).
- No breaking changes to persisted data or Git-tracked files.

---

## Dependencies

- `System.IO.Compression` — already referenced (used for SRTM zip extraction)
- No new NuGet packages required
