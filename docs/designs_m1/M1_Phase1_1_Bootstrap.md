# M1 Phase 1.1 — Bootstrap & Scenario Redesign

**Problem:** Game currently initializes the entire world (player, enemies, tile map, HUD, combat) in `Program.Start()` regardless of mode. Both the start menu overlay and the live game world are visible simultaneously.

**Goal:** Clean separation between menu-only state and active gameplay. Scenario-driven world loading.

---

## 1. Current Flow (broken)

```
Program.Start()
  → Register services
  → Create scene (light, skybox)
  → Spawn Player, Enemies, TileMap, Combat, HUD, etc.   ← ALWAYS
  → Create menus (Start, Pause, Settings)
  → if automation: hide menu, state=Exploring
    else: state=InMenu (but world is visible behind menu)
```

The world entities exist and render behind the start menu overlay.

---

## 2. New Flow

```
Program.Start()
  → Register services
  → Create scene infrastructure (light, skybox, camera, input)
  → Create menus (Start, Pause, Settings)
  → Wire automation handler (with stubs — no game refs yet)
  → if automation: call LoadScenario("m0_combat")   ← direct start for tests
    else: state=InMenu, show start menu only

StartMenu "New Game" clicked:
  → call LoadScenario("m0_combat")

StartMenu "Continue" clicked:
  → call LoadScenario("m0_combat")
  → apply save data via restorer
```

### 2.1 Key Concept: `LoadScenario(string scenarioId)`

A scenario defines what gets spawned. Keeps Program.cs clean and enables:
- Different scenarios for tests (e.g., "m0_combat" for combat tests, "empty" for smoke)
- Future zone loading (Phase 2 will add "town", "wasteland")
- Automation can request a specific scenario

### 2.2 Scenario Registry

```csharp
// Lives in Oravey2.Windows since it references Game for mesh creation
public class ScenarioLoader
{
    public void Load(string scenarioId, Scene rootScene, Game game);
    public void Unload(Scene rootScene);  // for quit-to-menu
}
```

**M1 scenarios:**

| Scenario ID  | Contents                                                  | Used by            |
|-------------|-----------------------------------------------------------|--------------------|
| `m0_combat` | Player + 3 enemies + tile map + combat + loot + HUD + UI | Default new game   |
| `empty`     | Player only (no enemies, basic tile map)                  | Quick smoke tests  |

### 2.3 What stays in Start() always (scene infrastructure)

- ServiceLocator registrations
- Graphics compositor + light + skybox
- Input entity
- Camera entity (no target yet — orbits origin)
- Notification feed entity
- Menu entities (Start, Pause, Settings)
- Automation server setup

### 2.4 What moves into ScenarioLoader.Load()

- Player entity creation + visual + movement script
- Player stats/health/combat/inventory/equipment
- Enemies + combat manager + encounter trigger
- Tile map
- Loot system
- HUD, inventory overlay, game over overlay, floating damage, enemy HP bars
- Wiring camera target to player
- Wiring automation handler game refs (SetPhaseB, SetPhaseC)
- State transition to `Exploring`

---

## 3. Automation Changes

### 3.1 Direct Scenario Start

Automation command line gains `--scenario <id>`:

```
Oravey2.Windows.exe --automation --scenario m0_combat
```

- `--automation` alone → loads `m0_combat` and starts in Exploring (backward compat)
- `--automation --scenario empty` → loads empty scenario for smoke tests
- No `--automation` → shows start menu

### 3.2 Automation Query: LoadScenario

New query `LoadScenario` with request `{ scenarioId: "m0_combat" }` to load/switch at runtime. Not needed for M1 but keeps the door open.

---

## 4. Quick Test Subset

Add `[Trait("Category", "Smoke")]` to a small set of fast-feedback tests.

**Smoke tests** (1 per area, ~30s total):

| Test class            | Test                            | Validates          |
|-----------------------|--------------------------------|--------------------|
| GameLifecycleTests    | Game_StartsAndConnects         | Boot + automation  |
| SpatialMovementTests  | FirstTest (movement)           | Player movement    |
| CameraDefaultTests    | FirstTest (camera)             | Camera works       |
| HudStateTests         | FirstTest (HUD)                | HUD renders        |
| MenuSaveLoadTests     | GameState_StartsAsExploring    | Menu/state system  |
| MenuSaveLoadTests     | SaveLoad_RoundTrip_Position    | Save/load pipeline |

Run with: `dotnet test --filter "Category=Smoke"`

---

## 5. Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `src/Oravey2.Windows/ScenarioLoader.cs` | Scenario load/unload logic |
| Modify | `src/Oravey2.Windows/Program.cs` | Split into infra + scenario, use ScenarioLoader |
| Modify | `src/Oravey2.Windows/OraveyAutomationHandler.cs` | Add LoadScenario query |
| Modify | `src/Oravey2.Core/Automation/AutomationContracts.cs` | Add LoadScenarioRequest/Response |
| Modify | `tests/Oravey2.UITests/GameQueryHelpers.cs` | Add LoadScenario helper |
| Modify | Various UI test files | Add `[Trait("Category", "Smoke")]` to subset |
| Modify | `src/Oravey2.Core/UI/Stride/HudSyncScript.cs` | Hide when state is InMenu/Loading |

---

## 6. Acceptance Criteria

1. Normal launch → only start menu visible (no world entities, no HUD)
2. "New Game" → world loads, menu hides, gameplay starts
3. "Continue" → world loads, save restored, menu hides
4. Automation mode → scenario loads directly, Exploring state
5. `dotnet test --filter "Category=Smoke"` runs ~6 tests in <30s
6. All existing UI tests still pass
