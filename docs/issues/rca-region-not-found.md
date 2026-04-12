# RCA — `Region 'town' not found in any world database`

**Date:** 2026-04-11
**Severity:** Blocker — game cannot start any scenario

## Symptom

```
System.InvalidOperationException:
  Region 'town' not found in any world database.
  at RegionLoader.FindRegion(String regionName) in RegionLoader.cs:line 84
  at RegionLoader.LoadRegion(...)
  at GameBootstrapper.<>c__DisplayClass0_0.<Start>g__LoadAndWireScenario|2(...)
```

Clicking **New Game → Haven Town** (or any built-in scenario) crashes
immediately.

## Root Cause

Integration step **i13** removed every hardcoded `LoadXxx()` method
from `ScenarioLoader` and made `RegionLoader` the sole loading path.
`RegionLoader.FindRegion()` searches the `WorldMapStore` list
(`world.db`, `debug.db`) for a matching region name.

**The problem:** `GameBootstrapper` only opens these databases if the
files already exist on disk:

```csharp
if (File.Exists(debugDbPath))
    debugStore = new WorldMapStore(debugDbPath);   // skipped if missing
```

There is no code that creates `debug.db` and seeds the five built-in
scenarios (`town`, `wasteland`, `m0_combat`, `empty`,
`terrain_test`) into it. The `WorldDbSeeder` class exists but is never
called from `GameBootstrapper`.

So `worldStores` is empty → `FindRegion("town")` iterates zero stores
→ throws.

## Timeline

| Step | What happened |
|------|--------------|
| Pre-i13 | `ScenarioLoader.LoadTown()` etc. built maps in-memory — no DB needed |
| i06 | `WorldDbSeeder` created to populate a DB with the 5 built-in scenarios |
| i08 | `RegionLoader` created to load regions from DB |
| i13 | `ScenarioLoader` stripped; `RegionLoader` made sole path |
| **Gap** | `GameBootstrapper` was never updated to auto-seed `debug.db` |

## Fix

In `GameBootstrapper.Start()`, **before** building the `worldStores`
list, create and seed `debug.db` if it doesn't exist:

```csharp
var debugDbPath = Path.Combine(AppContext.BaseDirectory, "debug.db");
if (!File.Exists(debugDbPath))
{
    using var seedStore = new WorldMapStore(debugDbPath);
    new WorldDbSeeder(seedStore).SeedAll();
}
var debugStore = new WorldMapStore(debugDbPath);
```

This guarantees the five built-in regions are always available.
`world.db` remains optional (only present after a content-pack export).

## Impact

- **Without fix:** Game is completely unplayable — no scenario can load.
- **With fix:** All 5 built-in scenarios work. Content-pack regions
  from `world.db` continue to work if present.
- **Risk:** Low. `WorldDbSeeder.SeedAll()` is already unit-tested
  (6 tests in `WorldDbSeederTests`). The only change is calling it
  at startup when the file is missing.
