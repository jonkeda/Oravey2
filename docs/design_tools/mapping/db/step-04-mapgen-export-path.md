# Step 04 — MapGen Export into Content Pack

## Goal

Change the MapGen "Export to Game DB" to write `world.db` inside the content
pack directory instead of beside it. This makes content packs self-contained
and shippable.

## Deliverables

### 4.1 Fix export path in `AssemblyStepViewModel`

File: `src/Oravey2.MapGen/ViewModels/AssemblyStepViewModel.cs`

**Before** (in `RunExportToDb()`):
```csharp
var dbPath = Path.Combine(
    Path.GetDirectoryName(_state.ContentPackPath)!,
    "world.db");
```

**After:**
```csharp
var dbPath = Path.Combine(_state.ContentPackPath, "world.db");
```

This writes `world.db` into e.g. `content/Oravey2.Apocalyptic.NL.NH/world.db`.

### 4.2 Verify MSBuild glob already covers it

The existing `ContentPacks` glob in `src/Oravey2.Windows/Oravey2.Windows.csproj`
already copies everything under `content/`:

```xml
<Content Include="$(ContentPacksRoot)\**\*"
         Exclude="$(ContentPacksRoot)\**\bin\**;$(ContentPacksRoot)\**\obj\**;$(ContentPacksRoot)\**\*.csproj"
         Link="ContentPacks\%(RecursiveDir)%(Filename)%(Extension)"
         CopyToOutputDirectory="PreserveNewest" />
```

No change needed — `world.db` inside the content pack directory is
automatically picked up and copied to `bin/.../ContentPacks/{packId}/world.db`.

### 4.3 Add `world.db` to `.gitignore`

Content pack `world.db` files are generated artifacts. Add to the repo's
`.gitignore`:

```
# Content pack generated databases
content/**/world.db
```

### 4.4 Clean up old `content/world.db`

The previous export wrote `content/world.db` (next to the pack dir, not inside
it). Delete this stale file if it exists. It will no longer be produced.

### 4.5 Verify

After this change:
- Run MapGen → complete pipeline → click "Export to Game DB"
- Confirm file: `content/Oravey2.Apocalyptic.NL.NH/world.db`
- Build `Oravey2.Windows` → confirm file:
  `bin/Debug/net10.0/ContentPacks/Oravey2.Apocalyptic.NL.NH/world.db`

## Dependencies

None — this is an independent path change.

## Estimated scope

- Modified files: 1 (`AssemblyStepViewModel.cs` — 1 line changed)
- Modified files: 1 (`.gitignore` — 1 line added)
- Deleted files: 1 (`content/world.db` if present)
