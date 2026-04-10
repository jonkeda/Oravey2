# Step i12 — Zone Transitions Between Regions

**Design doc:** 04
**Depends on:** i11 (RegionSelectorScript — regions are loadable)
**Deliverable:** `ZoneManager` supports transitioning between regions.
Save state tracks `current_region`.

---

## Goal

Players can move between regions at runtime via zone exit triggers.
When transitioning, the current region is unloaded, position is saved,
and the target region is loaded.

---

## Tasks

### i12.1 — Refactor `ZoneManager` for region transitions

File: `src/Oravey2.Core/World/ZoneManager.cs`

- [ ] Replace hardcoded zone name dispatch with:
  ```csharp
  public void TransitionToRegion(string regionName,
      string? spawnPoi = null)
  {
      SaveCurrentPosition();
      _regionLoader.UnloadCurrentRegion();
      Vector3? spawnPos = ResolveSpawnPosition(regionName, spawnPoi);
      _regionLoader.LoadRegion(regionName, spawnPos);
  }
  ```
- [ ] `SaveCurrentPosition()` writes player pos to save state
  under current region name
- [ ] `ResolveSpawnPosition()` checks: save state first, then
  POI lookup, then region default

### i12.2 — Save `current_region` in save state

File: `src/Oravey2.Core/Data/SaveStateStore.cs`

- [ ] Add `SetCurrentRegion(string name)` method
- [ ] Add `GetCurrentRegion()` → `string?` method
- [ ] Uses `save_meta` table: key `current_region`

### i12.3 — Save per-region player position

File: `src/Oravey2.Core/Data/SaveStateStore.cs`

- [ ] `SavePlayerPosition(string regionName, Vector3 position)`
- [ ] `GetPlayerPosition(string regionName)` → `Vector3?`
- [ ] Store in `save_meta` as `pos:{regionName}` → JSON `{x,y,z}`

### i12.4 — Wire `ZoneExitTriggerScript` to `ZoneManager`

File: `src/Oravey2.Core/World/ZoneExitTriggerScript.cs` (or existing
zone trigger script)

- [ ] On trigger enter: call
  `ZoneManager.TransitionToRegion(TargetRegionName)`
- [ ] The target region name comes from the `zone_exit:{target}`
  entity spawn

### i12.5 — "Continue Game" flow

File: `src/Oravey2.Core/Bootstrap/GameBootstrapper.cs`

- [ ] On "Continue": read `current_region` from save → load that
  region → restore position from save
- [ ] Fallback to first region in DB if save has no region

### i12.6 — Tests

File: `tests/Oravey2.Tests/World/ZoneTransitionTests.cs`

- [ ] `SaveCurrentRegion_PersistsToSaveDb`
- [ ] `GetCurrentRegion_ReturnsLastSaved`
- [ ] `SavePlayerPosition_RoundTrips` — save pos → load pos → match
- [ ] `TransitionToRegion_SavesPositionBeforeUnload`
- [ ] Build + all tests pass

---

## Files changed

| File | Action |
|------|--------|
| `ZoneManager.cs` | **Refactor** — region-aware transitions |
| `SaveStateStore.cs` | **Extend** — current_region, per-region pos |
| `ZoneExitTriggerScript.cs` | **Modify** — call ZoneManager |
| `GameBootstrapper.cs` | **Modify** — Continue Game flow |
| `ZoneTransitionTests.cs` | **New** |
