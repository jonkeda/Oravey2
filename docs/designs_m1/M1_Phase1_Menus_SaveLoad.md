# M1 Phase 1 — Menus & Save/Load

**Goal:** Start menu, pause menu, settings screen, and a working save/load pipeline.

**Depends on:** M0 complete

---

## 1. Start Menu Screen

### 1.1 StartMenuScript (SyncScript)

**File:** `src/Oravey2.Core/UI/Stride/StartMenuScript.cs`

A full-screen overlay rendered before the game world loads. Uses Stride's UI system (TextBlock + Button).

**Layout:**

```
┌──────────────────────────────┐
│                              │
│         ORAVEY 2             │
│    Post-Apocalyptic RPG      │
│                              │
│      [ New Game ]            │
│      [ Continue ]  (grayed   │
│                    if no     │
│                    save)     │
│      [ Settings ]            │
│      [ Quit ]                │
│                              │
└──────────────────────────────┘
```

**Behavior:**

| Button | Action |
|--------|--------|
| New Game | Transition to `GameState.Loading` → init fresh world → `GameState.Exploring` |
| Continue | Load save file → restore state → `GameState.Exploring` |
| Settings | Push `ScreenId.Settings` |
| Quit | `Environment.Exit(0)` |

**State:** `GameState.InMenu` while start menu is visible. Game world is **not** loaded until New Game or Continue.

### 1.2 Implementation Notes

- `GameStateManager` starts in `InMenu` instead of current `Exploring`
- "Continue" button enabled only if save file exists on disk
- Start menu is the **first thing shown** — game loop doesn't tick world scripts until `Exploring`
- Camera renders a static background (dark scene or title card)

---

## 2. Pause Menu

### 2.1 PauseMenuScript (SyncScript)

**File:** `src/Oravey2.Core/UI/Stride/PauseMenuScript.cs`

Triggered by `Escape` key during `Exploring` or `InCombat` states.

**Layout:**

```
┌──────────────────────────────┐
│         PAUSED               │
│                              │
│      [ Resume ]              │
│      [ Save Game ]           │
│      [ Settings ]            │
│      [ Quit to Menu ]        │
│                              │
└──────────────────────────────┘
```

**Behavior:**

| Button | Action |
|--------|--------|
| Resume | Pop screen, return to previous GameState |
| Save Game | Trigger save pipeline → show "Saved!" notification |
| Settings | Push `ScreenId.Settings` |
| Quit to Menu | Save (optional prompt) → unload world → show start menu |

**State transitions:**
- `Exploring` → Escape → `Paused` (push PauseMenu)
- `InCombat` → Escape → `Paused` (pause combat tick)
- `Paused` → Resume → restore previous state

### 2.2 Input Handling

- `GameAction.Pause` mapped to `Keys.Escape`
- Handled in a new `PauseInputScript` that checks `_inputProvider.IsActionPressed(GameAction.Pause)`
- Only fires when `GameState` is `Exploring` or `InCombat`

---

## 3. Settings Screen

### 3.1 SettingsMenuScript (SyncScript)

**File:** `src/Oravey2.Core/UI/Stride/SettingsMenuScript.cs`

Accessible from both start menu and pause menu.

**Settings (M1 scope):**

| Setting | Type | Default | Persisted |
|---------|------|---------|-----------|
| Master Volume | Slider 0–100 | 80 | Yes (settings.json) |
| Music Volume | Slider 0–100 | 60 | Yes |
| SFX Volume | Slider 0–100 | 80 | Yes |
| Auto-Save Enabled | Toggle | On | Yes |

**Implementation:**
- Uses existing `VolumeSettings` from `Oravey2.Core.Audio`
- Settings persisted to `settings.json` in app data folder (separate from save files)
- Back button pops screen stack

---

## 4. Save/Load Pipeline

### 4.1 SaveService

**File:** `src/Oravey2.Core/Save/SaveService.cs` (new)

Orchestrates the save/load pipeline. Uses existing `SaveDataBuilder` and `SaveDataRestorer`.

```csharp
public class SaveService
{
    // Save current game state to disk
    public void SaveGame(
        StatsComponent stats,
        SkillsComponent skills,
        HealthComponent health,
        LevelComponent level,
        PerkTreeComponent perks,
        InventoryComponent inventory,
        EquipmentComponent equipment,
        QuestLogComponent questLog,
        WorldStateService worldState,
        DayNightCycleProcessor dayNight,
        Vector3 playerPosition);

    // Load game state from disk, returns restorer
    public SaveDataRestorer? LoadGame();

    // Check if a save file exists
    public bool HasSaveFile();

    // Delete save file (for "New Game" overwrite)
    public void DeleteSave();
}
```

### 4.2 Save File Format

Single JSON file at `{AppData}/Oravey2/save.json`:

```json
{
  "header": {
    "formatVersion": 1,
    "playerName": "Survivor",
    "playTimeSeconds": 1234.5,
    "savedAt": "2026-03-30T12:00:00Z",
    "level": 3
  },
  "data": {
    "stats": { "strength": 5, "perception": 4, ... },
    "skills": { "firearms": 15, "melee": 20, ... },
    "hp": 85, "maxHp": 100,
    "level": 3, "xp": 450,
    "inventory": [ { "id": "pipe_wrench", "count": 1 }, ... ],
    "equipment": { "PrimaryWeapon": "pipe_wrench", "Torso": null, ... },
    "questStates": { "q_rat_hunt": "Active" },
    "questStages": { "q_rat_hunt": "kill_rats" },
    "worldFlags": { "talked_to_elder": true },
    "playerPosition": { "x": 0.0, "y": 0.5, "z": 0.0 },
    "playerZone": "town",
    "inGameHour": 14.5,
    "caps": 50
  }
}
```

### 4.3 Auto-Save

- `AutoSaveTracker` already exists with `ShouldSave` / `Tick(dt)` / `Acknowledge()`
- Wire into game loop: tick each frame, when `ShouldSave` → call `SaveService.SaveGame()`
- Default interval: 300 seconds (5 minutes)
- Also auto-save on zone transition
- Show "Auto-saving..." notification via `NotificationService`

### 4.4 Load Flow

1. Start menu → "Continue" clicked
2. `SaveService.LoadGame()` → returns `SaveDataRestorer`
3. Determine target zone from `restorer.PlayerZone`
4. Load zone map, spawn entities
5. Call `restorer.RestoreStats()`, `RestoreHealth()`, etc.
6. Set player position from `restorer.PlayerPosition`
7. Restore quest log states
8. Restore world flags
9. Transition to `GameState.Exploring`

---

## 5. Currency System

### 5.1 Caps (simple integer)

M1 needs a currency for death penalty and merchant trading.

- Add `Caps` property to `InventoryComponent` (or a new lightweight `WalletComponent`)
- Starting caps: 50
- Saved/loaded as part of inventory state
- Death penalty: lose 10% (rounded down, minimum 0)

---

## 6. Program.cs Changes

### 6.1 New Bootstrap Flow

```
App starts
  → Render title background
  → Show StartMenuScript
  → GameState = InMenu
  → Wait for user action

New Game:
  → GameState = Loading
  → Create fresh world (town zone)
  → Spawn player at town spawn point
  → Wire all systems (existing M0 wiring + new M1 systems)
  → Register SaveService, AutoSaveTracker
  → GameState = Exploring

Continue:
  → GameState = Loading
  → SaveService.LoadGame()
  → Create world for saved zone
  → Restore all state
  → GameState = Exploring
```

### 6.2 New GameAction Inputs

| Action | Key | Context |
|--------|-----|---------|
| `Pause` | Escape | Exploring, InCombat |
| `QuickSave` | F5 | Exploring |
| `QuickLoad` | F9 | Exploring |

---

## 7. Files to Create / Modify

| Action | File | Notes |
|--------|------|-------|
| Create | `src/Oravey2.Core/UI/Stride/StartMenuScript.cs` | Full-screen start menu |
| Create | `src/Oravey2.Core/UI/Stride/PauseMenuScript.cs` | Pause overlay |
| Create | `src/Oravey2.Core/UI/Stride/SettingsMenuScript.cs` | Volume + auto-save settings |
| Create | `src/Oravey2.Core/Save/SaveService.cs` | Orchestrate save/load I/O |
| Modify | `src/Oravey2.Core/Framework/State/GameState.cs` | Verify `InMenu` state transitions |
| Modify | `src/Oravey2.Core/Input/GameAction.cs` | Add `Pause`, `QuickSave`, `QuickLoad` |
| Modify | `src/Oravey2.Windows/Program.cs` | New bootstrap flow, deferred world init |
| Modify | `src/Oravey2.Core/Inventory/Core/InventoryComponent.cs` | Add `Caps` property |

---

## 8. Automation Queries (for UI Tests)

| Query | Response | Purpose |
|-------|----------|---------|
| `GetMenuState` | `{ screen: "StartMenu", buttons: [...] }` | Verify menu visibility |
| `ClickMenuButton` | `{ success: true }` | Simulate button press |
| `TriggerSave` | `{ success: true, path: "..." }` | Force save for testing |
| `TriggerLoad` | `{ success: true }` | Force load for testing |
| `GetSaveExists` | `{ exists: true/false }` | Check save file presence |

---

## 9. Test Plan

### Unit Tests (target: +15)

| Test | Validates |
|------|-----------|
| `SaveService_SaveAndLoad_RoundTrips` | Full save→load→verify cycle |
| `SaveService_HasSaveFile_ReturnsFalse_WhenNoFile` | No save on fresh install |
| `SaveService_HasSaveFile_ReturnsTrue_AfterSave` | Save creates file |
| `SaveHeader_CapturesMetadata` | Version, timestamp, level, playtime |
| `AutoSaveTracker_TriggersAfterInterval` | 300s timer fires |
| `AutoSaveTracker_AcknowledgeResets` | Reset after save completes |
| `PauseMenu_EscapeTogglesPause` | State transitions |
| `Caps_StartAt50` | Initial currency |
| `Caps_DeathPenalty_10Percent` | 50 caps → 45 after death |
| `Caps_DeathPenalty_MinZero` | 3 caps → 0, not negative |
| `SettingsService_PersistsVolume` | Save/load settings.json |
| `GameState_InMenu_TransitionsToLoading` | Valid state transition |
| `GameState_Loading_TransitionsToExploring` | Valid state transition |
| `QuickSave_OnlyDuringExploring` | Blocked during combat |
| `StartMenu_ContinueDisabled_WhenNoSave` | Button state logic |

### UI Tests (target: +10)

| Test | Validates |
|------|-----------|
| `StartMenu_ShowsOnLaunch` | Menu visible at startup |
| `StartMenu_NewGame_EntersExploring` | Transition to game world |
| `PauseMenu_EscapeOpens` | Escape key shows pause |
| `PauseMenu_ResumeCloses` | Resume returns to game |
| `SaveLoad_RoundTrip_Position` | Save position, load, verify |
| `SaveLoad_RoundTrip_Inventory` | Save items, load, verify |
| `SaveLoad_RoundTrip_QuestState` | Save quest progress, load, verify |
| `SaveLoad_RoundTrip_Health` | Save HP, load, verify |
| `QuickSave_F5_Saves` | F5 triggers save |
| `AutoSave_ShowsNotification` | "Auto-saving..." text appears |

---

## 10. Acceptance Criteria

Phase 1 is complete when:

1. Game launches to a start menu (not directly into gameplay)
2. "New Game" creates a fresh game session
3. "Continue" loads saved state (grayed out if no save)
4. Escape opens pause menu during gameplay
5. "Save Game" from pause menu persists to disk
6. "Load" from start menu restores all state
7. Auto-save triggers every 5 minutes
8. Settings screen adjusts volume (persisted separately)
9. Player has a Caps currency (starts at 50)
10. All M0 tests still pass
