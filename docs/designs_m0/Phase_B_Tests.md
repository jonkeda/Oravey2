# Design: Phase B Tests — Inventory, Loot & HUD UI Tests

Adds Brinell UI tests for the Phase B inventory/loot/HUD integration. Requires new automation queries to observe inventory contents, HUD state, and loot entities.

**Depends on:** Phase B implementation (M0Items, LootDropScript, LootPickupScript, HudSyncScript, InventoryOverlayScript)

---

## Problem

After Phase B, the game has new player-visible behavior with no UI test coverage:
- Player starts with a Pipe Wrench equipped and 2 Medkits — no test verifies this
- Killing an enemy spawns a loot cube — no test verifies loot appears
- Walking over loot picks up items — no test verifies pickup works
- HUD displays HP/AP/level/state — no test verifies HUD data matches game state
- Tab toggles an inventory overlay — no test verifies toggle or content

Tests cannot currently observe inventory contents, HUD text values, loot entity positions, or whether the inventory overlay is open.

---

## New Automation Queries

### Game-side: OraveyAutomationHandler

| Query | Args | Returns | Purpose |
|-------|------|---------|---------|
| `GetInventoryState` | — | `{ itemCount, currentWeight, maxWeight, isOverweight, items: [{id, name, category, count, weight}] }` | Full inventory snapshot |
| `GetEquipmentState` | — | `{ slots: { "PrimaryWeapon": {id, name} or null, "Torso": ... } }` | What's equipped in each slot |
| `GetHudState` | — | `{ hp, maxHp, ap, maxAp, level, gameState }` | HUD-visible stats snapshot |
| `GetLootEntities` | — | `{ count, entities: [{name, x, y, z, itemCount}] }` | All loot cubes in scene |
| `GetInventoryOverlayVisible` | — | `{ visible: bool }` | Whether the inventory overlay is currently shown |

### Why these five

- **`GetInventoryState`** — The only way to verify pickup happened. After walking over a loot cube, we need to confirm items appeared in the inventory with correct counts.
- **`GetEquipmentState`** — Verifies starting equipment (Pipe Wrench in PrimaryWeapon). Separate from inventory since equipped items are removed from the item list.
- **`GetHudState`** — The HUD renders text from live components. We verify the data feeding the HUD is correct. (Verifying the actual rendered text would require screenshot OCR, out of scope for M0.)
- **`GetLootEntities`** — After killing an enemy, we need to confirm a loot cube entity was spawned at the enemy's position with items. After pickup, we verify it was removed.
- **`GetInventoryOverlayVisible`** — Verifies Tab toggle behavior. The overlay's `Visibility` property is the source of truth.

### Implementation: OraveyAutomationHandler additions

Route additions in the existing switch:

```csharp
"GetInventoryState" => GetInventoryState(),
"GetEquipmentState" => GetEquipmentState(),
"GetHudState" => GetHudState(),
"GetLootEntities" => GetLootEntities(),
"GetInventoryOverlayVisible" => GetInventoryOverlayVisible(),
```

#### GetInventoryState

Reads the player's `InventoryComponent` (stored on handler or found via a known reference).

```csharp
private AutomationResponse GetInventoryState()
{
    if (_playerInventory == null)
        return AutomationResponse.Fail("Player inventory not initialized");

    var items = _playerInventory.Items.Select(i => new
    {
        id = i.Definition.Id,
        name = i.Definition.Name,
        category = i.Definition.Category.ToString(),
        count = i.StackCount,
        weight = i.TotalWeight,
    });

    var result = new
    {
        itemCount = _playerInventory.Items.Count,
        currentWeight = _playerInventory.CurrentWeight,
        maxWeight = _playerInventory.MaxCarryWeight,
        isOverweight = _playerInventory.IsOverweight,
        items,
    };
    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(result));
}
```

#### GetEquipmentState

Reads each `EquipmentSlot` from the player's `EquipmentComponent`.

```csharp
private AutomationResponse GetEquipmentState()
{
    if (_playerEquipment == null)
        return AutomationResponse.Fail("Player equipment not initialized");

    var slots = new Dictionary<string, object?>();
    foreach (var slot in Enum.GetValues<EquipmentSlot>())
    {
        var item = _playerEquipment.GetEquipped(slot);
        slots[slot.ToString()] = item != null
            ? new { id = item.Definition.Id, name = item.Definition.Name }
            : null;
    }

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new { slots }));
}
```

#### GetHudState

Reads from the same components the HudSyncScript uses:

```csharp
private AutomationResponse GetHudState()
{
    var result = new
    {
        hp = _playerHealth?.CurrentHP ?? 0,
        maxHp = _playerHealth?.MaxHP ?? 0,
        ap = (int)(_playerCombat?.CurrentAP ?? 0),
        maxAp = _playerCombat?.MaxAP ?? 0,
        level = _playerLevel?.Level ?? 0,
        gameState = _gameStateManager?.CurrentState.ToString() ?? "Unknown",
    };
    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(result));
}
```

#### GetLootEntities

Scans the scene for entities with the `LootItemsKey` tag:

```csharp
private AutomationResponse GetLootEntities()
{
    var lootEntities = _rootScene.Entities
        .Where(e => e.Tags.TryGetValue(LootDropScript.LootItemsKey, out _))
        .Select(e =>
        {
            e.Tags.TryGetValue(LootDropScript.LootItemsKey, out var items);
            return new
            {
                name = e.Name,
                x = e.Transform.Position.X,
                y = e.Transform.Position.Y,
                z = e.Transform.Position.Z,
                itemCount = items?.Count ?? 0,
            };
        });

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        count = lootEntities.Count(),
        entities = lootEntities,
    }));
}
```

#### GetInventoryOverlayVisible

Finds the `InventoryOverlayScript` and checks its UI root element visibility:

```csharp
private AutomationResponse GetInventoryOverlayVisible()
{
    var overlayEntity = FindEntity("InventoryOverlay");
    var script = overlayEntity?.Get<InventoryOverlayScript>();
    if (script == null)
        return AutomationResponse.Fail("InventoryOverlay entity not found");

    // The overlay stores visibility state internally
    var visible = script.IsVisible;
    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new { visible }));
}
```

> **Note:** `InventoryOverlayScript` needs a public `bool IsVisible` property exposing `_visible` for automation access.

---

## Handler Constructor Changes

`OraveyAutomationHandler` needs additional references passed from Program.cs:

```csharp
// Fields
private readonly InventoryComponent? _playerInventory;
private readonly EquipmentComponent? _playerEquipment;
private readonly HealthComponent? _playerHealth;
private readonly CombatComponent? _playerCombat;
private readonly LevelComponent? _playerLevel;
private readonly GameStateManager? _gameStateManager;
```

These are set via constructor parameters or a dedicated `SetPhaseB(...)` wiring method called in Program.cs after all components are created.

---

## Test-side: GameQueryHelpers additions

```csharp
// --- Inventory / Loot helpers ---

public record InventoryItem(string Id, string Name, string Category, int Count, double Weight);

public record InventoryState(
    int ItemCount, double CurrentWeight, double MaxWeight,
    bool IsOverweight, List<InventoryItem> Items);

public record EquipmentSlotInfo(string? Id, string? Name);

public record EquipmentState(Dictionary<string, EquipmentSlotInfo?> Slots);

public record HudState(int Hp, int MaxHp, int Ap, int MaxAp, int Level, string GameState);

public record LootEntityInfo(string Name, double X, double Y, double Z, int ItemCount);

public record LootEntitiesState(int Count, List<LootEntityInfo> Entities);

public static InventoryState GetInventoryState(IStrideTestContext context) { ... }
public static EquipmentState GetEquipmentState(IStrideTestContext context) { ... }
public static HudState GetHudState(IStrideTestContext context) { ... }
public static LootEntitiesState GetLootEntities(IStrideTestContext context) { ... }
public static bool GetInventoryOverlayVisible(IStrideTestContext context) { ... }
```

---

## File Layout

```
src/Oravey2.Windows/
└── OraveyAutomationHandler.cs        # MODIFY — add 5 new queries + new fields
src/Oravey2.Core/
└── UI/Stride/
    └── InventoryOverlayScript.cs     # MODIFY — add public IsVisible property

tests/Oravey2.UITests/
├── GameQueryHelpers.cs               # MODIFY — add 5 new records + 5 new helpers
├── StartingInventoryTests.cs         # NEW — verify starting items & equipment
├── LootDropTests.cs                  # NEW — verify loot spawn on death
├── LootPickupTests.cs               # NEW — verify pickup mechanics
├── HudStateTests.cs                  # NEW — verify HUD data accuracy
└── InventoryOverlayTests.cs         # NEW — verify Tab toggle + content display
```

---

## World Reference

Same as Phase A:
```
32×32 grid, TileSize=1.0, centered at world origin.
Player starts at world (0, 0.5, 0).
Enemies:
  enemy_1 at (8,   0.5,  8)    — NE quadrant
  enemy_2 at (-6,  0.5, 10)    — NW quadrant
  enemy_3 at (10,  0.5, -6)    — SE quadrant
Trigger radius: 5 units
Loot pickup radius: 1.5 units
```

### M0 starting inventory

| Item | Count | Where |
|------|-------|-------|
| Pipe Wrench | 1 | Equipped in PrimaryWeapon |
| Medkit | 2 | In inventory (stacked) |

### M0 loot table

| Item | Drop Chance | Weight |
|------|-------------|--------|
| Scrap Metal | 70% | 1.0 |
| Medkit | 30% | 0.5 |
| Pipe Wrench | 15% | 2.5 |
| Leather Jacket | 10% | 3.0 |

Max 2 items per drop.

---

## Test Class 1: StartingInventoryTests

Verifies the player's initial inventory and equipment state immediately after game start. Fresh game process per class.

| # | Test | Steps | Assertion |
|---|------|-------|-----------|
| 1 | `PlayerHas_PipeWrenchEquipped` | `GetEquipmentState()` | `PrimaryWeapon.Id == "pipe_wrench"` |
| 2 | `PlayerHas_MedkitsInInventory` | `GetInventoryState()` | Contains item with `id == "medkit"` and `count == 2` |
| 3 | `StartingWeight_IsCorrect` | `GetInventoryState()` | `CurrentWeight == 1.0` (2 Medkits × 0.5 = 1.0; wrench is equipped, not in bag) |
| 4 | `NotOverweight_AtStart` | `GetInventoryState()` | `IsOverweight == false` |
| 5 | `EquipmentSlots_MostlyEmpty` | `GetEquipmentState()` | Only `PrimaryWeapon` is non-null; Head, Torso, Legs, Feet, SecondaryWeapon, Accessory1, Accessory2 are null |

---

## Test Class 2: LootDropTests

Verifies that killing an enemy spawns a loot cube entity. Uses `TeleportPlayer` + `KillEnemy` from Phase A.

| # | Test | Steps | Assertion |
|---|------|-------|-----------|
| 1 | `NoLootEntities_AtStart` | `GetLootEntities()` | `Count == 0` |
| 2 | `KillEnemy_SpawnsLootCube` | Teleport near enemy_1 → wait InCombat → `KillEnemy("enemy_1")` → `HoldKey(Space, 200)` (advance frames for drop) → `GetLootEntities()` | `Count >= 1` (loot is RNG-based, but at 70% + 30% + 15% + 10% chances, the probability of zero items is very low) |
| 3 | `LootCube_AtEnemyPosition` | After killing enemy_1 → `GetLootEntities()` | First loot entity position ≈ enemy_1 position (8, 0.5, 8) within tolerance 1.0 |
| 4 | `LootCube_HasItems` | After killing enemy_1 → `GetLootEntities()` | First loot entity `itemCount >= 1` |
| 5 | `MultipleLootCubes_FromMultipleKills` | Kill enemy_1 and enemy_2 → advance frames → `GetLootEntities()` | `Count >= 2` (one per enemy, assuming at least one item dropped each) |

**"wait InCombat + kill" pattern (reused from Phase A):**
```csharp
GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
for (int i = 0; i < 10; i++)
{
    _fixture.Context.HoldKey(VirtualKey.Space, 50);
    var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
    if (combat.InCombat) break;
}
GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
_fixture.Context.HoldKey(VirtualKey.Space, 200); // advance frames for loot spawn
```

**Note on RNG:** Loot drops are random. Test 2 uses `>= 1` rather than exact count. The probability of getting zero items from the loot table (all 4 entries miss) is approximately `(1-0.7) × (1-0.3) × (1-0.15) × (1-0.1) ≈ 16%`. If this causes occasional flakes, we can add a deterministic `ForceLootDrop` query. For M0, the ≥1 assertion is acceptable.

---

## Test Class 3: LootPickupTests

Verifies walking over a loot cube picks up items and removes the cube entity.

| # | Test | Steps | Assertion |
|---|------|-------|-----------|
| 1 | `WalkOverLoot_PicksUpItems` | Kill enemy_1 → advance frames → get loot position → `TeleportPlayer` to loot position → `HoldKey(Space, 200)` (advance frames for pickup) → `GetInventoryState()` | `ItemCount > 1` (started with 1 item slot for medkits; now has more) |
| 2 | `WalkOverLoot_RemovesLootEntity` | After pickup from test 1 → `GetLootEntities()` | Count decreased (loot entity removed after pickup) |
| 3 | `PickupAdds_ToExistingStacks` | If medkit drops: before pickup count medkits → teleport to loot → advance → count medkits again | Medkit count increased (stacking works) |
| 4 | `Inventory_WeightIncreases_AfterPickup` | Get weight before → pick up loot → get weight after | `after.CurrentWeight > before.CurrentWeight` |

**Note:** Tests 1–4 share a game process. Test order matters — killing enemy_1 creates loot, teleporting picks it up, subsequent tests verify the accumulated state.

**Pickup flow:**
```csharp
// Kill enemy to get loot
GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
// ... WaitForCombat pattern ...
GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
_fixture.Context.HoldKey(VirtualKey.Space, 200);

// Get loot position
var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
Assert.True(loot.Count > 0, "Should have loot to pick up");

// Teleport to the loot cube and let pickup script fire
var lootPos = loot.Entities[0];
GameQueryHelpers.TeleportPlayer(_fixture.Context, lootPos.X, 0.5, lootPos.Z);
_fixture.Context.HoldKey(VirtualKey.Space, 200); // advance frames for pickup
```

---

## Test Class 4: HudStateTests

Verifies the HUD data matches live game state. The HudSyncScript reads from HealthComponent, CombatComponent, LevelComponent — we verify those values are accessible via automation.

| # | Test | Steps | Assertion |
|---|------|-------|-----------|
| 1 | `HudState_HasFullHealth_AtStart` | `GetHudState()` | `Hp == MaxHp == 105` (default stats: 50 + 5×10 + 1×5) |
| 2 | `HudState_ShowsExploring_AtStart` | `GetHudState()` | `GameState == "Exploring"` |
| 3 | `HudState_ShowsLevel1_AtStart` | `GetHudState()` | `Level == 1` |
| 4 | `HudState_ApMatches_MaxAp` | `GetHudState()` | `Ap == MaxAp == 10` |
| 5 | `HudState_ShowsInCombat_WhenFighting` | Teleport near enemy_1 → wait InCombat → `GetHudState()` | `GameState == "InCombat"` |
| 6 | `HudState_HealthDecreases_InCombat` | Teleport near enemy_1 → wait InCombat → idle 4s (`HoldKey(W, 4000)`) → `GetHudState()` | `Hp < MaxHp` (enemies attacked) |

---

## Test Class 5: InventoryOverlayTests

Verifies Tab toggles the inventory overlay and displays correct content.

| # | Test | Steps | Assertion |
|---|------|-------|-----------|
| 1 | `Overlay_NotVisible_AtStart` | `GetInventoryOverlayVisible()` | `== false` |
| 2 | `TabPress_OpensOverlay` | `PressKey(VirtualKey.Tab)` → `GetInventoryOverlayVisible()` | `== true` |
| 3 | `TabPress_ClosesOverlay` | Press Tab twice → `GetInventoryOverlayVisible()` | `== false` |
| 4 | `Overlay_ShowsStartingItems` | Press Tab → `GetInventoryState()` | Has medkit×2 in items |
| 5 | `Overlay_ShowsCorrectWeight` | Press Tab → `GetInventoryState()` | `CurrentWeight == 1.0` (2 Medkits × 0.5) |

**Tab key:** `VirtualKey.Tab` (0x09) must exist in the Brinell VirtualKey enum. If it doesn't, use `PressKey` with the raw code or `HoldKey(VirtualKey.Tab, 50)`.

**Note:** Tests 4 and 5 verify inventory data (not rendered text). Actual on-screen text verification would require screenshot + OCR which is deferred to post-M0. The Tab toggle test (2-3) confirms the overlay visibility flag changes.

---

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| 1 | `GetInventoryState`, `GetEquipmentState`, `GetHudState`, `GetLootEntities`, `GetInventoryOverlayVisible` queries work via automation pipe |
| 2 | `StartingInventoryTests` — 5 tests pass |
| 3 | `LootDropTests` — 5 tests pass |
| 4 | `LootPickupTests` — 4 tests pass |
| 5 | `HudStateTests` — 6 tests pass |
| 6 | `InventoryOverlayTests` — 5 tests pass |
| 7 | All existing UI tests (49 active) still pass |
| 8 | All 635 unit tests still pass |
