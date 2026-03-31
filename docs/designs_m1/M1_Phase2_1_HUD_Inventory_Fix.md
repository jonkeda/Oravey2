# M1 Phase 2.1 — HUD Caps Display + Inventory Overlay Fix

**Status:** Proposed  
**Depends on:** Phase 2.5 (Merchant Buy/Sell) ✅  
**Scope:** Small — 2 bug fixes + 1 enhancement, no new systems

---

## Problem Statement

Two issues prevent the player from seeing their economy state during gameplay:

1. **Caps not visible on the HUD** — The HUD shows HP, AP, Level/XP, and game state, but not the player's Caps balance. After buying/selling with the merchant, the player has no way to see how many caps they have without automation commands.

2. **Inventory doesn't open with I key** — The `InventoryOverlayScript` listens for `Keys.Tab` (hardcoded) instead of using `GameAction.Inventory` via `InputProvider`. The `I` key is correctly mapped to `GameAction.Inventory` in `KeyboardMouseInputProvider`, but the overlay never consumes it. Additionally, `InputProvider` is never wired to the overlay in `ScenarioLoader`.

---

## Fix A — Add Caps to HUD

### File: `src/Oravey2.Core/UI/Stride/HudSyncScript.cs`

**Change:** Add `InventoryComponent?` property. Add a `_capsText` TextBlock below the level row. Update it each frame with `Inventory.Caps`.

```
Layout (top-left):
┌────────────────────────────────┐
│ ████████████████  120/150      │  ← HP bar (green/yellow/red)
│ ██████████████    80/100       │  ← AP bar (blue)
│ LVL: 3  XP: 45/300            │  ← Level + XP (gold)
│ 💰 50 caps                     │  ← NEW: Caps (yellow)
│ Exploring                      │  ← Game state
└────────────────────────────────┘
```

### File: `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs`

**Change:** Pass `playerInventory` to `HudSyncScript` in all 3 scenario loaders (m0_combat, empty, town) via a new `Inventory` property.

### Unit Test Validation

No new unit tests needed — this is purely a display property wiring. Existing HUD automation tests (`GetHudState`) already cover the handler. However, if we want to verify caps appear in the HUD response, we could add a `Caps` field to `HudStateResponse`. *(Optional — caps are already queryable via `GetCapsState`.)*

---

## Fix B — Inventory Overlay Key Binding

### Root Cause

`InventoryOverlayScript.Update()` uses `Input.IsKeyPressed(Keys.Tab)` (raw Stride input) instead of `InputProvider.IsActionPressed(GameAction.Inventory)`. Three sub-issues:

| Issue | Location |
|-------|----------|
| Wrong key: `Keys.Tab` instead of `Keys.I` | `InventoryOverlayScript.cs` line 44 |
| Raw input instead of `InputProvider` | `InventoryOverlayScript.cs` line 44 |
| `InputProvider` not wired | `ScenarioLoader.cs` — all 3 scenario loaders |

### File: `src/Oravey2.Core/UI/Stride/InventoryOverlayScript.cs`

**Change:**
1. Add `IInputProvider? InputProvider` property
2. Replace `Input.IsKeyPressed(Keys.Tab)` with `InputProvider?.IsActionPressed(GameAction.Inventory) == true`
3. Block toggle when `InDialogue` (to avoid conflict with number keys / UI focus)

### File: `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs`

**Change:** Pass `inputProvider` to `InventoryOverlayScript` in all 3 scenario loaders.

---

## Fix C — Show Caps in Inventory Overlay Header

### File: `src/Oravey2.Core/UI/Stride/InventoryOverlayScript.cs`

**Change:** Add a `_capsText` TextBlock in the overlay header (between the title and weight line). Update it in `RefreshInventory()` with `Inventory.Caps`.

```
Layout (top-right panel):
┌─────────────────────────────┐
│     === INVENTORY ===       │
│ 💰 50 caps                   │  ← NEW
│ Weight: 3.5 / 80            │
│ Medkit               (0.5kg)│
│ Leather Jacket       (3.0kg)│
└─────────────────────────────┘
```

---

## Modified Files Summary

| File | Changes |
|------|---------|
| `src/Oravey2.Core/UI/Stride/HudSyncScript.cs` | Add `InventoryComponent?` prop, `_capsText` TextBlock, update in `Update()` |
| `src/Oravey2.Core/UI/Stride/InventoryOverlayScript.cs` | Add `IInputProvider?` prop, fix key binding, add `_capsText`, block in InDialogue |
| `src/Oravey2.Core/Bootstrap/ScenarioLoader.cs` | Wire `Inventory` to HUD, wire `InputProvider` to inventory overlay (×3 scenarios) |

## Unit Tests (+2)

| Test | File | Validates |
|------|------|-----------|
| `HudSyncScript_HasInventoryProperty` | Existing HudSyncScript test file | Property exists and accepts InventoryComponent |
| `InventoryOverlay_HasInputProviderProperty` | Existing InventoryOverlay test file | Property exists and accepts IInputProvider |

## UI Tests (+2, appended to TownTests.cs)

| Test | Validates |
|------|-----------|
| `Town_HudShowsCaps_InitialValue` | `GetHudState` or `GetCapsState` returns 50 on town load |
| `Town_InventoryOpens_ViaAutomation` | `GetInventoryOverlayVisible` returns true after toggle |

*(Note: We can't press I via automation easily, but we can verify the overlay exists and caps are queryable.)*

## Acceptance

- Build passes
- `I` key toggles inventory in-game
- HUD shows caps count, updates after merchant buy/sell  
- Inventory overlay header shows caps count
- All existing tests still pass (692 unit + 22 UI)
