# Step i11 — RegionSelectorScript

**Design doc:** 04
**Depends on:** i08 (RegionLoader)
**Deliverable:** In-game region picker that lists regions from the
database. Replaces the hardcoded `ScenarioSelectorScript` scenarios
array.

---

## Goal

The debug scenario selector currently has a hardcoded array of
built-in scenarios. Replace it with a data-driven list read from
`world.db` and optionally `debug.db`. Debug regions show a `[DEBUG]`
tag in dev builds.

---

## Tasks

### i11.1 — Create `RegionSelectorScript`

File: `src/Oravey2.Core/UI/Stride/RegionSelectorScript.cs`

- [ ] Replaces or extends `ScenarioSelectorScript`
- [ ] On `Start()`: query `WorldMapStore.GetAllRegions()` from all
  open databases
- [ ] Display list with: region name, biome, difficulty
- [ ] Tag debug.db regions with `[DEBUG]` prefix
- [ ] Hide debug regions when `#if !DEBUG` or config flag

### i11.2 — Navigation and selection

- [ ] Up/Down arrows to navigate
- [ ] Enter to confirm → calls `GameBootstrapper.StartScenario(name)`
- [ ] Escape to go back to main menu

### i11.3 — "Import Content Pack" option

- [ ] Press `I` to trigger import dialog
- [ ] Calls `ContentPackImporter.Import()` on selected folder
- [ ] Refreshes region list
- [ ] Dev-only feature (guarded by `#if DEBUG` or config)

### i11.4 — Retire `ScenarioSelectorScript`

- [ ] Remove the hardcoded `Scenarios` array
- [ ] Remove `DiscoverCustomMaps()` (custom maps are now imported
  into DB)
- [ ] Either refactor in-place or create new script and swap in
  `GameBootstrapper`

### i11.5 — Tests

- [ ] `RegionSelectorScript` is a Stride `SyncScript` — testable
  parts are the region discovery logic, not the input/rendering
- [ ] Unit test: given a WorldMapStore with 3 regions, discover
  returns 3 `ScenarioInfo` entries
- [ ] Unit test: debug regions from second store are tagged
- [ ] Build + all tests pass

---

## Files changed

| File | Action |
|------|--------|
| `RegionSelectorScript.cs` | **New or major refactor** of `ScenarioSelectorScript.cs` |
| `GameBootstrapper.cs` | **Modify** — wire new selector script |
| Region discovery tests | **New** |
