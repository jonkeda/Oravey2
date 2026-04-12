# Step 06 — Game Reads User Library

## Goal

Change `GameBootstrapper` to read `world.db` from the user's persistent
location (`%LOCALAPPDATA%/Oravey2/world.db`) instead of `AppContext.BaseDirectory`.
This ensures imported regions survive game rebuilds.

## Deliverables

### 6.1 Update world.db path in `GameBootstrapper`

File: `src/Oravey2.Core/Bootstrap/GameBootstrapper.cs`

**Before** (around line 95):
```csharp
// --- World DB stores + RegionLoader (sole loading path) ---
WorldMapStore? worldStore = null;
var worldDbPath = Path.Combine(AppContext.BaseDirectory, "world.db");
var debugDbPath = Path.Combine(AppContext.BaseDirectory, "debug.db");
if (File.Exists(worldDbPath))
    worldStore = new WorldMapStore(worldDbPath);
```

**After:**
```csharp
// --- World DB stores + RegionLoader (sole loading path) ---
WorldMapStore? worldStore = null;
var worldDbPath = WorldDbPaths.GetUserWorldDbPath();
var debugDbPath = Path.Combine(AppContext.BaseDirectory, "debug.db");
if (File.Exists(worldDbPath))
    worldStore = new WorldMapStore(worldDbPath);
```

Add using at top of file:
```csharp
using Oravey2.Core.Data;
```

### 6.2 `debug.db` stays in `AppContext.BaseDirectory`

No change. `debug.db` contains the 5 built-in scenarios and is auto-seeded
on first launch. It's ephemeral by design.

### 6.3 Log the resolved path

Add logging after the path is resolved so users can find their world.db:

```csharp
var worldDbPath = WorldDbPaths.GetUserWorldDbPath();
logger.LogInformation("World DB path: {Path}", worldDbPath);
```

### 6.4 Wire `ContentPackImportService` into bootstrapper

Create and store the import service so the scenario selector can use it:

```csharp
// After contentPackService.DiscoverPacks():
var contentPackImportService = new ContentPackImportService(contentPackService);
```

Pass it to the scenario selector (see step 07 for the UI side):

```csharp
scenarioSelector.ImportService = contentPackImportService;
```

### 6.5 Verify

1. Delete `%LOCALAPPDATA%/Oravey2/world.db` if it exists
2. Launch game → scenario selector shows only 5 built-in scenarios
3. Import Noord-Holland via the Import Region button (step 07)
4. Restart game → Noord-Holland appears in scenario list
5. Rebuild game (`dotnet build`) → Noord-Holland still appears

## Dependencies

- Step 01 (`WorldDbPaths`)
- Step 05 (`ContentPackImportService`) — for wiring into bootstrapper

## Estimated scope

- Modified files: 1 (`GameBootstrapper.cs` — 3 lines changed)
