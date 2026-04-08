# Step 03 — Data Download Service

**Work streams:** WS-Download (HTTP download with progress)
**Depends on:** Step 01 (RegionPreset for URLs and bounding box)
**User-testable result:** Integration tests download a real SRTM tile and verify the `.hgt` file. OSM download writes a valid PBF file.

---

## Goals

1. Download SRTM 1-arcsecond elevation tiles from NASA Earthdata.
2. Download OSM PBF extracts from Geofabrik.
3. Report progress with bytes downloaded, total bytes, and files completed.
4. Support cancellation.
5. Store NASA Earthdata credentials securely.

---

## Problem

Currently the user must manually download SRTM tiles from USGS EarthExplorer and OSM PBF files from Geofabrik and place them in the `data/` folder. This is error-prone (wrong tiles, wrong region, incomplete downloads). The app should handle downloads directly.

---

## Tasks

### 3.1 — Download Data Types

File: `src/Oravey2.MapGen/Download/DownloadProgress.cs`

- [ ] `DownloadProgress` record:
  ```csharp
  public record DownloadProgress(
      string FileName,
      long BytesDownloaded,
      long TotalBytes,
      int FilesCompleted,
      int TotalFiles);
  ```

File: `src/Oravey2.MapGen/Download/SrtmDownloadRequest.cs`

- [ ] `SrtmDownloadRequest` record:
  ```csharp
  public record SrtmDownloadRequest(
      double NorthLat, double SouthLat,
      double EastLon, double WestLon,
      string TargetDirectory,
      string? EarthdataUsername = null,
      string? EarthdataPassword = null);
  ```

File: `src/Oravey2.MapGen/Download/OsmDownloadRequest.cs`

- [ ] `OsmDownloadRequest` record:
  ```csharp
  public record OsmDownloadRequest(
      string DownloadUrl,
      string TargetFilePath);
  ```

### 3.2 — IDataDownloadService Interface

File: `src/Oravey2.MapGen/Download/IDataDownloadService.cs`

- [ ] Define interface:
  ```csharp
  public interface IDataDownloadService
  {
      Task DownloadSrtmTilesAsync(
          SrtmDownloadRequest request,
          IProgress<DownloadProgress> progress,
          CancellationToken ct = default);

      Task DownloadOsmExtractAsync(
          OsmDownloadRequest request,
          IProgress<DownloadProgress> progress,
          CancellationToken ct = default);

      List<string> GetRequiredSrtmTileNames(
          double northLat, double southLat,
          double eastLon, double westLon);

      List<string> GetExistingSrtmTiles(string directory);
  }
  ```

### 3.3 — SRTM Tile Name Calculation

File: `src/Oravey2.MapGen/Download/DataDownloadService.cs`

- [ ] Implement `GetRequiredSrtmTileNames`:
  - For each 1°×1° cell in the bounding box, produce `N{lat:D2}E{lon:D3}` (or `S`/`W` for negative)
  - Example: bbox 52.2°N–53.0°N, 4.0°E–5.5°E → `N52E004`, `N52E005`, `N53E004`, `N53E005`
- [ ] Implement `GetExistingSrtmTiles`:
  - Scan directory for `*.hgt` files, return tile names

### 3.4 — SRTM Download Implementation

- [ ] NASA Earthdata authentication:
  1. POST to `https://urs.earthdata.nasa.gov/api/users/token` with Basic auth header
  2. Receive Bearer token
  3. Use token for subsequent downloads
- [ ] Download URL pattern: `https://e4ftl01.cr.usgs.gov/MEASURES/SRTMGL1.003/2000.02.11/{tileName}.SRTMGL1.hgt.zip`
- [ ] Download each missing tile:
  1. Download `.hgt.zip` to temp file
  2. Extract `.hgt` from zip to target directory
  3. Delete temp zip
  4. Report progress after each tile
- [ ] Handle HTTP 401 (bad credentials), 404 (tile doesn't exist — ocean tiles)
- [ ] Support `CancellationToken` between tiles

### 3.5 — OSM PBF Download Implementation

- [ ] Simple HTTP GET with progress:
  1. Send GET to `request.DownloadUrl`
  2. Read `Content-Length` header for total size
  3. Stream to temp file with `IProgress<DownloadProgress>` updates every 1 MB
  4. Atomic replace: rename temp file to `request.TargetFilePath` on completion
- [ ] Handle large files (100–200 MB) — use streaming with 64 KB buffer
- [ ] No authentication required for Geofabrik
- [ ] Support `CancellationToken` — delete temp file on cancellation

### 3.6 — HttpClient Configuration

- [ ] Use `IHttpClientFactory` pattern — register in DI
- [ ] Configure redirect following (NASA Earthdata uses 302 redirects)
- [ ] Set User-Agent header (required by both services)
- [ ] Timeout: 5 minutes per tile, 30 minutes for large PBF files

### 3.7 — Unit Tests

File: `tests/Oravey2.Tests/Download/DataDownloadServiceTests.cs`

**Tile name calculation (no network):**
- [ ] `GetRequiredTiles_NoordHolland_Returns4Tiles` — bbox produces N52E004, N52E005, N53E004, N53E005
- [ ] `GetRequiredTiles_NegativeCoords_FormatsCorrectly` — S/W prefixes for southern/western hemispheres
- [ ] `GetRequiredTiles_SingleCell_Returns1Tile` — 1°×1° bbox
- [ ] `GetExistingTiles_FindsHgtFiles` — creates temp files, verifies detection

**Download (mocked HTTP or integration):**
- [ ] `DownloadOsm_ReportsProgress` — mock HttpClient, verify progress callbacks
- [ ] `DownloadOsm_Cancelled_DeletesTempFile` — cancel mid-download, verify cleanup
- [ ] `DownloadSrtm_MissingTile_SkipsGracefully` — 404 tile treated as ocean, not error

### 3.8 — Integration Test (Optional, Manual)

File: `tests/Oravey2.Tests/Download/DataDownloadIntegrationTests.cs`

- [ ] Mark with `[Category("Integration")]` — not run in CI
- [ ] `DownloadSingleSrtmTile` — downloads N52E004, verifies file size ~25 MB, valid .hgt
- [ ] `DownloadSmallOsmExtract` — downloads a small Geofabrik extract, verifies PBF magic bytes

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~DataDownload"
```

**User test:** Run integration test manually. Verify `.hgt` file appears in `data/srtm/` and can be parsed by `SrtmParser`. Verify `.osm.pbf` appears and can be parsed by `OsmParser`.
