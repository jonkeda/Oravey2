# UI Test Design — Spatial Behavioral Tests

## Problem

The current tests pass but fail to catch real bugs found in manual testing:
- Camera not following player
- Zoom not working
- Camera rotation not working
- Fullscreen toggle not working

**Root cause:** Tests verify internal state (property values) rather than observable behavior. A test that reads `CameraState.Yaw == 315` after pressing Q proves the _property changed_, not that the _view actually rotated_. All four bugs went undetected because the tests never checked what the user actually sees.

## Design Principles

1. **Assert observable outcomes, not internal state.** If the player moves, verify their world position landed on a known tile. If the camera rotates, verify that a landmark that was on the left is now on the right.
2. **Use the world as a test harness.** The 16x16 tile map is deterministic (seed 42). Road at x=8/y=8, walls on borders. Player starts at (0, 0.5, 0). These are known reference points.
3. **WorldToScreen is the bridge.** Project world points to screen coordinates to verify what's visible, not just what exists.
4. **Screenshot comparison as safety net.** Binary screenshot diff catches visual regressions that state queries can't.

## Available Query Infrastructure

| Query | Returns | Purpose |
|-------|---------|---------|
| `GetPlayerPosition` | world x,y,z | Player's current world-space position |
| `GetCameraState` | x,y,z,yaw,pitch,zoom | Camera properties |
| `GetEntityPosition(name)` | world x,y,z | Any named entity's world position |
| `WorldToScreen(x,y,z)` | screenX,screenY,normX,normY,onScreen | Project world point to screen coords |
| `GetTileAtWorldPos(x,z)` | tileX,tileZ,tileType | Which tile type is at a world position |
| `GetPlayerScreenPosition` | normX,normY,onScreen | Player projected to screen |
| `TakeScreenshot` | file path | Capture current frame |
| `GetSceneDiagnostics` | entity counts, camera info | Scene overview |

Input: `HoldKey(key, ms)`, `PressKey(key)` via Brinell automation pipe.

## World Reference Map

```
16x16 grid, TileSize=1.0, centered at world origin.
Tile (0,0) = world (-7.5, *, -7.5)    Tile (15,15) = world (7.5, *, 7.5)
Tile (8,8) = world (0.5, *, 0.5)      — Road intersection (center)

Player starts at world (0, 0.5, 0) ≈ tile (8, 8) — the road crossroads.
Camera starts at yaw=45°, pitch=30°, zoom=20, orthographic.
Rotation snaps 90° per Q/E press.

Border walls:  x=0, x=15, y=0, y=15
Road:          x=8 (vertical), y=8 (horizontal)
Ground/Rubble/Water: interior, deterministic via Random(42)
```

## Test Classes

### 1. `SpatialMovementTests` — "Move then verify where you are"

These replace the current `InputSimulationTests`. Instead of just checking "distance > 0.5", they verify the player arrived at a _specific tile_ and the _camera followed_.

| Test | Action | Assertion |
|------|--------|-----------|
| `MoveW_PlayerMovesToExpectedTile` | Hold W 1s | Player pos moved in +X/+Z direction (screen-up at 45° yaw). Player is on a known tile (Road or Ground, not Wall). |
| `MoveS_PlayerMovesOpposite` | Hold W 500ms, record, Hold S 500ms | Player moved back toward origin. Tile under player changed back. |
| `MoveA_PlayerMovesLeftOnScreen` | Record `WorldToScreen(player)`, Hold A 500ms | Player screen-X decreased (moved left on screen). Player still on valid tile. |
| `MoveD_PlayerMovesRightOnScreen` | Record `WorldToScreen(player)`, Hold D 500ms | Player screen-X increased (moved right on screen). Player still on valid tile. |
| `MoveW_PlayerStaysOnMap` | Hold W 3s | Player is still within tile map bounds (not past walls at edge). `GetTileAtWorldPos` returns non-Empty. |
| `WASD_AreOrthogonal` | Hold W 500ms record delta, Hold A 500ms record delta | W-delta and A-delta are roughly perpendicular (dot product ≈ 0). |

### 2. `CameraFollowTests` — "Player moves, camera follows, player stays on screen"

These verify the camera _actually_ tracks the player, not just that the camera property changed.

| Test | Action | Assertion |
|------|--------|-----------|
| `PlayerOnScreen_AtStart` | None | `GetPlayerScreenPosition` → `onScreen == true`, normX ≈ 0.5, normY ≈ 0.5 (centered). |
| `PlayerOnScreen_AfterMovement` | Hold W 1.5s | `GetPlayerScreenPosition` → `onScreen == true`. Player still roughly centered (normX in 0.3–0.7). |
| `CameraOffset_MatchesYawPitch` | None | Camera position = player position + expected offset from yaw=45°, pitch=30°, distance=20. |
| `CameraFollows_PositionDelta` | Hold W 1s, record player delta & camera delta | Camera delta direction ≈ player delta direction. Camera moved at least 50% of player distance. |
| `PlayerVisible_FromAllFourRotations` | For each of 4 Q-presses: move W 300ms, check `GetPlayerScreenPosition` | Player is `onScreen` at yaw 45°, 315°, 225°, 135°. |

### 3. `CameraRotationTests` — "Turn and verify the world rotated"

These verify that pressing Q/E produces an actual visual change — landmarks move on screen.

| Test | Action | Assertion |
|------|--------|-----------|
| `QPress_WorldRotatesOnScreen` | Record `WorldToScreen(wall-corner)` before, press Q, record after | Wall corner's screen-X changed significantly (>100px or >10% of screen width). |
| `EPress_WorldRotatesOpposite` | Press Q then record screen pos of a landmark, press E | Landmark returns to approximately its original screen position. |
| `FourQPresses_Returns360` | Press Q 4 times, record camera yaw after each | Yaw cycles through 4 values and returns to 45°. Screenshot after 4 matches screenshot before. |
| `RotationChangesView_Visually` | Take screenshot, press Q, take screenshot | Screenshots differ (byte comparison). Both are non-trivial (>1KB). |
| `Rotation_LandmarkMovement` | Pick tile (0,0) = wall corner at world (-7.5, *, -7.5). Record screenPos. Press Q. Record screenPos. | The screen position of the wall corner moved. If it was on screen, it moved to a different quadrant or went off screen. |

### 4. `ZoomTests` — "Zoom and verify the view changed"

Scroll wheel can't be easily injected. These tests verify zoom state and use the `WorldToScreen` projection to detect zoom effects.

| Test | Action | Assertion |
|------|--------|-----------|
| `InitialZoom_WorldEdgesVisible` | None | `WorldToScreen` for the 4 wall corners → at least 2 are `onScreen`. |
| `ZoomState_IsQueryable` | None | `GetCameraState().Zoom == 20`. Orthographic size matches. |
| `ZoomOut_WouldShowMoreWorld` | _(Requires zoom input or a `SetZoom` command)_ | After zoom to 30, all 4 corners have smaller normX/normY range (closer to center). |

> **Note:** Zoom tests are limited until scroll-wheel injection is supported or a `SetCameraZoom` automation command is added. Mark zoom-input tests as `[Fact(Skip = "...")]` with a note.

### 5. `FullscreenTests` — "F11 toggles window mode"

| Test | Action | Assertion |
|------|--------|-----------|
| `F11_TogglesFullscreen` | Press F11 | Screenshot resolution changes OR window state query returns fullscreen. |

> **Note:** Fullscreen detection requires a `GetWindowState` automation query that doesn't exist yet. This test needs a new query or resolution-based detection. Mark with skip and document.

### 6. `GameLifecycleTests` — Keep existing (these are fine)

The existing `Game_StartsAndConnects`, `Game_IsNotBusy_AfterStartup`, etc. are valid smoke tests. Keep as-is.

## New Automation Queries Needed

None beyond what already exists in `OraveyAutomationHandler`. The `WorldToScreen`, `GetTileAtWorldPos`, `GetEntityPosition`, and `GetPlayerScreenPosition` queries are already implemented.

**Optional future additions:**
- `SetCameraZoom(float)` — Allow tests to set zoom directly for zoom tests
- `GetWindowState` — Return fullscreen/windowed, resolution
- `GetTilesInScreenRect(x1,y1,x2,y2)` — Return visible tiles within a screen region

## New Test Helper Needed

`GameQueryHelpers` needs two new deserialization methods:

```csharp
// Project world point to screen coordinates
public static ScreenPosition WorldToScreen(IStrideTestContext ctx, double x, double y, double z)

// Get tile type at a world-space position
public static TileInfo GetTileAtWorldPos(IStrideTestContext ctx, double worldX, double worldZ)

// Get player screen position
public static ScreenPosition GetPlayerScreenPosition(IStrideTestContext ctx)
```

The records `ScreenPosition` (already defined) and `TileInfo` (new: `tileX, tileZ, tileType`) wrap the JSON responses.

## File Layout After Redesign

```
tests/Oravey2.UITests/
  GameLifecycleTests.cs          — Keep existing (smoke tests)
  SpatialMovementTests.cs        — Replaces InputSimulationTests.cs
  CameraFollowTests.cs           — New
  CameraRotationTests.cs         — Replaces CameraTests from GameStateQueryTests.cs
  ZoomTests.cs                   — New (partially skipped)
  FullscreenTests.cs             — New (skipped until query exists)
  GameWorldTests.cs              — Keep screenshot + scene diagnostics tests
  GameQueryHelpers.cs            — Add WorldToScreen/TileInfo helpers
  OraveyTestFixture.cs           — Keep as-is
  Pages/GameWorldPage.cs         — Keep as-is
```

Delete:
- `InputSimulationTests.cs` (replaced by `SpatialMovementTests.cs`)
- `GameStateQueryTests.cs` (camera tests move to `CameraRotationTests.cs`, others to `CameraFollowTests.cs`)

## Implementation Priority

1. Add `WorldToScreen` / `GetTileAtWorldPos` / `GetPlayerScreenPosition` helpers to `GameQueryHelpers.cs`
2. `SpatialMovementTests.cs` — Most critical, validates core WASD gameplay
3. `CameraFollowTests.cs` — Validates camera tracks player on screen
4. `CameraRotationTests.cs` — Validates Q/E produces visual rotation
5. `ZoomTests.cs` — Partial, blocked on scroll injection
6. `FullscreenTests.cs` — Blocked on window state query
7. Clean up old test files
