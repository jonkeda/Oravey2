# M0 Prototype — Completion Plan

**Goal:** A playable Windows-only prototype demonstrating tactical RPG camera, player movement, tile-based map, and a basic combat loop.

**Current status:** 621 unit tests passing across Steps 1-10. Core game logic is complete. The Stride runtime layer has camera, movement, and tile rendering working. Combat logic exists but is not wired into the running game.

---

## What's Already Working

| Feature                                         | Status         | Details                                                                    |
| ----------------------------------------------- | -------------- | -------------------------------------------------------------------------- |
| Tactical RPG camera                             | Done           | Perspective projection, Q/E rotation, scroll zoom, smooth follow           |
| Player movement                                 | Done           | WASD camera-relative, frame-independent, publishes events                  |
| Tile map rendering                              | Done           | 16×16 procedural map with walls, roads, rubble, water, ground             |
| Windows launcher                                | Done           | Code-only Stride app, no asset files needed                                |
| Input abstraction                               | Done           | IInputProvider + KeyboardMouseInputProvider                                |
| Framework (EventBus, ServiceLocator, GameState) | Done           | Wired into Program.cs                                                      |
| Combat logic                                    | Done (pure C#) | CombatEngine, DamageResolver, ActionQueue, CombatComponent, CombatFormulas |
| AI logic                                        | Done (pure C#) | AIBehaviorComponent, SightSensor, TileGridPathfinder, utility scoring      |
| All Steps 1-10 logic                            | Done (pure C#) | 621 tests, all passing                                                     |

## What's Missing for Playable M0

### Phase A — Enemy & Combat Integration

**A1. Spawn enemy entities in the scene**

- Add 2-3 enemy entities to `Program.cs` with procedural meshes (red capsules)
- Give each a `CombatComponent` (HP, AP, weapon stats)
- Place them on walkable tiles in the test map

**A2. Combat encounter trigger**

- Detect player proximity to enemy (simple distance check in a SyncScript)
- Transition `GameStateManager` to `InCombat` when player is within range
- Transition back to `Exploring` when all enemies are dead

**A3. Wire CombatEngine into the game loop**

- Create `CombatSyncScript` — a SyncScript that runs the RTwP combat loop each frame
- Read `CombatComponent` from player + enemies
- Call `CombatEngine.ProcessAttack()` for queued actions
- Consume `ActionQueue` entries and apply results (damage, death)
- Publish existing combat events (`CombatStartedEvent`, `AttackResolvedEvent`, `EntityDiedEvent`)

**A4. Player attack input**

- Add a keyboard shortcut (e.g., Space or left-click on enemy) to queue a player attack
- Wire into `ActionQueue` so the combat script processes it next frame

**A5. Simple combat feedback**Flash enemy mesh red on hit (material colour swap for 0.2s)

- Remove enemy entity on death
- Transition back to `Exploring` when no enemies remain

### Phase B — Camera Rename & Collision

**B1. Rename IsometricCameraScript → TacticalCameraScript**

- Rename class and file to match the actual camera style
- Update `Program.cs` reference

**B2. Basic player-wall collision**

- In `PlayerMovementScript`, read `TileMapData` and reject movement into `Wall` tiles
- Simple tile-occupancy check (no physics engine needed for M0)

### Phase C — Minimal HUD

**C1. Combat HUD overlay**

- Stride UI: display player HP bar and enemy HP bar during `InCombat` state
- Show "Exploring" / "In Combat" text indicator
- Use Stride's built-in UI system (`UIComponent` + `TextBlock` + `Grid`)

**C2. Game over / victory text**

- If player HP hits 0: show "GAME OVER" text, freeze input
- If all enemies dead: show "ENEMIES DEFEATED" text, return to exploring

### Phase D — Polish & Test

**D1. Gameplay tuning**

- Balance enemy HP / damage / AP so combat lasts 3-5 rounds
- Adjust enemy placement for a natural encounter flow
- Tune camera defaults (pitch, zoom, follow speed) for best feel

**D2. UI test coverage**

- Complete the 20+ Brinell UI tests designed in `tests/Oravey2.UITests/TEST_DESIGN.md`
- Add combat-specific tests: trigger encounter, verify state transition, verify enemy death

**D3. Cleanup**

- Remove any dead code or placeholder comments
- Verify the game runs cleanly from `dotnet run` with no warnings

---

## Phase Dependency Order

```
A1 (spawn enemies)
 └→ A2 (proximity trigger)
     └→ A3 (combat sync script)
         └→ A4 (attack input)
             └→ A5 (hit feedback)

B1 (camera rename)       — independent
B2 (wall collision)       — independent

C1 (HUD overlay)         — after A3
C2 (game over/victory)   — after A5 + C1

D1 (tuning)              — after A5 + C1
D2 (UI tests)            — after all A/B/C
D3 (cleanup)             — last
```

## Files to Create / Modify

| Action | File                                                        | Notes                                           |
| ------ | ----------------------------------------------------------- | ----------------------------------------------- |
| Create | `src/Oravey2.Core/Combat/CombatSyncScript.cs`             | SyncScript wiring CombatEngine to game loop     |
| Create | `src/Oravey2.Core/Combat/EncounterTriggerScript.cs`       | Proximity detection → state transition         |
| Create | `src/Oravey2.Core/UI/CombatHud.cs`                        | Minimal HP bars + state text (Stride UI)        |
| Modify | `src/Oravey2.Windows/Program.cs`                          | Spawn enemies, register combat scripts, add HUD |
| Rename | `IsometricCameraScript.cs` → `TacticalCameraScript.cs` | Class + file rename                             |
| Modify | `src/Oravey2.Core/Player/PlayerMovementScript.cs`         | Wall collision check                            |
| Modify | `tests/Oravey2.UITests/`                                  | Add combat test cases                           |

## Acceptance Criteria

The M0 prototype is complete when:

1. Player can move around the tile map with WASD, rotate/zoom camera with Q/E/scroll
2. Player cannot walk through walls
3. Walking near enemies triggers combat (GameState → InCombat)
4. Player can attack enemies with a keyboard shortcut
5. Enemies take damage and die (removed from scene)
6. Combat ends and returns to Exploring when all enemies are dead
7. Minimal HUD shows HP during combat
8. Player death shows game over state
9. All existing 621 unit tests still pass
10. Brinell UI tests cover camera, movement, and combat scenarios
