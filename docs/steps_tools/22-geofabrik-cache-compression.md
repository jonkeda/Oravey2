# Step 22 — Geofabrik Cache Compression

**Work streams:** WS-Compression (Data compression)
**Depends on:** Step 20 (Shared cache directory at `data/cache/`)
**User-testable result:** Geofabrik index is cached as `geofabrik-index-v1.json.gz` in `data/cache/`. ~90% disk reduction (~25 MB → ~2.5 MB).

---

## Goals

1. Change `GeofabrikService` to store the index cache as `.json.gz` instead of `.json`.
2. Move cache location from constructor-injected `_cacheDir` to `data/cache/`.
3. Read and write via `GZipStream` — transparent to callers.

---

## Problem

The Geofabrik index JSON is ~25 MB. It's downloaded once and cached for 7 days. Gzipping reduces it to ~2.5 MB with negligible decompression cost (parsed once per session).

---

## Tasks

### 22.1 — Update GeofabrikService Cache

File: `src/Oravey2.MapGen/WorldTemplate/GeofabrikService.cs`

- [ ] Change `CacheFileName` constant:
  ```csharp
  private const string CacheFileName = "geofabrik-index-v1.json.gz";
  ```

- [ ] Update write path to compress:
  ```csharp
  Directory.CreateDirectory(_cacheDir);
  var json = await _http.GetStringAsync(IndexUrl);

  await using var output = File.Create(cachePath);
  await using var gz = new GZipStream(output, CompressionLevel.Optimal);
  await using var writer = new StreamWriter(gz);
  await writer.WriteAsync(json);
  ```

- [ ] Update read path to decompress:
  ```csharp
  await using var input = File.OpenRead(cachePath);
  await using var gz = new GZipStream(input, CompressionMode.Decompress);
  using var reader = new StreamReader(gz);
  var json = await reader.ReadToEndAsync();
  ```

- [ ] Add `using System.IO.Compression;` import

### 22.2 — Update Cache Directory Injection

File: `src/Oravey2.MapGen/WorldTemplate/GeofabrikService.cs`

- [ ] Default `_cacheDir` to `data/cache` if not specified, or update callers to pass `data/cache`:
  ```csharp
  public GeofabrikService(HttpClient http, string? cacheDir = null)
  {
      _http = http;
      _cacheDir = cacheDir ?? Path.Combine("data", "cache");
  }
  ```

### 22.3 — Update Callers

- [ ] Update any DI registration or direct construction of `GeofabrikService` to use `data/cache` (or rely on new default)

### 22.4 — Unit Tests

File: `tests/Oravey2.Tests/WorldTemplate/GeofabrikServiceTests.cs`

- [ ] `GetIndex_WritesGzCache` — mock HTTP, call `GetIndexAsync`, verify `geofabrik-index-v1.json.gz` exists in cache dir
- [ ] `GetIndex_ReadsGzCache` — pre-write a `.json.gz` cache file, call `GetIndexAsync` without mock, verify it reads correctly
- [ ] `GetIndex_StaleCache_Refreshes` — set cache file timestamp to 8 days ago, verify re-download

---

## Verify

```bash
dotnet test tests/Oravey2.Tests --filter "FullyQualifiedName~GeofabrikService"
```

Old `.json` cache file (if any) is ignored — triggers a fresh download that produces `.json.gz`. No user-visible behavior change.
