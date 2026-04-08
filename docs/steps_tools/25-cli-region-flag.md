# Step 25 — CLI --region Flag

**Work streams:** WS-CLI (WorldTemplateTool enhancement)
**Depends on:** Step 20 (RegionPreset paths), Step 21 (SRTM .hgt.gz reading)
**User-testable result:** `Oravey2.WorldTemplateTool --region noord-holland` resolves all paths from the region folder and builds a worldtemplate. Explicit `--srtm`, `--osm`, `--output` flags still work as overrides.

---

## Goals

1. Add `--region <name>` argument that resolves all paths from `data/regions/<name>/`.
2. Load `region.json` from the region folder to get preset metadata.
3. Keep `--srtm`, `--osm`, `--output` as optional overrides.
4. Update help text.

---

## Problem

The CLI currently requires three separate path arguments: `--srtm <dir> --osm <file> --output <file>`. With region folders, specifying `--region noord-holland` is enough to derive all three.

---

## Tasks

### 25.1 — Add --region Argument Parsing

File: `tools/Oravey2.WorldTemplateTool/Program.cs`

- [ ] Add `--region` argument:
  ```csharp
  string? regionName = null;

  case "--region" when i + 1 < args.Length:
      regionName = args[++i];
      break;
  ```

### 25.2 — Resolve Paths from Region

File: `tools/Oravey2.WorldTemplateTool/Program.cs`

- [ ] After argument parsing, resolve from region if specified:
  ```csharp
  RegionPreset? preset = null;
  if (regionName != null)
  {
      var presetPath = Path.Combine("data", "regions", regionName, "region.json");
      if (!File.Exists(presetPath))
      {
          Console.Error.WriteLine($"Region preset not found: {presetPath}");
          return 1;
      }
      preset = RegionPreset.Load(presetPath);

      // Use region paths as defaults, allow overrides
      srtmDir ??= preset.SrtmDir;
      osmFile ??= preset.OsmFilePath;
      outputFile ??= preset.OutputFilePath;
  }
  ```

- [ ] Remove the hard requirement for all three flags when `--region` is given:
  ```csharp
  if (srtmDir == null || osmFile == null || outputFile == null)
  {
      Console.Error.WriteLine(
          "Usage: Oravey2.WorldTemplateTool --region <name> [overrides]\n" +
          "   or: Oravey2.WorldTemplateTool --srtm <dir> --osm <file> --output <file> [options]");
      return 1;
  }
  ```

- [ ] Use preset's cull settings as default when `--cull` is not specified and preset is available:
  ```csharp
  cullSettings ??= preset?.DefaultCullSettings;
  ```

### 25.3 — Update SRTM File Scanning

File: `tools/Oravey2.WorldTemplateTool/Program.cs`

- [ ] Scan for both `.hgt` and `.hgt.gz`:
  ```csharp
  var hgtFiles = Directory.GetFiles(srtmDir)
      .Where(f => f.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".hgt.gz", StringComparison.OrdinalIgnoreCase))
      .ToArray();
  ```

### 25.4 — Update Help Text

File: `tools/Oravey2.WorldTemplateTool/Program.cs`

- [ ] Update `--help` output:
  ```
  Usage: Oravey2.WorldTemplateTool --region <name> [overrides]
     or: Oravey2.WorldTemplateTool --srtm <dir> --osm <file> --output <file> [options]

  Options:
    --region <name>    Region name (resolves paths from data/regions/<name>/).
    --srtm <dir>       Directory containing .hgt/.hgt.gz SRTM files. Overrides --region.
    --osm <file>       Path to an OSM PBF extract file. Overrides --region.
    --output <file>    Output .worldtemplate file path. Overrides --region.
    --name <region>    Region name for the template (default: from --region or NoordHolland).
    --cull <file>      Apply culling from a .cullsettings JSON file.
                       When --region is used and --cull is omitted, preset cull settings apply.
    --help, -h         Show this help message.
  ```

### 25.5 — Update --name Default

File: `tools/Oravey2.WorldTemplateTool/Program.cs`

- [ ] When `--region` is used and `--name` is not specified, use preset name:
  ```csharp
  string templateName = nameArg ?? preset?.Name ?? "NoordHolland";
  ```

---

## Verify

```bash
# Region-based (all paths resolved automatically):
dotnet run --project tools/Oravey2.WorldTemplateTool -- --region noord-holland

# Region with override:
dotnet run --project tools/Oravey2.WorldTemplateTool -- --region noord-holland --output custom/output.worldtemplate

# Legacy explicit paths still work:
dotnet run --project tools/Oravey2.WorldTemplateTool -- --srtm data/regions/noord-holland/srtm --osm data/regions/noord-holland/osm/noord-holland-latest.osm.pbf --output out.worldtemplate
```

All three invocations produce a valid `.worldtemplate` file.
