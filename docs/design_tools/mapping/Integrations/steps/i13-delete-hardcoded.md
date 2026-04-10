# Step i13 — Delete Hardcoded Scenarios

**Design doc:** 01 phase 5
**Depends on:** i08 (RegionLoader fully working), i06 (all scenarios
seeded in debug.db)
**Deliverable:** All hardcoded `LoadXxx()` methods removed.
`ScenarioLoader` class deleted. Map builder classes deleted.

---

## Goal

Final cleanup. Once `RegionLoader` handles all loading and
`WorldDbSeeder` produces equivalent data for all 5 built-in
scenarios, the hardcoded code is dead and can be removed.

---

## Prerequisite verification

Before deleting anything, verify parity:

- [ ] Run the game with `RegionLoader` loading each of the 5 debug
  regions — gameplay matches the old hardcoded behavior
- [ ] All existing unit tests still pass
- [ ] All existing UI tests still pass (they use the game process)

---

## Tasks

### i13.1 — Delete hardcoded load methods

File: `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs`

- [ ] Delete `LoadM0Combat()`
- [ ] Delete `LoadEmpty()`
- [ ] Delete `LoadTown()`
- [ ] Delete `LoadWasteland()`
- [ ] Delete `LoadTerrainTest()`
- [ ] Delete `LoadFromCompiledMap()` (custom maps are imported into
  DB now)
- [ ] Delete the `switch` statement in `Load()`
- [ ] Delete `ScenarioLoader.cs` entirely if nothing else references
  it

### i13.2 — Delete map builder classes

- [ ] Delete `TownMapBuilder.cs` (only used by `LoadTown` and
  `WorldDbSeeder` — seeder should call its own version or the
  builder is kept only in seeder's namespace)

  **Important**: If `WorldDbSeeder.SeedTown()` still calls
  `TownMapBuilder.CreateTownMap()`, do NOT delete the builder.
  Only delete if the seeder has its own tile generation or if the
  builder was inlined. Decide at implementation time.

- [ ] Delete `WastelandMapBuilder.cs` (same caveat)
- [ ] Delete `TerrainTestData.cs` (same caveat)

### i13.3 — Delete `ScenarioSelectorScript` (if replaced)

- [ ] If `RegionSelectorScript` (step i11) fully replaces it,
  delete `ScenarioSelectorScript.cs`
- [ ] Remove any remaining references

### i13.4 — Clean up `GameBootstrapper`

- [ ] Remove `ScenarioLoader` field and instantiation
- [ ] Remove any fallback paths to old loading code
- [ ] `StartScenario()` should only call `RegionLoader.LoadRegion()`

### i13.5 — Clean up tests

- [ ] Delete or update any unit tests that directly tested the
  hardcoded methods (e.g., `ScenarioLoaderTests` if they existed)
- [ ] Ensure all remaining tests pass

### i13.6 — Build verification

- [ ] `dotnet build src/Oravey2.Core/Oravey2.Core.csproj` — 0 errors
- [ ] `dotnet build src/Oravey2.Windows/Oravey2.Windows.csproj` — 0 errors
- [ ] `dotnet test tests/Oravey2.Tests/Oravey2.Tests.csproj` — all pass

---

## Files changed

| File | Action |
|------|--------|
| `ScenarioLoader.cs` | **Delete** |
| `TownMapBuilder.cs` | **Delete** (if not needed by seeder) |
| `WastelandMapBuilder.cs` | **Delete** (if not needed by seeder) |
| `TerrainTestData.cs` | **Delete** (if not needed by seeder) |
| `ScenarioSelectorScript.cs` | **Delete** (if replaced by i11) |
| `GameBootstrapper.cs` | **Modify** — remove old references |
| Old test files | **Delete or update** |
