# Design: Step 08 — UI / UX

Implements the screen management framework, view-model layer for all game screens, and notification system per [docs/steps/08-ui-ux.md](../steps/08-ui-ux.md). HUD layout from [GAME_CONSTANTS.md](../constants/GAME_CONSTANTS.md) §10. Architecture from [CLASS_ARCHITECTURE.md](../CLASS_ARCHITECTURE.md) §11.

**Depends on:** Steps 1-7 (all game systems that feed UI data)

---

## Deferred to Stride Integration

The following require Stride UI runtime (`UIPage`, `UIComponent`, rendering) and are NOT implemented in this step:

- Actual XAML/code-behind screen classes (`HudScreen`, `InventoryScreen`, etc.)
- `UIInputRouter` — Stride input routing to topmost modal
- `TouchInputProvider`, `GamepadInputProvider` — platform-specific input
- UI scaling, DPI, safe area insets
- Screen transitions (fade, slide animations)
- Visual widgets (progress bars, tooltips, floating text rendering)

**What IS implemented:** Pure C# logic that the Stride screens will consume:

1. **ScreenManager** — modal stack, push/pop/replace with events
2. **Screen enum + metadata** — identifies each screen type
3. **View models** — read-only data snapshots the UI binds to, computed from game components
4. **NotificationService** — queues timed messages for the HUD to display
5. **QuickSlotBar** — 6-slot item assignment with use/cycle logic

---

## File Layout

All new files go in `src/Oravey2.Core/`. Tests in `tests/Oravey2.Tests/`.

```
src/Oravey2.Core/
├── UI/
│   ├── ScreenId.cs                  # enum — all screen types
│   ├── ScreenManager.cs             # push/pop/replace modal stack + events
│   ├── NotificationService.cs       # queued timed HUD messages
│   ├── QuickSlotBar.cs              # 6 quick-use item slots
│   ├── ViewModels/
│   │   ├── HudViewModel.cs          # HP, AP, time, zone, quest tracker snapshot
│   │   ├── CharacterViewModel.cs    # stats, skills, level, XP, perk points
│   │   ├── InventoryViewModel.cs    # items, weight, equipped slots
│   │   ├── QuestLogViewModel.cs     # active/completed/failed quests
│   │   └── MapViewModel.cs          # discovered locations, current zone, can-travel
├── Framework/
│   └── Events/
│       └── GameEvents.cs            # add new events (existing file)
tests/Oravey2.Tests/
├── UI/
│   ├── ScreenManagerTests.cs
│   ├── NotificationServiceTests.cs
│   ├── QuickSlotBarTests.cs
│   ├── HudViewModelTests.cs
│   ├── CharacterViewModelTests.cs
│   ├── InventoryViewModelTests.cs
│   ├── QuestLogViewModelTests.cs
│   └── MapViewModelTests.cs
```

**Source files:** 10 new + 1 modified (GameEvents.cs)
**Test files:** 8 new
**Estimated tests:** ~75

---

## Events to Add to GameEvents.cs

```csharp
// New events:
public readonly record struct ScreenPushedEvent(UI.ScreenId Screen) : IGameEvent;
public readonly record struct ScreenPoppedEvent(UI.ScreenId Screen) : IGameEvent;
public readonly record struct NotificationEvent(string Message, float DurationSeconds) : IGameEvent;
```

---

## Source Code

### 1. ScreenId.cs

```csharp
namespace Oravey2.Core.UI;

public enum ScreenId
{
    None,
    Hud,
    Inventory,
    Character,
    QuestLog,
    Crafting,
    Dialogue,
    Map,
    PauseMenu,
    Settings
}
```

### 2. ScreenManager.cs

```csharp
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.UI;

/// <summary>
/// Manages a modal screen stack. The topmost screen receives input.
/// Pure logic — Stride screens register themselves and render independently.
/// </summary>
public sealed class ScreenManager
{
    private readonly Stack<ScreenId> _stack = new();
    private readonly IEventBus _eventBus;

    public ScreenId ActiveScreen => _stack.Count > 0 ? _stack.Peek() : ScreenId.None;
    public int Count => _stack.Count;
    public IReadOnlyList<ScreenId> Stack => _stack.Reverse().ToList();

    public ScreenManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Pushes a screen onto the modal stack.
    /// </summary>
    public void Push(ScreenId screen)
    {
        if (screen == ScreenId.None) return;
        _stack.Push(screen);
        _eventBus.Publish(new ScreenPushedEvent(screen));
    }

    /// <summary>
    /// Pops the topmost screen. Returns the popped screen, or None if stack empty.
    /// </summary>
    public ScreenId Pop()
    {
        if (_stack.Count == 0) return ScreenId.None;
        var screen = _stack.Pop();
        _eventBus.Publish(new ScreenPoppedEvent(screen));
        return screen;
    }

    /// <summary>
    /// Replaces the topmost screen. If stack is empty, pushes instead.
    /// </summary>
    public void Replace(ScreenId screen)
    {
        if (screen == ScreenId.None) return;
        if (_stack.Count > 0)
        {
            var old = _stack.Pop();
            _eventBus.Publish(new ScreenPoppedEvent(old));
        }
        _stack.Push(screen);
        _eventBus.Publish(new ScreenPushedEvent(screen));
    }

    /// <summary>
    /// Clears all screens from the stack (e.g., returning to gameplay).
    /// Publishes pop events for each.
    /// </summary>
    public void Clear()
    {
        while (_stack.Count > 0)
            Pop();
    }

    /// <summary>
    /// Checks if a specific screen is anywhere in the stack.
    /// </summary>
    public bool Contains(ScreenId screen)
        => _stack.Contains(screen);
}
```

### 3. NotificationService.cs

```csharp
namespace Oravey2.Core.UI;

/// <summary>
/// Queues timed notifications for the HUD to display (e.g., "Item picked up", "Quest updated").
/// </summary>
public sealed class NotificationService
{
    public sealed record Notification(string Message, float DurationSeconds, float TimeRemaining);

    private readonly List<(string message, float duration, float remaining)> _active = new();
    private readonly Queue<(string message, float duration)> _pending = new();

    /// <summary>Maximum notifications displayed at once.</summary>
    public int MaxVisible { get; }

    public NotificationService(int maxVisible = 5)
    {
        MaxVisible = maxVisible;
    }

    /// <summary>
    /// Enqueues a notification. If there's room, it goes active immediately.
    /// </summary>
    public void Add(string message, float durationSeconds = 3f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (durationSeconds <= 0) durationSeconds = 3f;

        if (_active.Count < MaxVisible)
            _active.Add((message, durationSeconds, durationSeconds));
        else
            _pending.Enqueue((message, durationSeconds));
    }

    /// <summary>
    /// Ticks all active notifications, removing expired ones and promoting pending.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0) return;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var (msg, dur, rem) = _active[i];
            rem -= deltaSeconds;
            if (rem <= 0)
                _active.RemoveAt(i);
            else
                _active[i] = (msg, dur, rem);
        }

        // Promote pending
        while (_active.Count < MaxVisible && _pending.Count > 0)
        {
            var (msg, dur) = _pending.Dequeue();
            _active.Add((msg, dur, dur));
        }
    }

    /// <summary>
    /// Returns currently visible notifications as read-only snapshots.
    /// </summary>
    public IReadOnlyList<Notification> GetActive()
        => _active.Select(a => new Notification(a.message, a.duration, a.remaining)).ToList();

    /// <summary>Number of pending (not yet shown) notifications.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Removes all active and pending notifications.
    /// </summary>
    public void Clear()
    {
        _active.Clear();
        _pending.Clear();
    }
}
```

### 4. QuickSlotBar.cs

```csharp
namespace Oravey2.Core.UI;

/// <summary>
/// Manages 6 quick-use item slots. Each slot stores an item ID that the player
/// can activate. The Stride HUD renders these; this is the data/logic layer.
/// </summary>
public sealed class QuickSlotBar
{
    public const int SlotCount = 6;

    private readonly string?[] _slots = new string?[SlotCount];

    /// <summary>Gets the item ID assigned to a slot (0–5), or null if empty.</summary>
    public string? GetSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return null;
        return _slots[index];
    }

    /// <summary>Assigns an item ID to a slot. Pass null to clear.</summary>
    public void SetSlot(int index, string? itemId)
    {
        if (index < 0 || index >= SlotCount) return;
        _slots[index] = itemId;
    }

    /// <summary>Clears all slots.</summary>
    public void ClearAll()
    {
        for (int i = 0; i < SlotCount; i++)
            _slots[i] = null;
    }

    /// <summary>Finds the first slot containing the given item ID, or -1.</summary>
    public int FindSlot(string itemId)
    {
        for (int i = 0; i < SlotCount; i++)
            if (_slots[i] == itemId)
                return i;
        return -1;
    }

    /// <summary>Checks if any slot contains the given item ID.</summary>
    public bool Contains(string itemId)
        => FindSlot(itemId) >= 0;

    /// <summary>Returns a snapshot of all slots.</summary>
    public IReadOnlyList<string?> GetAllSlots()
        => _slots.ToArray();
}
```

### 5. HudViewModel.cs

```csharp
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Combat;
using Oravey2.Core.Quests;
using Oravey2.Core.Survival;
using Oravey2.Core.World;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Read-only snapshot of all data the HUD needs to display.
/// Computed from live game components — no mutable state.
/// </summary>
public sealed record HudViewModel(
    int CurrentHP,
    int MaxHP,
    float CurrentAP,
    int MaxAP,
    int Level,
    int CurrentXP,
    int XPToNextLevel,
    float InGameHour,
    DayPhase Phase,
    string? CurrentZoneName,
    float Hunger,
    float Thirst,
    float Fatigue,
    int RadiationLevel,
    bool SurvivalEnabled,
    IReadOnlyList<string?> QuickSlots
)
{
    /// <summary>
    /// Builds a HUD snapshot from live game components.
    /// </summary>
    public static HudViewModel Create(
        HealthComponent health,
        CombatComponent combat,
        LevelComponent level,
        DayNightCycleProcessor dayNight,
        string? currentZoneName,
        SurvivalComponent? survival,
        RadiationComponent? radiation,
        QuickSlotBar quickSlots)
    {
        return new HudViewModel(
            CurrentHP: health.CurrentHP,
            MaxHP: health.MaxHP,
            CurrentAP: combat.CurrentAP,
            MaxAP: combat.MaxAP,
            Level: level.Level,
            CurrentXP: level.CurrentXP,
            XPToNextLevel: level.XPToNextLevel,
            InGameHour: dayNight.InGameHour,
            Phase: dayNight.CurrentPhase,
            CurrentZoneName: currentZoneName,
            Hunger: survival?.Hunger ?? 0f,
            Thirst: survival?.Thirst ?? 0f,
            Fatigue: survival?.Fatigue ?? 0f,
            RadiationLevel: radiation?.Level ?? 0,
            SurvivalEnabled: survival?.Enabled ?? false,
            QuickSlots: quickSlots.GetAllSlots()
        );
    }
}
```

### 6. CharacterViewModel.cs

```csharp
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Snapshot of the character sheet: stats, skills, level info, available points.
/// </summary>
public sealed record CharacterViewModel(
    IReadOnlyDictionary<Stat, int> BaseStats,
    IReadOnlyDictionary<Stat, int> EffectiveStats,
    IReadOnlyDictionary<SkillType, int> EffectiveSkills,
    int Level,
    int CurrentXP,
    int XPToNextLevel,
    int StatPointsAvailable,
    int SkillPointsAvailable,
    int PerkPointsAvailable
)
{
    public static CharacterViewModel Create(
        StatsComponent stats,
        SkillsComponent skills,
        LevelComponent level)
    {
        var baseStats = new Dictionary<Stat, int>();
        var effectiveStats = new Dictionary<Stat, int>();
        foreach (Stat s in Enum.GetValues<Stat>())
        {
            baseStats[s] = stats.GetBase(s);
            effectiveStats[s] = stats.GetEffective(s);
        }

        var effectiveSkills = new Dictionary<SkillType, int>();
        foreach (SkillType sk in Enum.GetValues<SkillType>())
            effectiveSkills[sk] = skills.GetEffective(sk);

        return new CharacterViewModel(
            BaseStats: baseStats,
            EffectiveStats: effectiveStats,
            EffectiveSkills: effectiveSkills,
            Level: level.Level,
            CurrentXP: level.CurrentXP,
            XPToNextLevel: level.XPToNextLevel,
            StatPointsAvailable: level.StatPointsAvailable,
            SkillPointsAvailable: level.SkillPointsAvailable,
            PerkPointsAvailable: level.PerkPointsAvailable
        );
    }
}
```

### 7. InventoryViewModel.cs

```csharp
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Snapshot of inventory state for the inventory screen.
/// </summary>
public sealed record InventoryItemView(
    string ItemId,
    string Name,
    string Description,
    ItemCategory Category,
    int StackCount,
    float Weight,
    int? CurrentDurability,
    int? MaxDurability
);

public sealed record InventoryViewModel(
    IReadOnlyList<InventoryItemView> Items,
    float CurrentWeight,
    float MaxCarryWeight,
    bool IsOverweight
)
{
    public static InventoryViewModel Create(InventoryComponent inventory)
    {
        var items = inventory.Items.Select(item => new InventoryItemView(
            ItemId: item.Definition.Id,
            Name: item.Definition.Name,
            Description: item.Definition.Description,
            Category: item.Definition.Category,
            StackCount: item.StackCount,
            Weight: item.TotalWeight,
            CurrentDurability: item.CurrentDurability,
            MaxDurability: item.Definition.Durability?.MaxDurability
        )).ToList();

        return new InventoryViewModel(
            Items: items,
            CurrentWeight: inventory.CurrentWeight,
            MaxCarryWeight: inventory.MaxCarryWeight,
            IsOverweight: inventory.IsOverweight
        );
    }
}
```

### 8. QuestLogViewModel.cs

```csharp
using Oravey2.Core.Quests;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Snapshot of quest log for the quest screen. Separates quests by status.
/// </summary>
public sealed record QuestEntry(
    string QuestId,
    QuestStatus Status,
    string? CurrentStageId
);

public sealed record QuestLogViewModel(
    IReadOnlyList<QuestEntry> Active,
    IReadOnlyList<QuestEntry> Completed,
    IReadOnlyList<QuestEntry> Failed
)
{
    public static QuestLogViewModel Create(QuestLogComponent questLog)
    {
        var active = new List<QuestEntry>();
        var completed = new List<QuestEntry>();
        var failed = new List<QuestEntry>();

        foreach (var (questId, status) in questLog.Quests)
        {
            var entry = new QuestEntry(questId, status, questLog.GetCurrentStage(questId));
            switch (status)
            {
                case QuestStatus.Active:
                    active.Add(entry);
                    break;
                case QuestStatus.Completed:
                    completed.Add(entry);
                    break;
                case QuestStatus.Failed:
                    failed.Add(entry);
                    break;
            }
        }

        return new QuestLogViewModel(active, completed, failed);
    }
}
```

### 9. MapViewModel.cs

```csharp
using Oravey2.Core.World;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Snapshot of map data for the world map screen: discovered locations and fast-travel state.
/// </summary>
public sealed record MapLocationView(
    string Id,
    string Name,
    int ChunkX,
    int ChunkY,
    bool CanTravelTo
);

public sealed record MapViewModel(
    IReadOnlyList<MapLocationView> Locations,
    string? CurrentLocationId,
    float InGameHour,
    DayPhase Phase
)
{
    /// <summary>
    /// Builds map view from fast-travel service and day/night state.
    /// </summary>
    public static MapViewModel Create(
        FastTravelService fastTravel,
        DayNightCycleProcessor dayNight,
        string? currentLocationId)
    {
        var locations = fastTravel.Locations.Select(loc => new MapLocationView(
            Id: loc.Id,
            Name: loc.Name,
            ChunkX: loc.ChunkX,
            ChunkY: loc.ChunkY,
            CanTravelTo: currentLocationId != null && fastTravel.CanTravel(currentLocationId, loc.Id)
        )).ToList();

        return new MapViewModel(
            Locations: locations,
            CurrentLocationId: currentLocationId,
            InGameHour: dayNight.InGameHour,
            Phase: dayNight.CurrentPhase
        );
    }
}
```

---

## Test Tables

### ScreenManagerTests.cs — 10 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Push_SetsActiveScreen` | Push(Inventory) | `ActiveScreen == Inventory` |
| 2 | `Push_None_Ignored` | Push(None) | `Count == 0` |
| 3 | `Pop_ReturnsTopmost` | Push(Inventory), Pop() | returns Inventory, `ActiveScreen == None` |
| 4 | `Pop_EmptyStack_ReturnsNone` | — | `Pop() == None` |
| 5 | `Replace_SwapsTopmost` | Push(Inventory), Replace(Character) | `ActiveScreen == Character`, `Count == 1` |
| 6 | `Replace_EmptyStack_Pushes` | Replace(Map) | `ActiveScreen == Map`, `Count == 1` |
| 7 | `Clear_PopsAll` | Push 3 screens | `Count == 0`, 3 pop events |
| 8 | `Contains_Found` | Push(Inventory), Push(Character) | `Contains(Inventory) == true` |
| 9 | `Push_PublishesPushedEvent` | Subscribe ScreenPushedEvent | event.Screen == pushed screen |
| 10 | `Pop_PublishesPoppedEvent` | Subscribe ScreenPoppedEvent | event.Screen == popped screen |

### NotificationServiceTests.cs — 9 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Add_AppearsInActive` | Add("hello") | `GetActive().Count == 1` |
| 2 | `Add_EmptyMessage_Ignored` | Add("") | `GetActive().Count == 0` |
| 3 | `Update_ExpiresNotification` | Add("x", 1.0), Update(2.0) | `GetActive().Count == 0` |
| 4 | `Update_DecrementsTimeRemaining` | Add("x", 5.0), Update(2.0) | `remaining ≈ 3.0` |
| 5 | `MaxVisible_ExcessQueued` | maxVisible=2, add 3 | `GetActive().Count == 2, PendingCount == 1` |
| 6 | `Update_PromotesPending` | maxVisible=1, add 2, expire first | second promotes to active |
| 7 | `Clear_RemovesAll` | Add 3, Clear() | `GetActive().Count == 0, PendingCount == 0` |
| 8 | `Add_NegativeDuration_DefaultsTo3` | Add("x", -1) | `duration == 3.0` |
| 9 | `Update_ZeroDelta_NoChange` | Add("x", 1.0), Update(0) | `remaining == 1.0` |

### QuickSlotBarTests.cs — 8 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Default_AllSlotsNull` | new bar | all 6 slots null |
| 2 | `SetSlot_GetSlot_RoundTrip` | SetSlot(0, "stimpak") | `GetSlot(0) == "stimpak"` |
| 3 | `SetSlot_OutOfRange_Ignored` | SetSlot(10, "x") | no throw, GetSlot(10) == null |
| 4 | `SetSlot_Null_ClearsSlot` | Set then clear | `GetSlot(0) == null` |
| 5 | `ClearAll_ResetsAllSlots` | Set slots 0,1,2, ClearAll | all null |
| 6 | `FindSlot_Found` | SetSlot(3, "stimpak") | `FindSlot("stimpak") == 3` |
| 7 | `FindSlot_NotFound_MinusOne` | — | `FindSlot("nope") == -1` |
| 8 | `Contains_TrueWhenAssigned` | SetSlot(0, "x") | `Contains("x") == true` |

### HudViewModelTests.cs — 6 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Create_MapsHP` | health.CurrentHP=80, MaxHP=105 | `vm.CurrentHP==80, vm.MaxHP==105` |
| 2 | `Create_MapsAP` | combat.MaxAP=10, CurrentAP=7 | `vm.MaxAP==10, vm.CurrentAP==7` |
| 3 | `Create_MapsLevel` | level.Level=5 | `vm.Level==5` |
| 4 | `Create_MapsDayNight` | dayNight at hour 8 | `vm.InGameHour==8, vm.Phase==Day` |
| 5 | `Create_MapsZoneName` | zone="Haven" | `vm.CurrentZoneName=="Haven"` |
| 6 | `Create_NullSurvival_Defaults` | survival=null, radiation=null | `Hunger==0, RadiationLevel==0, SurvivalEnabled==false` |

### CharacterViewModelTests.cs — 5 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Create_MapsBaseStats` | stats default (all 5) | `BaseStats[Strength] == 5` |
| 2 | `Create_MapsEffectiveWithModifier` | Add +2 Str modifier | `EffectiveStats[Strength] == 7` |
| 3 | `Create_MapsSkills` | default skills | `EffectiveSkills[Firearms] == 20` |
| 4 | `Create_MapsLevel` | level 1 | `Level==1, CurrentXP==0` |
| 5 | `Create_MapsAvailablePoints` | level=1 | `StatPointsAvailable==0` |

### InventoryViewModelTests.cs — 5 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Create_MapsItems` | Add 2 items | `Items.Count == 2` |
| 2 | `Create_MapsWeight` | Add weighted item | `CurrentWeight > 0` |
| 3 | `Create_MapsOverweight` | Exceed carry weight | `IsOverweight == true` |
| 4 | `Create_ItemView_HasDurability` | Add durable item at 50 | `CurrentDurability==50, MaxDurability==100` |
| 5 | `Create_ItemView_NoDurability_Null` | Add non-durable item | `CurrentDurability == null` |

### QuestLogViewModelTests.cs — 5 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Create_SeparatesActive` | Start 2 quests | `Active.Count == 2` |
| 2 | `Create_SeparatesCompleted` | Start + Complete 1 | `Completed.Count == 1` |
| 3 | `Create_SeparatesFailed` | Start + Fail 1 | `Failed.Count == 1` |
| 4 | `Create_IncludesCurrentStage` | Start with stage "s1" | `Active[0].CurrentStageId == "s1"` |
| 5 | `Create_EmptyLog_AllEmpty` | Empty quest log | all lists empty |

### MapViewModelTests.cs — 6 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Create_MapsLocations` | Discover 2 locations | `Locations.Count == 2` |
| 2 | `Create_CanTravelTo_BothDiscovered` | Discover A, B; current=A | B.CanTravelTo == true |
| 3 | `Create_CanTravelTo_Self_False` | Discover A; current=A | A.CanTravelTo == false |
| 4 | `Create_NullCurrentLocation_AllFalse` | Discover A; current=null | all CanTravelTo false |
| 5 | `Create_MapsTime` | dayNight at 14.0 | `InGameHour==14, Phase==Day` |
| 6 | `Create_MapsCurrentLocationId` | current="haven" | `CurrentLocationId=="haven"` |

---

## Execution Order

1. **Create enum:** `ScreenId.cs`
2. **Create core classes:** `ScreenManager.cs`, `NotificationService.cs`, `QuickSlotBar.cs`
3. **Create view models:** `HudViewModel.cs`, `CharacterViewModel.cs`, `InventoryViewModel.cs`, `QuestLogViewModel.cs`, `MapViewModel.cs`
4. **Modify GameEvents.cs** — add 3 events
5. **Build Core** — verify 0 errors
6. **Create tests:** all 8 test files
7. **Run full test suite** — verify all pass (~537 total)
