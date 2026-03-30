# Design: Step 05 — Dialogue & Quests

Implements branching dialogue system, data-driven quest engine, and world flags per [docs/steps/05-dialogue-quests.md](../steps/05-dialogue-quests.md). Data model follows [docs/schemas/dialogues.md](../schemas/dialogues.md) and [docs/schemas/quests.md](../schemas/quests.md).

**Depends on:** Step 1 (Framework, Events, IEventBus), Step 2 (SkillsComponent, InventoryComponent, LevelComponent, StatsComponent)

---

## File Layout

All new files go in `src/Oravey2.Core/`. Tests in `tests/Oravey2.Tests/`.

```
src/Oravey2.Core/
├── Dialogue/
│   ├── DialogueTree.cs           # DialogueNode + DialogueChoice + DialogueTree records
│   ├── DialogueContext.cs        # service bag for condition/consequence evaluation
│   ├── IDialogueCondition.cs    # interface + SkillCheckCondition, FlagCondition, ItemCondition, LevelCondition
│   ├── IConsequenceAction.cs    # interface + SetFlagAction, GiveXPAction, StartQuestAction
│   └── DialogueProcessor.cs     # conversation state machine
├── Quests/
│   ├── QuestDefinition.cs       # QuestStatus + QuestType enums + QuestStage + QuestDefinition records
│   ├── QuestContext.cs           # service bag for condition/action evaluation
│   ├── IQuestCondition.cs       # interface + HasItemCondition, QuestFlagCondition, QuestLevelCondition, QuestCompleteCondition
│   ├── IQuestAction.cs          # interface + QuestSetFlagAction, QuestGiveXPAction, UpdateJournalAction
│   ├── QuestLogComponent.cs     # tracks active/completed/failed quests + current stages
│   └── QuestProcessor.cs        # evaluates stage conditions, advances/completes/fails quests
├── World/
│   └── WorldStateService.cs     # global string→bool flag dictionary
├── Framework/
│   └── Events/
│       └── GameEvents.cs        # add 5 new events (existing file)
tests/Oravey2.Tests/
├── Dialogue/
│   ├── DialogueConditionTests.cs
│   ├── ConsequenceActionTests.cs
│   └── DialogueProcessorTests.cs
├── Quests/
│   ├── QuestConditionTests.cs
│   ├── QuestActionTests.cs
│   ├── QuestLogComponentTests.cs
│   └── QuestProcessorTests.cs
└── World/
    └── WorldStateServiceTests.cs
```

### Deferred to Stride Integration

| Deliverable | Reason |
|-------------|--------|
| `DialogueComponent` (EntityComponent) | Stride ECS attachment — wraps `DialogueTreeId` |
| `DialogueUI` | Stride UI framework — text display, choice buttons, skill check indicators |
| `GiveItemAction` / `RemoveItemAction` (consequence) | Require `ItemDefinition` lookup/registry not yet implemented |
| `ModifyFactionAction` (consequence) | Faction reputation system not yet implemented |
| `ModifyStatAction` (consequence) | Modifies base stats — needs design review on permanence |
| `FactionCondition` / `FactionRepCondition` | Faction system not yet implemented |
| `StatCheckCondition` (dialogue) | Deferred with stat modification |
| `SpawnEntityAction` (quest action) | Requires Stride prefab system |
| `TriggerEventAction` (quest action) | Generic event dispatch — Stride integration concern |
| `EntityDeadCondition` (quest) | Requires entity tracking system |
| `ZoneVisitedCondition` (quest) | Requires zone streaming system (Step 7) |
| `QuestGiveItemAction` / `QuestRemoveItemAction` | Require `ItemDefinition` lookup/registry |
| `QuestModifyFactionAction` | Faction system not yet implemented |
| `QuestProcessor` per-frame auto-evaluation | Requires Stride SyncScript tick loop |

The pure C# classes designed here use string IDs and component references instead of Stride Entity references — fully testable without the engine.

---

## World State

### WorldStateService.cs

Global flag dictionary shared by dialogue conditions, consequences, quest conditions, and quest actions. Single source of truth for "the world remembers" state.

```csharp
namespace Oravey2.Core.World;

public sealed class WorldStateService
{
    private readonly Dictionary<string, bool> _flags = new();

    public IReadOnlyDictionary<string, bool> Flags => _flags;

    public void SetFlag(string name, bool value) => _flags[name] = value;

    public bool GetFlag(string name)
        => _flags.TryGetValue(name, out var value) && value;
}
```

---

## Dialogue Data Model

### DialogueTree.cs

Three records representing the dialogue data model. Matches [docs/schemas/dialogues.md](../schemas/dialogues.md). Fields use interface types for conditions/consequences so different implementations can be mixed freely.

```csharp
namespace Oravey2.Core.Dialogue;

public sealed record DialogueNode(
    string Id,
    string Speaker,
    string Text,
    string? Portrait,
    DialogueChoice[] Choices);

public sealed record DialogueChoice(
    string Text,
    string? NextNodeId,
    IDialogueCondition? Condition,
    IConsequenceAction[] Consequences);

public sealed record DialogueTree(
    string Id,
    string StartNodeId,
    IReadOnlyDictionary<string, DialogueNode> Nodes);
```

---

## Dialogue Context

### DialogueContext.cs

Service bag passed to dialogue conditions and consequences. Bundles the player's components and shared services so each condition/action grabs only what it needs. Avoids scattering individual parameters across every method.

```csharp
namespace Oravey2.Core.Dialogue;

using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.World;

public sealed class DialogueContext
{
    public SkillsComponent Skills { get; }
    public InventoryComponent Inventory { get; }
    public WorldStateService WorldState { get; }
    public LevelComponent Level { get; }
    public IEventBus EventBus { get; }

    public DialogueContext(
        SkillsComponent skills,
        InventoryComponent inventory,
        WorldStateService worldState,
        LevelComponent level,
        IEventBus eventBus)
    {
        Skills = skills;
        Inventory = inventory;
        WorldState = worldState;
        Level = level;
        EventBus = eventBus;
    }
}
```

---

## Dialogue Conditions

### IDialogueCondition.cs

Interface plus all implemented condition types. Each evaluates against the `DialogueContext` and returns true/false.

```csharp
namespace Oravey2.Core.Dialogue;

using Oravey2.Core.Character.Skills;

public interface IDialogueCondition
{
    bool Evaluate(DialogueContext context);
}

/// <summary>
/// Passes if the player's effective skill ≥ threshold.
/// Hidden controls whether the UI displays the requirement.
/// </summary>
public sealed class SkillCheckCondition : IDialogueCondition
{
    public SkillType Skill { get; }
    public int Threshold { get; }
    public bool Hidden { get; }

    public SkillCheckCondition(SkillType skill, int threshold, bool hidden = false)
    {
        Skill = skill;
        Threshold = threshold;
        Hidden = hidden;
    }

    public bool Evaluate(DialogueContext context)
        => context.Skills.GetEffective(Skill) >= Threshold;
}

/// <summary>
/// Passes if a world flag matches the expected value.
/// </summary>
public sealed class FlagCondition : IDialogueCondition
{
    public string Flag { get; }
    public bool Expected { get; }

    public FlagCondition(string flag, bool expected = true)
    {
        Flag = flag;
        Expected = expected;
    }

    public bool Evaluate(DialogueContext context)
        => context.WorldState.GetFlag(Flag) == Expected;
}

/// <summary>
/// Passes if the player has at least count of the specified item.
/// </summary>
public sealed class ItemCondition : IDialogueCondition
{
    public string ItemId { get; }
    public int Count { get; }

    public ItemCondition(string itemId, int count = 1)
    {
        ItemId = itemId;
        Count = count;
    }

    public bool Evaluate(DialogueContext context)
        => context.Inventory.Contains(ItemId, Count);
}

/// <summary>
/// Passes if the player's level ≥ minLevel.
/// </summary>
public sealed class LevelCondition : IDialogueCondition
{
    public int MinLevel { get; }

    public LevelCondition(int minLevel)
    {
        MinLevel = minLevel;
    }

    public bool Evaluate(DialogueContext context)
        => context.Level.Level >= MinLevel;
}
```

---

## Dialogue Consequences

### IConsequenceAction.cs

Interface plus all implemented consequence types. Each mutates game state via the `DialogueContext`.

```csharp
namespace Oravey2.Core.Dialogue;

using Oravey2.Core.Framework.Events;

public interface IConsequenceAction
{
    void Execute(DialogueContext context);
}

/// <summary>
/// Sets a world state flag.
/// </summary>
public sealed class SetFlagAction : IConsequenceAction
{
    public string Flag { get; }
    public bool Value { get; }

    public SetFlagAction(string flag, bool value = true)
    {
        Flag = flag;
        Value = value;
    }

    public void Execute(DialogueContext context)
        => context.WorldState.SetFlag(Flag, Value);
}

/// <summary>
/// Grants XP to the player.
/// </summary>
public sealed class GiveXPAction : IConsequenceAction
{
    public int Amount { get; }

    public GiveXPAction(int amount)
    {
        Amount = amount;
    }

    public void Execute(DialogueContext context)
        => context.Level.GainXP(Amount);
}

/// <summary>
/// Publishes a QuestStartRequestedEvent so the quest system can start the quest.
/// Decoupled from QuestProcessor to avoid circular dependency.
/// </summary>
public sealed class StartQuestAction : IConsequenceAction
{
    public string QuestId { get; }

    public StartQuestAction(string questId)
    {
        QuestId = questId;
    }

    public void Execute(DialogueContext context)
        => context.EventBus.Publish(new QuestStartRequestedEvent(QuestId));
}
```

---

## Dialogue Processor

### DialogueProcessor.cs

Manages the active conversation. Tracks current tree and node. Evaluates conditions for choice availability, executes consequences on selection, and navigates the dialogue graph.

```csharp
namespace Oravey2.Core.Dialogue;

using Oravey2.Core.Framework.Events;

public sealed class DialogueProcessor
{
    private readonly IEventBus _eventBus;

    public DialogueTree? ActiveTree { get; private set; }
    public DialogueNode? CurrentNode { get; private set; }
    public bool IsActive => ActiveTree != null;

    public DialogueProcessor(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Begin a dialogue. Sets the current node to the tree's start node.
    /// </summary>
    public void StartDialogue(DialogueTree tree)
    {
        ActiveTree = tree;
        CurrentNode = tree.Nodes[tree.StartNodeId];
        _eventBus.Publish(new DialogueStartedEvent(tree.Id));
    }

    /// <summary>
    /// Returns all choices for the current node with availability flags.
    /// Unavailable choices (failed condition) are included so UI can show them grayed out.
    /// Returns empty if no active dialogue.
    /// </summary>
    public IReadOnlyList<(DialogueChoice Choice, bool Available)> GetAvailableChoices(
        DialogueContext context)
    {
        if (CurrentNode == null)
            return [];

        var result = new List<(DialogueChoice, bool)>();
        foreach (var choice in CurrentNode.Choices)
        {
            var available = choice.Condition?.Evaluate(context) ?? true;
            result.Add((choice, available));
        }
        return result;
    }

    /// <summary>
    /// Select a choice by index. Evaluates the condition, executes consequences,
    /// and advances to the next node. Returns false if invalid or unavailable.
    /// If NextNodeId is null, ends the dialogue.
    /// </summary>
    public bool SelectChoice(int index, DialogueContext context)
    {
        if (CurrentNode == null || index < 0 || index >= CurrentNode.Choices.Length)
            return false;

        var choice = CurrentNode.Choices[index];

        // Check condition
        if (choice.Condition != null && !choice.Condition.Evaluate(context))
            return false;

        // Execute consequences
        foreach (var consequence in choice.Consequences)
            consequence.Execute(context);

        // Navigate
        if (choice.NextNodeId == null)
        {
            EndDialogue();
        }
        else
        {
            CurrentNode = ActiveTree!.Nodes[choice.NextNodeId];
        }

        return true;
    }

    /// <summary>
    /// End the active dialogue. Clears state and publishes event.
    /// </summary>
    public void EndDialogue()
    {
        if (ActiveTree == null) return;

        var treeId = ActiveTree.Id;
        ActiveTree = null;
        CurrentNode = null;
        _eventBus.Publish(new DialogueEndedEvent(treeId));
    }
}
```

---

## Quest Data Model

### QuestDefinition.cs

Enums and records for the quest data model. Matches [docs/schemas/quests.md](../schemas/quests.md).

```csharp
namespace Oravey2.Core.Quests;

public enum QuestStatus
{
    NotStarted,
    Active,
    Completed,
    Failed
}

public enum QuestType
{
    Main,
    Faction,
    Side,
    Radiant
}

public sealed record QuestStage(
    string Id,
    string Description,
    IQuestCondition[] Conditions,
    IQuestAction[] OnCompleteActions,
    string? NextStageId,
    IQuestCondition[] FailConditions,
    IQuestAction[] OnFailActions);

public sealed record QuestDefinition(
    string Id,
    string Title,
    string Description,
    QuestType Type,
    string FirstStageId,
    IReadOnlyDictionary<string, QuestStage> Stages,
    int XPReward = 0);
```

---

## Quest Context

### QuestContext.cs

Service bag for quest condition evaluation and action execution. Includes `QuestLogComponent` so `QuestCompleteCondition` can check other quests.

```csharp
namespace Oravey2.Core.Quests;

using Oravey2.Core.Character.Level;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.World;

public sealed class QuestContext
{
    public InventoryComponent Inventory { get; }
    public WorldStateService WorldState { get; }
    public LevelComponent Level { get; }
    public QuestLogComponent QuestLog { get; }
    public IEventBus EventBus { get; }

    public QuestContext(
        InventoryComponent inventory,
        WorldStateService worldState,
        LevelComponent level,
        QuestLogComponent questLog,
        IEventBus eventBus)
    {
        Inventory = inventory;
        WorldState = worldState;
        Level = level;
        QuestLog = questLog;
        EventBus = eventBus;
    }
}
```

---

## Quest Conditions

### IQuestCondition.cs

Interface plus all implemented quest condition types. All conditions are `AND`-ed for stage completion. Fail conditions are `OR`-ed (any one triggers failure).

```csharp
namespace Oravey2.Core.Quests;

public interface IQuestCondition
{
    bool Evaluate(QuestContext context);
}

/// <summary>
/// Passes if the player has at least count of the specified item.
/// </summary>
public sealed class HasItemCondition : IQuestCondition
{
    public string ItemId { get; }
    public int Count { get; }

    public HasItemCondition(string itemId, int count = 1)
    {
        ItemId = itemId;
        Count = count;
    }

    public bool Evaluate(QuestContext context)
        => context.Inventory.Contains(ItemId, Count);
}

/// <summary>
/// Passes if a world flag matches the expected value.
/// </summary>
public sealed class QuestFlagCondition : IQuestCondition
{
    public string Flag { get; }
    public bool Expected { get; }

    public QuestFlagCondition(string flag, bool expected = true)
    {
        Flag = flag;
        Expected = expected;
    }

    public bool Evaluate(QuestContext context)
        => context.WorldState.GetFlag(Flag) == Expected;
}

/// <summary>
/// Passes if the player's level ≥ minLevel.
/// </summary>
public sealed class QuestLevelCondition : IQuestCondition
{
    public int MinLevel { get; }

    public QuestLevelCondition(int minLevel)
    {
        MinLevel = minLevel;
    }

    public bool Evaluate(QuestContext context)
        => context.Level.Level >= MinLevel;
}

/// <summary>
/// Passes if another quest has been completed.
/// </summary>
public sealed class QuestCompleteCondition : IQuestCondition
{
    public string QuestId { get; }

    public QuestCompleteCondition(string questId)
    {
        QuestId = questId;
    }

    public bool Evaluate(QuestContext context)
        => context.QuestLog.GetStatus(QuestId) == QuestStatus.Completed;
}
```

---

## Quest Actions

### IQuestAction.cs

Interface plus all implemented quest action types. Executed when a stage completes or fails.

```csharp
namespace Oravey2.Core.Quests;

using Oravey2.Core.Framework.Events;

public interface IQuestAction
{
    void Execute(QuestContext context);
}

/// <summary>
/// Sets a world state flag.
/// </summary>
public sealed class QuestSetFlagAction : IQuestAction
{
    public string Flag { get; }
    public bool Value { get; }

    public QuestSetFlagAction(string flag, bool value = true)
    {
        Flag = flag;
        Value = value;
    }

    public void Execute(QuestContext context)
        => context.WorldState.SetFlag(Flag, Value);
}

/// <summary>
/// Grants XP to the player.
/// </summary>
public sealed class QuestGiveXPAction : IQuestAction
{
    public int Amount { get; }

    public QuestGiveXPAction(int amount)
    {
        Amount = amount;
    }

    public void Execute(QuestContext context)
        => context.Level.GainXP(Amount);
}

/// <summary>
/// Publishes a JournalUpdatedEvent for the quest log UI.
/// </summary>
public sealed class UpdateJournalAction : IQuestAction
{
    public string QuestId { get; }
    public string Text { get; }

    public UpdateJournalAction(string questId, string text)
    {
        QuestId = questId;
        Text = text;
    }

    public void Execute(QuestContext context)
        => context.EventBus.Publish(new JournalUpdatedEvent(QuestId, Text));
}
```

---

## Quest Log

### QuestLogComponent.cs

Tracks quest progress: status per quest, current stage per quest. Pure C# class — Stride `EntityComponent` wrapper deferred.

```csharp
namespace Oravey2.Core.Quests;

public sealed class QuestLogComponent
{
    private readonly Dictionary<string, QuestStatus> _quests = new();
    private readonly Dictionary<string, string> _currentStages = new();

    public IReadOnlyDictionary<string, QuestStatus> Quests => _quests;
    public IReadOnlyDictionary<string, string> CurrentStages => _currentStages;

    public QuestStatus GetStatus(string questId)
        => _quests.TryGetValue(questId, out var status) ? status : QuestStatus.NotStarted;

    public string? GetCurrentStage(string questId)
        => _currentStages.TryGetValue(questId, out var stage) ? stage : null;

    public void StartQuest(string questId, string firstStageId)
    {
        if (_quests.ContainsKey(questId) && _quests[questId] != QuestStatus.NotStarted)
            return; // already started/completed/failed

        _quests[questId] = QuestStatus.Active;
        _currentStages[questId] = firstStageId;
    }

    public void AdvanceStage(string questId, string nextStageId)
    {
        if (GetStatus(questId) != QuestStatus.Active) return;
        _currentStages[questId] = nextStageId;
    }

    public void CompleteQuest(string questId)
    {
        _quests[questId] = QuestStatus.Completed;
        _currentStages.Remove(questId);
    }

    public void FailQuest(string questId)
    {
        _quests[questId] = QuestStatus.Failed;
        _currentStages.Remove(questId);
    }
}
```

---

## Quest Processor

### QuestProcessor.cs

Coordinates quest lifecycle. `StartQuest` activates a quest. `EvaluateQuest` checks the current stage's conditions and advances, completes, or fails the quest.

```csharp
namespace Oravey2.Core.Quests;

using Oravey2.Core.Framework.Events;

public sealed class QuestProcessor
{
    private readonly IEventBus _eventBus;

    public QuestProcessor(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Start a quest. Adds to the log and publishes QuestUpdatedEvent.
    /// No-op if the quest is already active/completed/failed.
    /// </summary>
    public void StartQuest(QuestLogComponent log, QuestDefinition quest)
    {
        if (log.GetStatus(quest.Id) != QuestStatus.NotStarted)
            return;

        log.StartQuest(quest.Id, quest.FirstStageId);
        _eventBus.Publish(new QuestUpdatedEvent(quest.Id, QuestStatus.Active));
    }

    /// <summary>
    /// Evaluate the current stage of a quest.
    /// Checks fail conditions first (OR — any true = fail).
    /// Then checks completion conditions (AND — all true = advance).
    /// Returns true if the quest state changed.
    /// </summary>
    public bool EvaluateQuest(QuestLogComponent log, QuestDefinition quest, QuestContext context)
    {
        if (log.GetStatus(quest.Id) != QuestStatus.Active)
            return false;

        var stageId = log.GetCurrentStage(quest.Id);
        if (stageId == null || !quest.Stages.TryGetValue(stageId, out var stage))
            return false;

        // Check fail conditions (OR — any true = quest fails)
        if (stage.FailConditions.Length > 0)
        {
            foreach (var failCondition in stage.FailConditions)
            {
                if (failCondition.Evaluate(context))
                {
                    // Execute on-fail actions
                    foreach (var action in stage.OnFailActions)
                        action.Execute(context);

                    log.FailQuest(quest.Id);
                    _eventBus.Publish(new QuestUpdatedEvent(quest.Id, QuestStatus.Failed));
                    return true;
                }
            }
        }

        // Check completion conditions (AND — all must be true)
        foreach (var condition in stage.Conditions)
        {
            if (!condition.Evaluate(context))
                return false;
        }

        // All conditions met — execute on-complete actions
        foreach (var action in stage.OnCompleteActions)
            action.Execute(context);

        _eventBus.Publish(new QuestStageCompletedEvent(quest.Id, stageId));

        // Advance or complete
        if (stage.NextStageId != null)
        {
            log.AdvanceStage(quest.Id, stage.NextStageId);
        }
        else
        {
            // Grant quest-level XP reward
            if (quest.XPReward > 0)
                context.Level.GainXP(quest.XPReward);

            log.CompleteQuest(quest.Id);
            _eventBus.Publish(new QuestUpdatedEvent(quest.Id, QuestStatus.Completed));
        }

        return true;
    }
}
```

---

## Events to Add

Add to `src/Oravey2.Core/Framework/Events/GameEvents.cs`:

```csharp
public readonly record struct DialogueStartedEvent(string TreeId) : IGameEvent;
public readonly record struct DialogueEndedEvent(string TreeId) : IGameEvent;
public readonly record struct QuestStartRequestedEvent(string QuestId) : IGameEvent;
public readonly record struct QuestUpdatedEvent(string QuestId, Quests.QuestStatus NewStatus) : IGameEvent;
public readonly record struct QuestStageCompletedEvent(string QuestId, string StageId) : IGameEvent;
public readonly record struct JournalUpdatedEvent(string QuestId, string Text) : IGameEvent;
```

**Required import** in `GameEvents.cs`:
```csharp
using Oravey2.Core.Quests;
```

---

## Interaction with Previous Steps

### Step 1: Framework

```
IEventBus ──▶ DialogueProcessor.StartDialogue/EndDialogue (publish events)
           ──▶ StartQuestAction (publishes QuestStartRequestedEvent)
           ──▶ QuestProcessor.StartQuest/EvaluateQuest (publish events)
           ──▶ UpdateJournalAction (publishes JournalUpdatedEvent)

GameStateManager ──▶ Stride integration transitions Exploring ↔ InDialogue
                     (deferred — DialogueProcessor is pure logic, no state transitions)
```

### Step 2: Character & Inventory

```
SkillsComponent.GetEffective() ──▶ SkillCheckCondition
InventoryComponent.Contains()  ──▶ ItemCondition, HasItemCondition
LevelComponent.Level           ──▶ LevelCondition, QuestLevelCondition
LevelComponent.GainXP()        ──▶ GiveXPAction, QuestGiveXPAction, QuestProcessor (XPReward)
```

### Event Flow: Dialogue → Quest

```
Player interacts with NPC
  │
  ▼
DialogueProcessor.StartDialogue(tree)
  ├─ publishes DialogueStartedEvent
  │
  ▼
GetAvailableChoices(context)
  ├─ SkillCheckCondition.Evaluate() → checks SkillsComponent
  ├─ FlagCondition.Evaluate()       → checks WorldStateService
  ├─ ItemCondition.Evaluate()       → checks InventoryComponent
  └─ returns (Choice, Available) list
  │
  ▼
SelectChoice(index, context)
  ├─ re-evaluates condition (guard against stale UI)
  ├─ executes consequences:
  │   ├─ SetFlagAction          → WorldStateService.SetFlag()
  │   ├─ GiveXPAction           → LevelComponent.GainXP()
  │   └─ StartQuestAction       → publishes QuestStartRequestedEvent
  └─ navigates to next node (or EndDialogue)
  │
  ▼
EndDialogue()
  └─ publishes DialogueEndedEvent
```

### Event Flow: Quest Evaluation

```
QuestProcessor.StartQuest(log, quest)
  ├─ QuestLogComponent.StartQuest(id, firstStageId)
  └─ publishes QuestUpdatedEvent(id, Active)
  │
  ▼
QuestProcessor.EvaluateQuest(log, quest, context)  [called periodically]
  ├─ check FailConditions (OR):
  │   └─ if any true → execute OnFailActions → FailQuest → QuestUpdatedEvent(Failed)
  ├─ check Conditions (AND):
  │   └─ if any false → return (no change)
  ├─ all conditions met:
  │   ├─ execute OnCompleteActions
  │   │   ├─ QuestSetFlagAction  → WorldStateService.SetFlag()
  │   │   ├─ QuestGiveXPAction   → LevelComponent.GainXP()
  │   │   └─ UpdateJournalAction → publishes JournalUpdatedEvent
  │   └─ publishes QuestStageCompletedEvent
  │
  ├─ if NextStageId != null:
  │   └─ QuestLogComponent.AdvanceStage()
  └─ if NextStageId == null:
      ├─ grant XPReward
      ├─ QuestLogComponent.CompleteQuest()
      └─ publishes QuestUpdatedEvent(Completed)
```

---

## Tests

### WorldStateServiceTests.cs

| Test | Assertion |
|------|-----------|
| `SetFlag_GetFlag_Roundtrip` | SetFlag("x", true) → GetFlag("x") == true |
| `GetFlag_Unknown_ReturnsFalse` | GetFlag("never_set") == false |
| `SetFlag_Overwrite` | Set true then false → GetFlag returns false |
| `SetFlag_FalseExplicitly` | SetFlag("x", false) → GetFlag("x") == false |
| `Flags_ReturnsAllEntries` | Set 3 flags → Flags.Count == 3 |
| `GetFlag_EmptyStore_ReturnsFalse` | Fresh service → all GetFlag == false |

### DialogueConditionTests.cs

| Test | Assertion |
|------|-----------|
| `SkillCheck_AboveThreshold_Passes` | Effective Speech ≥ 40 → true |
| `SkillCheck_BelowThreshold_Fails` | Effective Speech < 40 → false |
| `SkillCheck_ExactThreshold_Passes` | Effective == threshold → true |
| `SkillCheck_HiddenFlag_DoesNotAffectResult` | Hidden=true, same logic → same result |
| `FlagCondition_FlagSet_Passes` | Flag "met_merchant" set true → true |
| `FlagCondition_FlagNotSet_Fails` | Flag not set → false |
| `FlagCondition_ExpectedFalse_PassesWhenNotSet` | Expected=false, flag not set → true |
| `FlagCondition_ExpectedFalse_FailsWhenSet` | Expected=false, flag=true → false |
| `ItemCondition_HasEnough_Passes` | Inventory has 3 stimpaks, condition requires 2 → true |
| `ItemCondition_NotEnough_Fails` | Inventory has 1, requires 2 → false |
| `ItemCondition_ItemAbsent_Fails` | Item not in inventory → false |
| `LevelCondition_AboveMin_Passes` | Level 10, minLevel 5 → true |
| `LevelCondition_BelowMin_Fails` | Level 3, minLevel 5 → false |
| `LevelCondition_ExactMin_Passes` | Level 5, minLevel 5 → true |

### ConsequenceActionTests.cs

| Test | Assertion |
|------|-----------|
| `SetFlag_SetsWorldFlag` | Execute → WorldState.GetFlag("x") == true |
| `SetFlag_CanSetFalse` | SetFlagAction(flag, false) → GetFlag == false |
| `GiveXP_AddsXPToLevel` | Execute with Amount=100 → LevelComponent.CurrentXP increased |
| `StartQuest_PublishesQuestStartRequestedEvent` | Execute → event bus received QuestStartRequestedEvent |

### DialogueProcessorTests.cs

| Test | Assertion |
|------|-----------|
| `StartDialogue_SetsActiveTree` | After start → ActiveTree == tree |
| `StartDialogue_SetsCurrentNodeToStart` | After start → CurrentNode == startNode |
| `StartDialogue_PublishesDialogueStartedEvent` | EventBus received DialogueStartedEvent |
| `StartDialogue_IsActive_True` | After start → IsActive == true |
| `GetAvailableChoices_NoConditions_AllAvailable` | 3 unconditioned choices → all Available=true |
| `GetAvailableChoices_FailedCondition_MarkedUnavailable` | Skill check fails → Available=false |
| `GetAvailableChoices_PassedCondition_Available` | Skill check passes → Available=true |
| `GetAvailableChoices_NotActive_ReturnsEmpty` | Before StartDialogue → empty list |
| `SelectChoice_ValidIndex_AdvancesToNextNode` | Select choice 0 → CurrentNode is next |
| `SelectChoice_InvalidIndex_ReturnsFalse` | Index -1 or out of range → false |
| `SelectChoice_UnavailableCondition_ReturnsFalse` | Failed condition → false, node unchanged |
| `SelectChoice_ExecutesConsequences` | Choice has SetFlagAction → flag is set after selection |
| `SelectChoice_NullNextNode_EndsDialogue` | NextNodeId=null → IsActive=false |
| `SelectChoice_NotActive_ReturnsFalse` | No active dialogue → false |
| `EndDialogue_ClearsState` | After end → ActiveTree=null, CurrentNode=null, IsActive=false |
| `EndDialogue_PublishesDialogueEndedEvent` | EventBus received DialogueEndedEvent |
| `EndDialogue_WhenNotActive_NoEvent` | EndDialogue on inactive → no event published |

### QuestConditionTests.cs

| Test | Assertion |
|------|-----------|
| `HasItem_Present_Passes` | Inventory has "supply_crate" ×1 → true |
| `HasItem_Absent_Fails` | Item not in inventory → false |
| `HasItem_InsufficientCount_Fails` | Has 1, needs 3 → false |
| `QuestFlag_Set_Passes` | Flag "outpost_reached" set → true |
| `QuestFlag_NotSet_Fails` | Flag not set → false |
| `QuestFlag_ExpectedFalse_Passes` | expected=false, not set → true |
| `QuestLevel_Above_Passes` | Level 10, minLevel 5 → true |
| `QuestLevel_Below_Fails` | Level 3, minLevel 5 → false |
| `QuestLevel_Exact_Passes` | Level 5, minLevel 5 → true |
| `QuestComplete_Completed_Passes` | Other quest completed → true |
| `QuestComplete_Active_Fails` | Other quest still active → false |
| `QuestComplete_NotStarted_Fails` | Other quest not started → false |

### QuestActionTests.cs

| Test | Assertion |
|------|-----------|
| `SetFlag_SetsWorldFlag` | Execute → WorldState.GetFlag("flag") == true |
| `SetFlag_CanSetFalse` | value=false → GetFlag == false |
| `GiveXP_AddsXP` | Execute with 200 → LevelComponent.CurrentXP increased |
| `UpdateJournal_PublishesJournalUpdatedEvent` | Execute → EventBus received JournalUpdatedEvent with correct text |

### QuestLogComponentTests.cs

| Test | Assertion |
|------|-----------|
| `StartQuest_SetsActive` | StartQuest → GetStatus == Active |
| `StartQuest_SetsFirstStage` | StartQuest → GetCurrentStage == firstStageId |
| `StartQuest_AlreadyActive_NoOp` | Start twice → still Active, stage unchanged |
| `AdvanceStage_UpdatesStage` | AdvanceStage → GetCurrentStage == newStageId |
| `AdvanceStage_NotActive_NoOp` | AdvanceStage on NotStarted → no change |
| `CompleteQuest_SetsCompleted` | CompleteQuest → GetStatus == Completed |
| `CompleteQuest_RemovesCurrentStage` | CompleteQuest → GetCurrentStage == null |
| `FailQuest_SetsFailed` | FailQuest → GetStatus == Failed |
| `FailQuest_RemovesCurrentStage` | FailQuest → GetCurrentStage == null |
| `GetStatus_Unknown_ReturnsNotStarted` | Unknown quest → NotStarted |
| `GetCurrentStage_Unknown_ReturnsNull` | Unknown quest → null |

### QuestProcessorTests.cs

| Test | Assertion |
|------|-----------|
| `StartQuest_AddsToLog_PublishesEvent` | Start → log Active + QuestUpdatedEvent(Active) |
| `StartQuest_AlreadyActive_NoOp` | Start twice → only 1 event published |
| `StartQuest_Completed_NoOp` | Quest completed → start does nothing |
| `EvaluateQuest_NotActive_ReturnsFalse` | Quest NotStarted → false |
| `EvaluateQuest_ConditionsMet_AdvancesStage` | All conditions true → stage advances, returns true |
| `EvaluateQuest_ConditionsNotMet_ReturnsFalse` | A condition false → no change, false |
| `EvaluateQuest_LastStage_CompletesQuest` | NextStageId=null → Completed + QuestUpdatedEvent(Completed) |
| `EvaluateQuest_LastStage_GrantsXPReward` | XPReward=200 → LevelComponent.CurrentXP increased |
| `EvaluateQuest_ExecutesOnCompleteActions` | OnComplete has SetFlagAction → flag set after advance |
| `EvaluateQuest_PublishesStageCompletedEvent` | Stage completes → QuestStageCompletedEvent published |
| `EvaluateQuest_FailConditionMet_FailsQuest` | FailCondition true → Failed + QuestUpdatedEvent(Failed) |
| `EvaluateQuest_FailCondition_ExecutesOnFailActions` | OnFail has SetFlagAction → flag set |
| `EvaluateQuest_FailCondition_CheckedBeforeCompletion` | Both fail+complete conditions true → quest fails (fail checked first) |
| `EvaluateQuest_EmptyConditions_CompletesImmediately` | No conditions → stage completes |

---

## Execution Order

1. **`WorldStateService.cs`** + `WorldStateServiceTests.cs` — no deps
2. **Dialogue data:** `DialogueTree.cs` — records only, depends on interfaces from step 3–4
3. **`DialogueContext.cs`** — depends on Step 2 components + WorldStateService + IEventBus
4. **`IDialogueCondition.cs`** + `DialogueConditionTests.cs` — depends on DialogueContext
5. **`IConsequenceAction.cs`** + `ConsequenceActionTests.cs` — depends on DialogueContext
6. **`DialogueProcessor.cs`** + `DialogueProcessorTests.cs` — depends on all dialogue types
7. **Quest data:** `QuestDefinition.cs` — enums + records, depends on quest interfaces from step 8
8. **`QuestContext.cs`** — depends on Step 2 components + WorldStateService + QuestLogComponent
9. **`IQuestCondition.cs`** + `QuestConditionTests.cs` — depends on QuestContext
10. **`IQuestAction.cs`** + `QuestActionTests.cs` — depends on QuestContext
11. **`QuestLogComponent.cs`** + `QuestLogComponentTests.cs` — no deps beyond QuestStatus
12. **`QuestProcessor.cs`** + `QuestProcessorTests.cs` — depends on all quest types
13. **Events** in `GameEvents.cs` — add 6 new event records
14. **Run full test suite** — all unit + UI tests pass
