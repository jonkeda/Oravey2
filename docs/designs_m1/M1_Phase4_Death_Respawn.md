# M1 Phase 4 — Death & Respawn

**Goal:** When the player dies, respawn them at the town with a caps penalty and full HP. When all 3 quests are complete, show a victory screen.

**Depends on:** Phase 3 (combat zone, quests, kill tracking)

---

## 1. Death & Respawn Flow

### 1.1 Current Behavior (M0)

Player death → `GameOverOverlayScript` shows "GAME OVER" → input frozen → game stuck (no recovery).

### 1.2 New Behavior (M1)

```
Player HP hits 0
  → GameState = GameOver
  → Show death overlay: "YOU DIED" (2 second delay)
  → Apply death penalty (lose 10% caps)
  → After 2 seconds, show "Respawning..." text
  → After 3 seconds total:
      → Heal player to max HP
      → Reset combat state
      → Load town zone
      → Place player at town spawn point
      → GameState = Exploring
      → Show notification: "You wake up in Haven. Lost X caps."
```

### 1.3 DeathRespawnScript (SyncScript)

**File:** `src/Oravey2.Core/Combat/DeathRespawnScript.cs`

Replaces the permanent game-over freeze with a timed respawn sequence.

```csharp
public class DeathRespawnScript : SyncScript
{
    public float RespawnDelay { get; set; } = 3.0f;
    public string RespawnZoneId { get; set; } = "town";
    public Vector3 RespawnPosition { get; set; } = new(0, 0.5f, 0);

    // Dependencies (set via Program.cs)
    public HealthComponent? PlayerHealth { get; set; }
    public InventoryComponent? PlayerInventory { get; set; }
    public GameStateManager? GameStateManager { get; set; }
    public GameOverOverlayScript? DeathOverlay { get; set; }
    public ZoneManager? ZoneManager { get; set; }
    public NotificationService? Notifications { get; set; }
}
```

**State machine:**

| State | Duration | Action |
|-------|----------|--------|
| Idle | — | Wait for `GameState.GameOver` |
| ShowDeath | 0–2s | Display "YOU DIED" overlay, freeze input |
| Respawning | 2–3s | Display "Respawning..." text, calculate penalty |
| Complete | 3s+ | Execute respawn, transition to Exploring |

### 1.4 Modifications to GameOverOverlayScript

- Add `SetTitle(string)` and `SetSubtitle(string)` methods
- Death: Title = "YOU DIED", Subtitle = "" → then "Respawning..."
- Victory: Title = "HAVEN IS SAFE", Subtitle = "Quest chain complete"
- Death overlay auto-closes after respawn (controlled by DeathRespawnScript)

---

## 2. Death Penalty

### 2.1 Caps Penalty

```csharp
public static class DeathPenalty
{
    public const float CapsLossPercent = 0.10f; // 10%

    public static int CalculateCapsLoss(int currentCaps)
        => (int)(currentCaps * CapsLossPercent); // Floor, min 0
}
```

**Examples:**
- 50 caps → lose 5, respawn with 45
- 3 caps → lose 0, respawn with 3
- 0 caps → lose 0, respawn with 0

### 2.2 What Is NOT Lost on Death

| State | Preserved? | Notes |
|-------|-----------|-------|
| Inventory items | ✅ Yes | Keep all items |
| Equipment | ✅ Yes | Keep equipped gear |
| Quest progress | ✅ Yes | Flags/counters preserved |
| World flags | ✅ Yes | Killed bosses stay dead |
| XP / Level | ✅ Yes | No XP loss |
| Caps (partial) | ⚠️ 90% | Lose 10% on death |
| Current zone | ❌ No | Always respawn in town |
| Combat state | ❌ No | Reset, enemies respawn fresh |

---

## 3. Victory Screen

### 3.1 Trigger

When `WorldStateService.GetFlag("m1_complete") == true`:
- After Quest 3's `SetFlag` action fires
- `QuestProcessor` publishes `QuestCompletedEvent` for `q_safe_passage`
- `VictoryCheckScript` listens for this specific event

### 3.2 VictoryCheckScript (SyncScript)

**File:** `src/Oravey2.Core/UI/Stride/VictoryCheckScript.cs`

```csharp
public class VictoryCheckScript : SyncScript
{
    public GameOverOverlayScript? Overlay { get; set; }
    public WorldStateService? WorldState { get; set; }
    public GameStateManager? GameStateManager { get; set; }

    // Check after each quest completion event
    public void CheckVictory()
    {
        if (WorldState?.GetFlag("m1_complete") == true)
        {
            Overlay?.Show("HAVEN IS SAFE", "You completed all quests.");
            // Don't freeze — show for 5 seconds then return to exploring
            // Player can continue playing (free roam)
        }
    }
}
```

### 3.3 Victory Flow

```
Quest 3 completes
  → SetFlag: m1_complete = true
  → VictoryCheckScript.CheckVictory()
  → Show overlay: "HAVEN IS SAFE" / "You completed all quests."
  → After 5 seconds: overlay fades
  → Player remains in Exploring state (free roam continues)
  → Start menu shows "Victory" badge on Continue button
```

**Design choice:** Victory doesn't end the game. Player can keep exploring, trading, and fighting. The victory screen is a milestone acknowledgment, not a game-ending screen.

---

## 4. Respawn Integration with Zone System

### 4.1 Zone Reset on Respawn

When respawning:
1. Current zone is unloaded (all entities removed)
2. Town zone is loaded fresh
3. Player placed at spawn point
4. Town NPCs respawned
5. Combat state fully reset

### 4.2 Wasteland State After Death

When the player re-enters the wasteland after dying:
- Regular enemies respawn (same as normal zone re-entry)
- Boss enemies do NOT respawn if their kill flag is set
- Quest counters are preserved (no lost progress)

---

## 5. Auto-Save on Death Prevention

- Do NOT auto-save when `GameState == GameOver`
- Auto-save is paused during the death/respawn sequence
- Next auto-save triggers after respawn, once in `Exploring` state

---

## 6. Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `src/Oravey2.Core/Combat/DeathRespawnScript.cs` | Timed respawn sequence |
| Create | `src/Oravey2.Core/Combat/DeathPenalty.cs` | Caps loss calculation |
| Create | `src/Oravey2.Core/UI/Stride/VictoryCheckScript.cs` | M1 victory detection |
| Modify | `src/Oravey2.Core/UI/Stride/GameOverOverlayScript.cs` | Add subtitle, auto-close |
| Modify | `src/Oravey2.Core/Save/AutoSaveTracker.cs` | Skip during GameOver |
| Modify | `src/Oravey2.Windows/Program.cs` | Wire DeathRespawnScript, VictoryCheckScript |

---

## 7. Automation Queries

| Query | Response | Purpose |
|-------|----------|---------|
| `GetDeathState` | `{ isDead, respawnTimer, capsLost }` | Verify death sequence |
| `ForcePlayerDeath` | `{ success }` | Trigger death for testing |
| `GetVictoryState` | `{ achieved, flag }` | Check victory condition |

---

## 8. Test Plan

### Unit Tests (target: +12)

| Test | Validates |
|------|-----------|
| `DeathPenalty_10Percent_Of50` | 50 → lose 5 |
| `DeathPenalty_10Percent_Of100` | 100 → lose 10 |
| `DeathPenalty_MinZero_SmallAmount` | 3 → lose 0 |
| `DeathPenalty_Zero_LosesNothing` | 0 → lose 0 |
| `DeathPenalty_LargeAmount` | 1000 → lose 100 |
| `DeathRespawn_HealsToMax` | HP = MaxHP after respawn |
| `DeathRespawn_ResetsZoneToTown` | CurrentZone = "town" |
| `DeathRespawn_PreservesQuestFlags` | Flags unchanged |
| `DeathRespawn_PreservesInventory` | Item count unchanged |
| `VictoryCheck_FlagFalse_NoVictory` | m1_complete = false → noop |
| `VictoryCheck_FlagTrue_Triggers` | m1_complete = true → victory |
| `AutoSave_SkipsDuringGameOver` | ShouldSave = false during death |

### UI Tests (target: +12)

| Test | Validates |
|------|-----------|
| `PlayerDeath_ShowsYouDied` | Death overlay visible |
| `PlayerDeath_RespawnsAfterDelay` | Player in town after 3s |
| `PlayerDeath_CapsReduced` | 50 → 45 caps |
| `PlayerDeath_HpFull_AfterRespawn` | HP = MaxHP |
| `PlayerDeath_QuestPreserved` | Active quest still active |
| `PlayerDeath_InventoryPreserved` | Items unchanged |
| `PlayerDeath_InTownZone` | CurrentZone = "town" |
| `PlayerDeath_CombatReset` | InCombat = false |
| `PlayerDeath_CanReenterWasteland` | Zone transition works after death |
| `Victory_ShowsOverlay` | "HAVEN IS SAFE" displayed |
| `Victory_DoesNotEndGame` | Player can still move after |
| `Victory_AllQuestsComplete` | Quest log shows 3 completed |

---

## 9. Acceptance Criteria

Phase 4 is complete when:

1. Player death triggers "YOU DIED" overlay (not permanent freeze)
2. After 3 seconds, player respawns at town spawn point
3. Player HP is restored to max on respawn
4. 10% of caps are lost (rounded down, min 0)
5. Inventory, equipment, quest state, and world flags preserved
6. Combat state fully reset after respawn
7. Re-entering wasteland respawns regular enemies but not killed bosses
8. Completing all 3 quests shows "HAVEN IS SAFE" victory screen
9. Victory screen fades after 5 seconds — player can continue playing
10. Auto-save does not trigger during death/respawn sequence
11. Death notification shows how many caps were lost
12. All M0 + Phase 1-3 tests still pass

---

## 10. M1 Complete Checklist

When Phase 4 passes, verify the full M1 acceptance criteria from [M1_PLAN.md](M1_PLAN.md):

- [ ] Start menu with New Game / Continue / Quit
- [ ] New Game → spawn in town with NPCs
- [ ] Talk to quest giver → dialogue UI with choices
- [ ] Accept quest → quest objective on HUD
- [ ] Travel to wasteland → enemies present
- [ ] Combat works with quest-relevant enemies
- [ ] Quest objectives update on kills
- [ ] Return to town, complete quest chain
- [ ] Save/load preserves all state
- [ ] Death → respawn at town with penalty
- [ ] All 3 quests → victory screen
- [ ] 700+ unit tests, 160+ UI tests
- [ ] `dotnet build` 0 errors
