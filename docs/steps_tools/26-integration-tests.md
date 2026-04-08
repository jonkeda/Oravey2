# Step 26 — Integration Tests for Region Packages & Compression

**Work streams:** WS-Testing (Cross-cutting integration tests)
**Depends on:** Steps 20–25 (all region & compression changes)
**User-testable result:** Integration tests verify the full lifecycle: create region → download (mocked) → compress → parse → build. All path resolution, gzip, and region discovery work end-to-end.

---

## Goals

1. Integration tests that verify the full region folder lifecycle.
2. Test the compressed-round-trip: gzip SRTM → parse `.hgt.gz` → build worldtemplate.
3. Test region discovery: create region folders → `LoadPresetsFromRegions()` finds them.
4. Test CLI `--region` path resolution.

---

## Problem

Steps 20–25 introduce changes across 6+ files. Unit tests cover individual methods. Integration tests verify the interactions: preset paths feed into download service, download produces gzipped files, parser reads them, builder outputs to region folder.

---

## Tasks

### 26.1 — Region Lifecycle Integration Test

File: `tests/Oravey2.Tests/Integration/RegionLifecycleTests.cs`

- [ ] `CreateRegion_DownloadSrtm_ParseAndBuild`:
  1. Create a `RegionPreset` with `Name = "test-region"`
  2. Call `EnsureDirectories()` — verify all subdirs created
  3. Write a known SRTM byte array to `preset.SrtmDir / "N52E004.hgt.gz"` (gzipped)
  4. Parse with `SrtmParser.ParseHgtFile(gzPath)` — verify grid dimensions
  5. Verify all paths resolve to `data/regions/test-region/...`

- [ ] `RegionPreset_SaveAndLoad_RoundTrips`:
  1. Create preset, `Save(preset.PresetFilePath)`
  2. `Load(preset.PresetFilePath)` — verify all properties match
  3. Verify computed paths are not serialized to JSON

### 26.2 — Compression Round-Trip Integration Test

File: `tests/Oravey2.Tests/Integration/CompressionIntegrationTests.cs`

- [ ] `SrtmGzip_RoundTrip_ProducesIdenticalGrid`:
  1. Generate a synthetic 1201×1201 SRTM byte array (3-arcsecond)
  2. Parse raw bytes → `grid1`
  3. Gzip the byte array to temp `.hgt.gz`  
  4. Parse via `SrtmParser.ParseHgtFile(gzPath)` → `grid2`
  5. Assert `grid1` and `grid2` are identical

- [ ] `GeofabrikGzip_RoundTrip_PreservesJson`:
  1. Write a sample JSON string to `.json.gz` via `GZipStream`
  2. Read back via `GZipStream` → verify identical content

### 26.3 — Region Discovery Integration Test

File: `tests/Oravey2.Tests/Integration/RegionDiscoveryTests.cs`

- [ ] `LoadPresetsFromRegions_MultipleRegions`:
  1. Create temp `data/regions/` with 3 region folders, each containing `region.json`
  2. Call `LoadPresetsFromRegions()` on a ViewModel
  3. Verify 3 presets loaded with correct names

- [ ] `LoadPresetsFromRegions_EmptyRegionsDir_ReturnsEmpty`:
  1. Create empty `data/regions/` directory
  2. Call `LoadPresetsFromRegions()` — verify `Presets.Count == 0`

- [ ] `LoadPresetsFromRegions_MissingDir_ReturnsEmpty`:
  1. Don't create `data/regions/`
  2. Call `LoadPresetsFromRegions()` — no exception, empty collection

### 26.4 — CLI Path Resolution Test

File: `tests/Oravey2.Tests/Integration/CliRegionTests.cs`

- [ ] `RegionFlag_ResolvesCorrectPaths`:
  1. Create a temp `data/regions/test-region/region.json`
  2. Verify `RegionPreset.Load(presetPath)` produces expected `SrtmDir`, `OsmFilePath`, `OutputFilePath`

- [ ] `RegionFlag_WithOverrides_OverridesTakePrecedence`:
  1. Load a preset
  2. Simulate `--srtm custom/path` override
  3. Verify the override path wins over the computed path

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~Integration"
```

All integration tests pass. The full flow from region creation through compressed storage to world template building works end-to-end.
