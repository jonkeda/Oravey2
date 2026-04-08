# Step 23 — DataDownloadService Region Paths

**Work streams:** WS-Download (HTTP download), WS-FileLayout (Directory restructure)
**Depends on:** Step 20 (RegionPreset computed paths), Step 21 (SRTM gzip)
**User-testable result:** SRTM and OSM downloads land in the correct region subdirectories. Downloads produce `.hgt.gz` files. Region folder is created automatically on first download.

---

## Goals

1. `DownloadSrtmTilesAsync` writes to `data/regions/<name>/srtm/` and produces `.hgt.gz`.
2. `DownloadOsmExtractAsync` writes to `data/regions/<name>/osm/`.
3. Region directories are created on first download.
4. `SrtmDownloadRequest` and `OsmDownloadRequest` use `RegionPreset` paths.

---

## Problem

`SrtmDownloadRequest.TargetDirectory` and `OsmDownloadRequest.TargetFilePath` are currently arbitrary paths. Callers must manually assemble them from the preset. With computed paths on `RegionPreset`, download requests should derive their paths from the preset directly.

---

## Tasks

### 23.1 — Update SrtmDownloadRequest

File: `src/Oravey2.MapGen/Download/SrtmDownloadRequest.cs`

- [ ] Change `TargetDirectory` to use region SRTM dir:
  ```csharp
  public record SrtmDownloadRequest(
      double NorthLat, double SouthLat,
      double EastLon, double WestLon,
      string TargetDirectory,
      string? EarthdataUsername = null,
      string? EarthdataPassword = null);
  ```
  (Record shape stays the same — callers now pass `preset.SrtmDir` instead of `preset.DefaultSrtmDir`)

### 23.2 — Update DownloadSrtmTilesAsync Post-Processing

File: `src/Oravey2.MapGen/Download/DataDownloadService.cs`

- [ ] After `ExtractHgtFromZip`, gzip and delete raw (from Step 21.2):
  ```csharp
  ExtractHgtFromZip(tempZip, request.TargetDirectory, tile);

  // Compress to .hgt.gz
  var hgtPath = Path.Combine(request.TargetDirectory, $"{tile}.hgt");
  var gzPath = Path.Combine(request.TargetDirectory, $"{tile}.hgt.gz");

  await using (var input = File.OpenRead(hgtPath))
  await using (var output = File.Create(gzPath))
  await using (var gz = new GZipStream(output, CompressionLevel.Optimal))
      await input.CopyToAsync(gz, ct);

  File.Delete(hgtPath);
  ```

### 23.3 — Update OsmDownloadRequest

File: `src/Oravey2.MapGen/Download/OsmDownloadRequest.cs`

- [ ] No structural change needed — callers now pass `preset.OsmFilePath` instead of manually constructing the path

### 23.4 — Update ViewModel Download Commands

File: `src/Oravey2.MapGen/ViewModels/WorldTemplateViewModel.cs`

- [ ] In `DownloadSrtmAsync`, use `SelectedPreset.SrtmDir`:
  ```csharp
  var request = new SrtmDownloadRequest(
      SelectedPreset.NorthLat, SelectedPreset.SouthLat,
      SelectedPreset.EastLon, SelectedPreset.WestLon,
      SelectedPreset.SrtmDir, username, password);
  ```
  (Previously used `SrtmDirectory` property which was set from `DefaultSrtmDir`)

- [ ] In `DownloadOsmAsync`, use `SelectedPreset.OsmFilePath`:
  ```csharp
  var request = new OsmDownloadRequest(SelectedPreset.OsmDownloadUrl, SelectedPreset.OsmFilePath);
  ```

- [ ] Call `SelectedPreset.EnsureDirectories()` before each download

### 23.5 — Unit Tests

File: `tests/Oravey2.Tests/Download/DataDownloadServiceTests.cs`

- [ ] `DownloadSrtm_ProducesGzFile` — mock download, verify `.hgt.gz` exists and `.hgt` does not
- [ ] `DownloadSrtm_GzFileIsValidGzip` — verify output is valid gzip by decompressing

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~DataDownload"
```

After downloading SRTM for Noord-Holland, files appear at `data/regions/noord-holland/srtm/N52E004.hgt.gz`. No raw `.hgt` files remain.
