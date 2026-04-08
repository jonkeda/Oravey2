# Step 21 — SRTM Compression

**Work streams:** WS-Compression (Data compression)
**Depends on:** Step 20 (RegionPreset paths — SRTM files now live in region folder)
**User-testable result:** `SrtmParser` reads `.hgt.gz` files directly. New SRTM downloads are stored as `.hgt.gz` only (raw `.hgt` deleted after gzip). Disk usage drops from ~25 MB to ~1.7 MB per tile.

---

## Goals

1. Modify `SrtmParser` to transparently read `.hgt.gz` via `GZipStream`.
2. Modify `DataDownloadService` to gzip the extracted `.hgt` after download, producing `.hgt.gz` and deleting the raw file.
3. Update `GetExistingSrtmTiles()` to detect `.hgt.gz` files.
4. ~93% disk savings per SRTM tile.

---

## Problem

USGS serves `.hgt.zip` files. We extract the raw `.hgt` (~25 MB per tile) and keep it on disk. SRTM data compresses extremely well due to spatial locality — the existing `N52E004.hgt.gz` in `data/srtm/` is only 1.7 MB. But `SrtmParser` only reads raw `.hgt` via `File.ReadAllBytes`.

---

## Tasks

### 21.1 — Update SrtmParser to Read .hgt.gz

File: `src/Oravey2.MapGen/WorldTemplate/SrtmParser.cs`

- [ ] Replace `File.ReadAllBytes` with stream-based reading that detects `.gz`:
  ```csharp
  public float[,] ParseHgtFile(string filePath)
  {
      byte[] bytes;
      if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
      {
          using var fileStream = File.OpenRead(filePath);
          using var gzStream = new GZipStream(fileStream, CompressionMode.Decompress);
          using var ms = new MemoryStream();
          gzStream.CopyTo(ms);
          bytes = ms.ToArray();
      }
      else
      {
          bytes = File.ReadAllBytes(filePath);
      }

      return ParseHgtFile(bytes);
  }
  ```
- [ ] Add `using System.IO.Compression;` import

### 21.2 — Gzip After SRTM Download

File: `src/Oravey2.MapGen/Download/DataDownloadService.cs`

- [ ] After `ExtractHgtFromZip`, gzip the `.hgt` and delete the raw file:
  ```csharp
  // After ExtractHgtFromZip:
  var hgtPath = Path.Combine(request.TargetDirectory, $"{tile}.hgt");
  var gzPath = Path.Combine(request.TargetDirectory, $"{tile}.hgt.gz");

  await using (var input = File.OpenRead(hgtPath))
  await using (var output = File.Create(gzPath))
  await using (var gz = new GZipStream(output, CompressionLevel.Optimal))
      await input.CopyToAsync(gz, ct);

  File.Delete(hgtPath);
  ```

### 21.3 — Update GetExistingSrtmTiles

File: `src/Oravey2.MapGen/Download/DataDownloadService.cs`

- [ ] Detect `.hgt.gz` files:
  ```csharp
  public List<string> GetExistingSrtmTiles(string directory)
  {
      if (!Directory.Exists(directory))
          return [];

      return Directory.GetFiles(directory)
          .Where(f => f.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".hgt.gz", StringComparison.OrdinalIgnoreCase))
          .Select(f => f.EndsWith(".hgt.gz", StringComparison.OrdinalIgnoreCase)
              ? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f))
              : Path.GetFileNameWithoutExtension(f))
          .Distinct()
          .ToList();
  }
  ```

### 21.4 — Update CLI and VM to Scan for .hgt.gz

File: `tools/Oravey2.WorldTemplateTool/Program.cs`

- [ ] Change `Directory.GetFiles(srtmDir, "*.hgt")` to find both `.hgt` and `.hgt.gz`:
  ```csharp
  var hgtFiles = Directory.GetFiles(srtmDir)
      .Where(f => f.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".hgt.gz", StringComparison.OrdinalIgnoreCase))
      .ToArray();
  ```

File: `src/Oravey2.MapGen/ViewModels/WorldTemplateViewModel.cs`

- [ ] Same change in `ParseAsync()`:
  ```csharp
  var hgtFiles = Directory.GetFiles(SrtmDirectory)
      .Where(f => f.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".hgt.gz", StringComparison.OrdinalIgnoreCase))
      .ToArray();
  ```

### 21.5 — Unit Tests

File: `tests/Oravey2.Tests/WorldTemplate/SrtmParserTests.cs`

- [ ] `ParseHgtGz_ProducesSameGrid` — gzip a known `.hgt` byte array, write to temp `.hgt.gz`, parse, verify identical grid
- [ ] `ParseHgt_RawStillWorks` — raw `.hgt` file still parses correctly

File: `tests/Oravey2.Tests/Download/DataDownloadServiceTests.cs`

- [ ] `GetExistingTiles_FindsHgtGzFiles` — create temp `.hgt.gz` files, verify detection
- [ ] `GetExistingTiles_MixedFormats_Deduplicates` — both `N52E004.hgt` and `N52E004.hgt.gz` → single `N52E004`

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~SrtmParser|GetExisting"
```

Parse the existing `data/srtm/N52E004.hgt.gz` file directly — produces valid elevation grid with no exceptions.
