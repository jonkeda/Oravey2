# M1 Phase 1 — Task List

## Files Created

- [x] `src/Oravey2.Core/Save/SaveService.cs` — Save/load I/O orchestration
- [x] `src/Oravey2.Core/UI/Stride/StartMenuScript.cs` — Full-screen start menu
- [x] `src/Oravey2.Core/UI/Stride/PauseMenuScript.cs` — Pause overlay with Escape toggle
- [x] `src/Oravey2.Core/UI/Stride/SettingsMenuScript.cs` — Volume sliders + auto-save toggle
- [x] `src/Oravey2.Core/UI/Stride/SaveLoadScript.cs` — QuickSave (F5), QuickLoad (F9), auto-save tick
- [x] `tests/Oravey2.Tests/Save/SaveServiceTests.cs` — 7 unit tests for SaveService
- [x] `tests/Oravey2.Tests/Inventory/CapsTests.cs` — 7 unit tests for Caps currency + death penalty
- [x] `tests/Oravey2.UITests/MenuSaveLoadTests.cs` — 6 UI tests for menus + save/load
- [x] `tests/Oravey2.UITests/QuickSaveDeathPenaltyTests.cs` — 6 UI tests for F5/F9/death penalty

## Files Modified

- [x] `src/Oravey2.Core/Input/GameAction.cs` — Added `QuickSave`, `QuickLoad`
- [x] `src/Oravey2.Core/Input/KeyboardMouseInputProvider.cs` — Added F5→QuickSave, F9→QuickLoad key bindings
- [x] `src/Oravey2.Core/Inventory/Core/InventoryComponent.cs` — Added `Caps` property (default 50) + `ApplyDeathPenalty()`
- [x] `src/Oravey2.Core/Save/SaveData.cs` — Added `Caps` field
- [x] `src/Oravey2.Core/Save/SaveDataBuilder.cs` — `WithInventory` now captures `Caps`
- [x] `src/Oravey2.Core/Save/SaveDataRestorer.cs` — Added `Caps` property
- [x] `src/Oravey2.Core/Framework/State/GameStateManager.cs` — Added `Loading↔InMenu` transitions
- [x] `src/Oravey2.Core/Automation/AutomationContracts.cs` — Added 6 M1 records (MenuState, ClickMenuButton, TriggerSave/Load, SaveExists) + CapsState
- [x] `src/Oravey2.Windows/OraveyAutomationHandler.cs` — Added `SetM1()`, 6 handler methods (GetMenuState, ClickMenuButton, TriggerSave, TriggerLoad, GetSaveExists, GetCapsState)
- [x] `src/Oravey2.Windows/Program.cs` — New bootstrap: menus, SaveLoadScript, auto-save, death penalty, Helper refactors (BuildSaveData, PerformSave, ApplyLoadedSave)
- [x] `tests/Oravey2.UITests/GameQueryHelpers.cs` — Added 6 helpers (GetMenuState, ClickMenuButton, TriggerSave, TriggerLoad, GetSaveExists, GetCapsState)
- [x] `tests/Oravey2.Tests/Framework/GameStateManagerTests.cs` — Added 3 transition tests (Loading→InMenu, InMenu→Loading, InMenu→Exploring)

## Bug Fix (pre-existing)

- [x] Deleted stale `src/Oravey2.Core/Camera/IsometricCameraScript.cs` — duplicate of `TacticalCameraScript.cs` from M0 rename

## Font Fix (RCA-001)

- [x] All 9 UI scripts (menus, HUD, game-over, notifications, inventory, enemy HP, floating damage) — added `SpriteFont Font` property and wired `Font = font` on every `TextBlock`
- [x] `Program.cs` — loads `StrideDefaultFont` via `Content.Load<SpriteFont>()`, passes to all scripts
- [x] `ScenarioLoader.cs` — accepts `Font` property, passes to scripts in both m0_combat and empty scenarios

## Build Status

- [x] `Oravey2.Core` — builds clean
- [x] `Oravey2.Windows` — builds clean
- [x] `Oravey2.Tests` — 652 passed, 0 failed
- [x] `Oravey2.UITests` — MenuSaveLoadTests: 6 passed; CameraRotationTests: 5 passed; QuickSaveDeathPenaltyTests: 6 (pending run)

## Remaining

- [ ] Settings persistence to `settings.json` — design calls for persisting volume/auto-save settings to disk (not yet wired)
- [ ] "Quit to Menu" full cleanup — basic cleanup done (unload + auto-save reset), advanced state cleanup deferred to M2
