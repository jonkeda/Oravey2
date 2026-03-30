---
applyTo: "tests/Oravey2.UITests/**"
description: "Guidelines for writing and maintaining Oravey2 UI tests using Brinell.Stride automation. Use when creating, editing, or reviewing UI test files."
---

# Oravey2 UI Test Guidelines

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

1. Run one test class at a time using `--filter`
2. If any test in the class fails, **fix it before moving to the next class**
3. After fixing, rerun only that class to confirm the fix
4. Update the task list after each class completes
5. Only run the full suite as a final confirmation after all classes pass individually

```bash
# Run a single test class
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
