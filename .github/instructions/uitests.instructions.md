---
applyTo: "tests/Oravey2.UITests/**"
description: "Guidelines for writing and maintaining Oravey2 UI tests using Brinell.Stride automation. Use when creating, editing, or reviewing UI test files."
---

# Oravey2 UI Test Guidelines

## When to write a UI test

UI tests verify behavior that **requires a running game process**. If you can test it by constructing a component directly and calling methods on it, write a unit test in `tests/Oravey2.Tests/` instead.

**Use UI tests ONLY for:**

- Screen-space rendering — `WorldToScreen`, screenshots, `OnScreen` checks
- Keyboard/mouse input processing — `HoldKey`/`PressKey` → observable state change
- Game process lifecycle — startup, shutdown, automation pipe connectivity
- Spatial/physics interactions — collision, proximity triggers, zone transitions
- Cross-system integration that only fires in the live game loop (kill→loot→pickup→notification)

**Never use UI tests for:**

- Config constants (HP=105, Zoom=28, enemy count=3) — unit test the config/formula
- Formulas or math (damage calculation, XP gain, weight) — unit test the component
- Data model shapes (inventory contents, equipment slots) — unit test the component
- State machines in isolation (Exploring→InCombat) — unit test `GameStateManager`

**Decision rule:** Does the test need `_fixture.Context.HoldKey()`, `GameQueryHelpers.WorldToScreen()`, or a live game loop to trigger behavior? → UI test. Otherwise → unit test.

**One canonical location per assertion:** Before adding a new assertion, grep for existing tests that already check the same behavior. Do not verify the same fact in multiple test classes.

## Always create a task list before running tests

Before running any UI tests, create (or update) a task list document at `docs/tasks/uitests-tasklist.md`. The task list must:

1. **List every test class** with its test count
2. **List every test method** as a checkbox under its class
3. **Track pass/fail status** — mark tests `[x]` when passing, leave `[ ]` when failing or not yet run
4. **Add a ✅ next to the class name** once all tests in that class pass
5. **Note flaky tests** with an italic annotation (e.g., `*(flaky — passed 2/3 runs)*`)
6. **Show totals** at the bottom (active, skipped, total)

## Run and fix tests one class at a time

UI tests are slow (~6s each including game startup/teardown). **Never run the full suite as the first step.** Instead:

1. Run smoke tests first: `dotnet test tests/Oravey2.UITests --filter "Category=Smoke"`
2. For new functionality, run the specific test class: `dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~MyNewTests"`
3. If any test fails, **fix it before moving to the next class**
4. After fixing, rerun only that class to confirm the fix
5. Only run the full suite as a final confirmation when explicitly requested

```bash
# Run smoke tests only (fast validation ~20s)
dotnet test tests/Oravey2.UITests --filter "Category=Smoke"

# Run a single test class (for new or changed functionality)
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~CameraRotationTests"

# Run a single test method (when debugging a specific failure)
dotnet test tests/Oravey2.UITests --filter "FullyQualifiedName~QPress_WorldRotatesOnScreen"
```

**Important:** Always rebuild the game binary (`dotnet build src/Oravey2.Windows/Oravey2.Windows.csproj`) before running UI tests if any game-side code changed (automation handler, SyncScripts, Program.cs).

## Never wait for time — always wait for a response

`HoldKey` and `PressKey` are synchronous over the named pipe. They block until the game-side action completes (key released, frame processed). The game state is settled when the call returns. **Never add `Thread.Sleep` after input calls.**

```csharp
// WRONG — unnecessary delay
_fixture.Context.HoldKey(VirtualKey.W, 1000);
Thread.Sleep(300);  // ← game already settled
var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

// RIGHT — query immediately after input returns
_fixture.Context.HoldKey(VirtualKey.W, 1000);
var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
```

If you genuinely need to wait for an async game-side process (animation, transition), poll a query in a loop with a timeout rather than sleeping a fixed duration.

## Assert observable behavior, not internal state

Tests should verify what the user sees, not engine property values. Use `WorldToScreen`, `GetPlayerScreenPosition`, `GetTileAtWorldPos`, and screenshot comparison to assert spatial outcomes.

```csharp
// WRONG — checks a property, misses rendering bugs
var cam = GameQueryHelpers.GetCameraState(ctx);
Assert.Equal(315.0, cam.Yaw, 1.0);

// RIGHT — checks that a world landmark actually moved on screen
var before = GameQueryHelpers.WorldToScreen(ctx, -7.5, 0, -7.5);
_fixture.Context.PressKey(VirtualKey.Q);
var after = GameQueryHelpers.WorldToScreen(ctx, -7.5, 0, -7.5);
Assert.True(Math.Abs(after.ScreenX - before.ScreenX) > 100);
```

## Test fixture lifecycle

Each test class gets its own game process via `OraveyTestFixture` (`IAsyncLifetime`). The game launches in `InitializeAsync` and is killed in `DisposeAsync`. Tests within a class share one process and run sequentially — game state carries over between tests in the same class.

## Available query helpers

All helpers are in `GameQueryHelpers.cs`. Key APIs:

| Method | Returns | Use for |
|--------|---------|---------|
| `GetPlayerPosition(ctx)` | world x,y,z | Player location after movement |
| `GetCameraState(ctx)` | x,y,z,yaw,pitch,zoom | Camera properties |
| `WorldToScreen(ctx, x, y, z)` | screenX,screenY,normX,normY,onScreen | Verifying visibility and screen position |
| `GetPlayerScreenPosition(ctx)` | normX,normY,onScreen + world pos | Player on-screen check (camera follow) |
| `GetTileAtWorldPos(ctx, x, z)` | tileX,tileZ,tileType | Verify player is on a valid tile |
| `TakeScreenshot(ctx)` | file path | Visual regression / non-solid-color checks |
| `GetSceneDiagnostics(ctx)` | entity counts, camera info | Scene readiness verification |

## World reference

- 16×16 tile map, `TileSize=1.0`, centered at world origin
- Tile (0,0) = world (-7.5, 0, -7.5), Tile (15,15) = world (7.5, 0, 7.5)
- Player starts at world (0, 0.5, 0) ≈ tile (8,8) — road intersection
- Camera: yaw=45°, pitch=30°, zoom=20, orthographic, continuous rotation at 120°/s via Q/E held, zoom at 15 units/s via PageUp/PageDown held
- Border walls at tile x=0, x=15, y=0, y=15; roads at x=8, y=8
- No wall collision yet — player can walk off the map with long hold times

## Skip convention

Use `[Fact(Skip = "reason")]` for tests blocked on missing infrastructure (e.g., scroll-wheel injection, `GetWindowState` query). Include the specific blocker in the skip reason.
