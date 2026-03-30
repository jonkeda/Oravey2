# Design: Phase B — Inventory, Loot & HUD Integration

Wires the existing pure-C# inventory system (Step 02) and HUD view-models (Step 08) into the live Stride game loop. After this phase, enemies drop loot on death, the player can pick up items by walking over them, the HUD displays HP/AP/level, and pressing Tab opens an inventory overlay showing carried items.

**Depends on:** Phase A (combat working), Steps 02 + 08 (pure C# inventory + UI view-models)

---

## Scope

| Sub-task | Summary |
|----------|---------|
| B1 | Create M0 item definitions (pipe wrench, medkit, scrap, leather jacket) |
| B2 | Wire player InventoryComponent + EquipmentComponent in Program.cs |
| B3 | LootDropScript — enemies drop a loot entity on death |
| B4 | LootPickupScript — proximity auto-pickup on walkable tiles |
| B5 | HudSyncScript — render HP bar, AP bar, level text via Stride UI |
| B6 | InventoryOverlayScript — Tab toggles an inventory item list overlay |

### What's Deferred

| Item | Deferred To |
|------|-------------|
| Full crafting station UI | Phase C / Post-M0 |
| Equipment stat effects on combat | Post-M0 (M0 uses hardcoded weapon constants) |
| Durability degradation per attack | Post-M0 |
| Item icons / 3D preview | Post-M0 |
| Drag & drop inventory management | Post-M0 |
| Quick slot bar rendering | Post-M0 |
| Drop items from inventory | Post-M0 |
| Save/load of inventory state | Phase D |

---

## File Layout

```
src/Oravey2.Core/
├── Inventory/
│   └── Items/
│       └── M0Items.cs                  # NEW — static item definitions for M0
├── Loot/
│   ├── LootDropScript.cs               # NEW — spawns loot entity on EntityDiedEvent
│   ├── LootPickupScript.cs             # NEW — proximity-triggered item pickup
│   └── LootTable.cs                    # NEW — simple weighted random loot selection
├── UI/
│   └── Stride/
│       ├── HudSyncScript.cs            # NEW — renders HP/AP/LVL via Stride UI
│       └── InventoryOverlayScript.cs   # NEW — Tab-toggled inventory list
src/Oravey2.Windows/
└── Program.cs                          # MODIFY — wire inventory, loot, HUD
```

---

## Existing APIs We'll Use

### InventoryComponent

```csharp
bool CanAdd(ItemInstance item)     // weight check
bool Add(ItemInstance item)        // stack-aware add
bool Remove(string itemId, int count)
bool Contains(string itemId, int count)
float CurrentWeight { get; }
float MaxCarryWeight { get; }
```

### InventoryProcessor

```csharp
bool TryPickup(ItemInstance item)   // CanAdd + Add + publishes ItemPickedUpEvent
bool TryEquip(ItemInstance item, EquipmentSlot slot)
bool TryUnequip(EquipmentSlot slot)
```

### EquipmentComponent

```csharp
ItemInstance? GetEquipped(EquipmentSlot slot)
ItemInstance? Equip(ItemInstance item, EquipmentSlot slot)
ItemInstance? Unequip(EquipmentSlot slot)
```

### InventoryViewModel

```csharp
static InventoryViewModel Create(InventoryComponent inventory)
// Returns: Items[], CurrentWeight, MaxCarryWeight, IsOverweight
```

### HudViewModel

```csharp
static HudViewModel Create(HealthComponent, CombatComponent, LevelComponent,
    DayNightCycleProcessor, string? zone, SurvivalComponent?, RadiationComponent?,
    QuickSlotBar)
```

### Events (already defined)

- `ItemPickedUpEvent(string ItemId)` — published by InventoryProcessor.TryPickup
- `ItemDroppedEvent(string ItemId, int Count)` — published by InventoryProcessor.TryDrop
- `ItemEquippedEvent(string ItemId, EquipmentSlot Slot)`
- `EntityDiedEvent()` — published when enemy HP reaches 0
- `NotificationEvent(string Message, float DurationSeconds)`

---

## B1 — M0 Item Definitions

### M0Items.cs

Static factory returning the 4 item definitions used in M0. No JSON loading — hardcoded for simplicity.

```csharp
namespace Oravey2.Core.Inventory.Items;

public static class M0Items
{
    public static ItemDefinition PipeWrench() => new(
        Id: "pipe_wrench",
        Name: "Pipe Wrench",
        Description: "A heavy pipe wrench. Better than bare fists.",
        Category: ItemCategory.WeaponMelee,
        Weight: 2.5f,
        Stackable: false,
        Value: 15,
        Slot: EquipmentSlot.PrimaryWeapon,
        Weapon: new WeaponData(
            Damage: 14,
            Range: 2f,
            ApCost: 3,
            Accuracy: 0.80f,
            SkillType: "melee",
            CritMultiplier: 1.5f));

    public static ItemDefinition Medkit() => new(
        Id: "medkit",
        Name: "Medkit",
        Description: "Restores 30 HP. A salvaged first-aid kit.",
        Category: ItemCategory.Consumable,
        Weight: 0.5f,
        Stackable: true,
        Value: 25,
        MaxStack: 5,
        Effects: new Dictionary<string, string> { { "heal", "30" } });

    public static ItemDefinition ScrapMetal() => new(
        Id: "scrap_metal",
        Name: "Scrap Metal",
        Description: "Twisted metal fragments. Useful for crafting.",
        Category: ItemCategory.CraftingMaterial,
        Weight: 1.0f,
        Stackable: true,
        Value: 3,
        MaxStack: 20);

    public static ItemDefinition LeatherJacket() => new(
        Id: "leather_jacket",
        Name: "Leather Jacket",
        Description: "Worn but sturdy. Offers some protection.",
        Category: ItemCategory.Armor,
        Weight: 3.0f,
        Stackable: false,
        Value: 20,
        Slot: EquipmentSlot.Torso,
        Armor: new ArmorData(
            DamageReduction: 3,
            CoverageZones: new Dictionary<string, float>
            {
                { "torso", 0.8f }, { "arms", 0.5f }
            }));
}
```

---

## B2 — Wire Player Inventory in Program.cs

After the existing player combat data, add:

```csharp
// --- Player inventory ---
var playerInventory = new InventoryComponent(playerStats);
var playerEquipment = new EquipmentComponent();
var inventoryProcessor = new InventoryProcessor(playerInventory, playerEquipment, eventBus);

// Give player a starting pipe wrench
var startingWeapon = new ItemInstance(M0Items.PipeWrench());
inventoryProcessor.TryPickup(startingWeapon);
inventoryProcessor.TryEquip(startingWeapon, EquipmentSlot.PrimaryWeapon);
```

---

## B3 — LootDropScript

A `SyncScript` that listens for enemy death and spawns a loot entity at the enemy's position.

### LootTable.cs

Simple weighted random selection:

```csharp
namespace Oravey2.Core.Loot;

using Oravey2.Core.Inventory.Items;

public sealed class LootTable
{
    private readonly List<(ItemDefinition Item, float Weight)> _entries = [];
    private static readonly Random _rng = new();

    public void Add(ItemDefinition item, float weight) =>
        _entries.Add((item, weight));

    /// <summary>
    /// Selects 1-maxCount items using weighted random.
    /// Each entry is rolled independently against its weight as a drop chance (0-1).
    /// </summary>
    public List<ItemInstance> Roll(int maxCount = 2)
    {
        var drops = new List<ItemInstance>();
        foreach (var (item, weight) in _entries)
        {
            if (drops.Count >= maxCount) break;
            if (_rng.NextDouble() < weight)
                drops.Add(new ItemInstance(item));
        }
        return drops;
    }
}
```

### LootDropScript.cs

```csharp
namespace Oravey2.Core.Loot;

using Oravey2.Core.Inventory.Items;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

/// <summary>
/// Spawns yellow cube loot entities at enemy death positions.
/// Called from CombatSyncScript.CleanupDead() rather than event-driven,
/// because we need the enemy's position before the entity is removed.
/// </summary>
public class LootDropScript : SyncScript
{
    internal LootTable? LootTable { get; set; }

    private readonly Queue<(Vector3 Position, List<ItemInstance> Items)> _pendingDrops = new();

    /// <summary>
    /// Called by CombatSyncScript when an enemy dies, before entity removal.
    /// </summary>
    internal void QueueDrop(Vector3 position)
    {
        var items = LootTable?.Roll() ?? [];
        if (items.Count > 0)
            _pendingDrops.Enqueue((position, items));
    }

    public override void Update()
    {
        while (_pendingDrops.TryDequeue(out var drop))
        {
            SpawnLootEntity(drop.Position, drop.Items);
        }
    }

    private void SpawnLootEntity(Vector3 position, List<ItemInstance> items)
    {
        var lootEntity = new Entity($"loot_{position.X:F0}_{position.Z:F0}");
        lootEntity.Transform.Position = position;

        // Visual: small yellow cube
        var visual = new Entity("LootVisual");
        var cubeMesh = GeometricPrimitive.Cube.New(Game.GraphicsDevice, 0.3f).ToMeshDraw();
        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = cubeMesh });
        // Use a simple yellow material — created in Start() and cached
        if (_lootMaterial != null)
            model.Materials.Add(_lootMaterial);
        visual.Add(new ModelComponent(model));
        lootEntity.AddChild(visual);

        // Store items as a component tag (lightweight approach for M0)
        lootEntity.Tags.Set(LootItemsKey, items);

        Entity.Scene?.Entities.Add(lootEntity);
    }

    // Material cached on Start
    private MaterialInstance? _lootMaterial;

    public static readonly Stride.Core.PropertyKey<List<ItemInstance>> LootItemsKey =
        new("LootItems", typeof(LootDropScript));

    public override void Start()
    {
        base.Start();
        // Create yellow material for loot cubes
        var descriptor = new MaterialDescriptor
        {
            Attributes =
            {
                Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor(new Color4(0.9f, 0.8f, 0.1f, 1.0f))),
                DiffuseModel = new MaterialDiffuseLambertModelFeature()
            }
        };
        var material = Material.New(Game.GraphicsDevice, descriptor);
        _lootMaterial = new MaterialInstance(material);
    }
}
```

### Integration: CombatSyncScript calls LootDropScript

Add a `LootDrop` property to `CombatSyncScript`:

```csharp
public LootDropScript? LootDrop { get; set; }
```

In the existing `CleanupDead()` method, before removing the entity:

```csharp
// Existing code: if (!enemy.Health.IsAlive) ...
LootDrop?.QueueDrop(enemy.Entity.Transform.Position);
// Then remove entity as before
```

---

## B4 — LootPickupScript

A `SyncScript` on the player entity that checks for nearby loot entities each frame.

```csharp
namespace Oravey2.Core.Loot;

using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

/// <summary>
/// Auto-picks up loot when the player walks within PickupRadius.
/// Runs on the player entity.
/// </summary>
public class LootPickupScript : SyncScript
{
    public float PickupRadius { get; set; } = 1.5f;
    public InventoryProcessor? Processor { get; set; }
    public IEventBus? EventBus { get; set; }

    public override void Update()
    {
        if (Processor == null || Entity.Scene == null) return;

        var playerPos = Entity.Transform.Position;
        var toRemove = new List<Entity>();

        foreach (var entity in Entity.Scene.Entities)
        {
            if (!entity.Tags.TryGetValue(LootDropScript.LootItemsKey, out var items))
                continue;

            var dist = Vector3.Distance(playerPos, entity.Transform.Position);
            if (dist > PickupRadius) continue;

            // Pick up all items from this loot entity
            foreach (var item in items)
            {
                if (Processor.TryPickup(item))
                {
                    EventBus?.Publish(new NotificationEvent(
                        $"Picked up {item.Definition.Name}" +
                        (item.StackCount > 1 ? $" x{item.StackCount}" : ""),
                        3f));
                }
                // If inventory full, item stays — but for M0 we skip this edge case
            }

            toRemove.Add(entity);
        }

        foreach (var entity in toRemove)
            Entity.Scene.Entities.Remove(entity);
    }
}
```

---

## B5 — HudSyncScript

Renders HP bar, AP bar, and level text using Stride's UI system. The script creates a `UIComponent` with a simple layout.

### Design Rationale

Stride UI uses `UIPage` → `Grid`/`StackPanel` → visual elements. For M0 we keep it minimal: a fixed overlay with three elements at the screen top-left.

```csharp
namespace Oravey2.Core.UI.Stride;

using global::Stride.Engine;
using global::Stride.UI;
using global::Stride.UI.Controls;
using global::Stride.UI.Panels;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Combat;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Framework.State;

/// <summary>
/// Updates HUD text each frame from live game components.
/// Creates a simple Stride UI overlay on Start.
/// </summary>
public class HudSyncScript : SyncScript
{
    // --- Refs set from Program.cs ---
    public HealthComponent? Health { get; set; }
    public CombatComponent? Combat { get; set; }
    public LevelComponent? Level { get; set; }
    public GameStateManager? StateManager { get; set; }

    private TextBlock? _hpText;
    private TextBlock? _apText;
    private TextBlock? _levelText;
    private TextBlock? _stateText;

    public override void Start()
    {
        base.Start();

        _hpText = new TextBlock
        {
            Text = "HP: --/--",
            TextSize = 18,
            TextColor = global::Stride.Core.Mathematics.Color.White,
            Margin = new Thickness(10, 10, 0, 0)
        };

        _apText = new TextBlock
        {
            Text = "AP: --/--",
            TextSize = 18,
            TextColor = global::Stride.Core.Mathematics.Color.LightBlue,
            Margin = new Thickness(10, 0, 0, 0)
        };

        _levelText = new TextBlock
        {
            Text = "LVL: -",
            TextSize = 16,
            TextColor = global::Stride.Core.Mathematics.Color.LightGoldenrodYellow,
            Margin = new Thickness(10, 0, 0, 0)
        };

        _stateText = new TextBlock
        {
            Text = "Exploring",
            TextSize = 14,
            TextColor = global::Stride.Core.Mathematics.Color.LightGray,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { _hpText, _apText, _levelText, _stateText }
        };

        var page = new UIPage { RootElement = stack };
        Entity.Add(new UIComponent { Page = page });
    }

    public override void Update()
    {
        if (Health != null && _hpText != null)
            _hpText.Text = $"HP: {Health.CurrentHP}/{Health.MaxHP}";

        if (Combat != null && _apText != null)
            _apText.Text = $"AP: {Combat.CurrentAP:F0}/{Combat.MaxAP}";

        if (Level != null && _levelText != null)
            _levelText.Text = $"LVL: {Level.Level}  XP: {Level.CurrentXP}/{Level.XPToNextLevel}";

        if (StateManager != null && _stateText != null)
            _stateText.Text = StateManager.CurrentState.ToString();
    }
}
```

---

## B6 — InventoryOverlayScript

Tab key toggles an inventory list overlay. Uses `InventoryViewModel` for the data snapshot.

```csharp
namespace Oravey2.Core.UI.Stride;

using global::Stride.Engine;
using global::Stride.Input;
using global::Stride.UI;
using global::Stride.UI.Controls;
using global::Stride.UI.Panels;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.UI.ViewModels;

/// <summary>
/// Tab-toggled inventory overlay. Reads from InventoryComponent each time it opens.
/// M0: text-only list, no drag-drop or item interaction.
/// </summary>
public class InventoryOverlayScript : SyncScript
{
    public InventoryComponent? Inventory { get; set; }

    private UIComponent? _uiComponent;
    private StackPanel? _itemList;
    private TextBlock? _weightText;
    private bool _visible;

    public override void Start()
    {
        base.Start();
        BuildUI();
    }

    public override void Update()
    {
        // Toggle on Tab press
        if (Input.IsKeyPressed(Keys.Tab))
        {
            _visible = !_visible;
            if (_uiComponent != null)
            {
                if (_visible)
                {
                    RefreshInventory();
                    _uiComponent.Page!.RootElement.Visibility = Visibility.Visible;
                }
                else
                {
                    _uiComponent.Page!.RootElement.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    private void BuildUI()
    {
        _weightText = new TextBlock
        {
            Text = "Weight: 0/0",
            TextSize = 16,
            TextColor = global::Stride.Core.Mathematics.Color.White,
            Margin = new Thickness(10, 10, 10, 5)
        };

        _itemList = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10, 0, 10, 10)
        };

        var header = new TextBlock
        {
            Text = "=== INVENTORY ===",
            TextSize = 20,
            TextColor = global::Stride.Core.Mathematics.Color.Gold,
            Margin = new Thickness(10, 10, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            BackgroundColor = new global::Stride.Core.Mathematics.Color(0, 0, 0, 180),
            Width = 350,
            Children = { header, _weightText, _itemList },
            Visibility = Visibility.Collapsed
        };

        var page = new UIPage { RootElement = container };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);
    }

    private void RefreshInventory()
    {
        if (Inventory == null || _itemList == null || _weightText == null) return;

        var vm = InventoryViewModel.Create(Inventory);
        _weightText.Text = $"Weight: {vm.CurrentWeight:F1} / {vm.MaxCarryWeight:F0}" +
                           (vm.IsOverweight ? " [OVERWEIGHT]" : "");

        _itemList.Children.Clear();

        if (vm.Items.Count == 0)
        {
            _itemList.Children.Add(new TextBlock
            {
                Text = "(empty)",
                TextSize = 14,
                TextColor = global::Stride.Core.Mathematics.Color.Gray,
                Margin = new Thickness(5, 2, 0, 2)
            });
            return;
        }

        foreach (var item in vm.Items)
        {
            var countSuffix = item.StackCount > 1 ? $" x{item.StackCount}" : "";
            var durSuffix = item.CurrentDurability.HasValue
                ? $" [{item.CurrentDurability}/{item.MaxDurability}]"
                : "";
            var color = item.Category switch
            {
                Inventory.Items.ItemCategory.WeaponMelee or
                Inventory.Items.ItemCategory.WeaponRanged
                    => global::Stride.Core.Mathematics.Color.OrangeRed,
                Inventory.Items.ItemCategory.Armor
                    => global::Stride.Core.Mathematics.Color.SteelBlue,
                Inventory.Items.ItemCategory.Consumable
                    => global::Stride.Core.Mathematics.Color.LimeGreen,
                _ => global::Stride.Core.Mathematics.Color.LightGray
            };

            _itemList.Children.Add(new TextBlock
            {
                Text = $"{item.Name}{countSuffix}{durSuffix}  ({item.Weight:F1} kg)",
                TextSize = 14,
                TextColor = color,
                Margin = new Thickness(5, 2, 0, 2)
            });
        }
    }
}
```

---

## Program.cs Changes (B2–B6)

Full patch to program.cs. All additions are marked with `// Phase B` comments.

### After player combat data section:

```csharp
// --- Player inventory (Phase B) ---
var playerInventory = new InventoryComponent(playerStats);
var playerEquipment = new EquipmentComponent();
var inventoryProcessor = new InventoryProcessor(playerInventory, playerEquipment, eventBus);

// Starting equipment
var startingWeapon = new ItemInstance(M0Items.PipeWrench());
inventoryProcessor.TryPickup(startingWeapon);
inventoryProcessor.TryEquip(startingWeapon, EquipmentSlot.PrimaryWeapon);

// Starting consumable
inventoryProcessor.TryPickup(new ItemInstance(M0Items.Medkit(), 2));
```

### After combat manager setup, add loot system:

```csharp
// --- Loot system (Phase B) ---
var lootTable = new LootTable();
lootTable.Add(M0Items.ScrapMetal(), 0.7f);    // 70% chance
lootTable.Add(M0Items.Medkit(), 0.3f);         // 30% chance
lootTable.Add(M0Items.PipeWrench(), 0.15f);    // 15% chance
lootTable.Add(M0Items.LeatherJacket(), 0.1f);  // 10% chance

var lootDropScript = new LootDropScript { LootTable = lootTable };
combatManagerEntity.Add(lootDropScript);

// Wire loot drop to combat
combatScript.LootDrop = lootDropScript;

// Loot pickup on player entity
var lootPickup = new LootPickupScript
{
    Processor = inventoryProcessor,
    EventBus = eventBus,
    PickupRadius = 1.5f,
};
playerEntity.Add(lootPickup);
```

### After camera setup, add HUD + inventory overlay:

```csharp
// --- HUD (Phase B) ---
var hudEntity = new Entity("HUD");
var hudScript = new HudSyncScript
{
    Health = playerHealth,
    Combat = playerCombat,
    Level = playerLevel,
    StateManager = gameStateManager,
};
hudEntity.Add(hudScript);
rootScene.Entities.Add(hudEntity);

// --- Inventory overlay (Phase B) ---
var inventoryOverlayEntity = new Entity("InventoryOverlay");
var inventoryOverlay = new InventoryOverlayScript
{
    Inventory = playerInventory,
};
inventoryOverlayEntity.Add(inventoryOverlay);
rootScene.Entities.Add(inventoryOverlayEntity);
```

### New usings required in Program.cs:

```csharp
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Loot;
using Oravey2.Core.UI.Stride;
```

---

## Automation Queries (for UI Tests)

New queries to add to `OraveyAutomationHandler.cs`:

| Query | Args | Returns |
|-------|------|---------|
| `GetInventoryState` | — | `{ itemCount, currentWeight, maxWeight, isOverweight, items: [{id, name, count}] }` |
| `GetHudState` | — | `{ hp, maxHp, ap, maxAp, level, gameState }` |
| `GetLootEntities` | — | `{ count, positions: [{x, y, z, itemCount}] }` |

These queries will be fully specified in the Phase_B_Tests.md design document.

---

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| 1 | Player starts with a Pipe Wrench equipped and 2 Medkits |
| 2 | Killing an enemy spawns a yellow cube at their position |
| 3 | Walking over a loot cube picks up items and removes the cube |
| 4 | HUD displays HP, AP, level, and game state in top-left |
| 5 | Tab opens/closes an inventory list showing item names, counts, weights |
| 6 | `GetInventoryState` automation query returns correct data |
| 7 | All 635 unit tests still pass |
| 8 | All 49 active UI tests still pass |
