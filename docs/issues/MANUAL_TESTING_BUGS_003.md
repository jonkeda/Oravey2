# Issue: Three Manual Testing Bugs (W/S Swapped, Q/E Broken, Zoom Broken)

## Bug 1: W and S movement directions are swapped

### Symptom
Pressing W moves the player visually downward/away on screen. Pressing S moves the player visually upward/toward the camera. The directions are the reverse of what the user expects.

### Root Cause
In `PlayerMovementScript.Update()`, the input-to-world mapping is:

```csharp
var worldX = movement.X * cosYaw + movement.Y * sinYaw;
var worldZ = -movement.X * sinYaw + movement.Y * cosYaw;
```

W sets `movement.Y = +1`. At yaw=45°, this yields `worldZ = +1 * cos(45°) ≈ +0.707`.

In Stride's coordinate system, **+Z points away from the camera** in the default isometric view. So W pushes the player in +Z, which is visually "down" on screen — the opposite of what the user expects.

### Fix
Negate the Y component when computing `worldZ` (or negate the entire Z result):

```csharp
// Current (wrong)
var worldZ = -movement.X * sinYaw + movement.Y * cosYaw;

// Fixed
var worldZ = -movement.X * sinYaw - movement.Y * cosYaw;
```

Alternatively, negate `movement.Y` before the transform to flip the forward/backward convention.

### Files
- `src/Oravey2.Core/Player/PlayerMovementScript.cs` — lines 47–48

---

## Bug 2: Q/E camera rotation doesn't work during manual play

### Symptom
Pressing Q or E on the physical keyboard does not rotate the camera. The automated tests can detect the yaw property changing, but during manual gameplay the rotation doesn't fire.

### Analysis
`IsometricCameraScript.HandleRotationInput()` calls `_inputProvider.IsActionPressed(GameAction.RotateCameraLeft)`, which maps to Stride's `InputManager.IsKeyPressed(Keys.Q)`.

`IsKeyPressed` returns true only on the **single frame** where the key transitions from released→pressed. This works fine with the physical keyboard. If Q/E rotation doesn't work manually, the issue may be:

1. **Script execution order**: `InputUpdateScript` calls `_inputProvider.Update(Input)` which reads `IsKeyPressed`. But `IsometricCameraScript.Update()` also calls `_inputProvider.IsActionPressed()`. If `IsometricCameraScript.Update()` runs **before** `InputUpdateScript.Update()`, the `_input` field may still be null or stale from the previous frame.

2. **Stale `_input` reference**: `KeyboardMouseInputProvider.Update(InputManager)` stores the `InputManager` reference, but `IsKeyPressed` state is frame-transient. If `IsActionPressed` is called after the InputManager clears its per-frame state, it returns false.

### Fix
Ensure script execution order: `InputUpdateScript` must run before `IsometricCameraScript`.

Or: Cache the per-frame pressed state in `KeyboardMouseInputProvider.Update()` instead of reading live from `InputManager`:

```csharp
// In Update(), snapshot the pressed actions for this frame
_pressedThisFrame.Clear();
foreach (var (action, keys) in _keyBindings)
    if (keys.Any(k => input.IsKeyPressed(k)))
        _pressedThisFrame.Add(action);

// In IsActionPressed()
return _pressedThisFrame.Contains(action);
```

### Files
- `src/Oravey2.Core/Input/KeyboardMouseInputProvider.cs` — `IsActionPressed()` and `Update()`
- `src/Oravey2.Core/Input/InputUpdateScript.cs` — execution order
- `src/Oravey2.Core/Camera/IsometricCameraScript.cs` — `HandleRotationInput()`

---

## Bug 3: Zoom (scroll wheel) doesn't work

### Symptom
Scrolling the mouse wheel does not change the camera zoom level during manual play. The zoom value stays at 20.

### Analysis
`IsometricCameraScript.HandleZoomInput()` reads `_inputProvider.ScrollDelta`, which comes from `KeyboardMouseInputProvider.Update()`:

```csharp
ScrollDelta = input.MouseWheelDelta;
```

This reads `InputManager.MouseWheelDelta`. If the value is always 0, possible causes:

1. **Same script ordering issue as Bug 2**: If `IsometricCameraScript.Update()` runs before `InputUpdateScript.Update()`, `ScrollDelta` is still 0 from the previous frame (or the input provider hasn't been updated yet).

2. **MouseWheelDelta cleared too early**: Stride's `InputManager` resets `MouseWheelDelta` to 0 between frames. If read at the wrong time, it's always 0.

3. **No automation support**: The Brinell automation system injects keys via `HandleKeyDown`/`HandleKeyUp` on the keyboard device but **cannot inject mouse scroll events**. This means automated tests cannot test zoom without a `SetCameraZoom` automation command. However, the bug is about manual play, not tests.

### Fix
Same root fix as Bug 2 — ensure `InputUpdateScript` runs first so `ScrollDelta` is captured before `IsometricCameraScript` reads it.

Additionally, for automated testing, add a `SetCameraZoom` command to `OraveyAutomationHandler`:

```csharp
"SetCameraZoom" => SetCameraZoom(command),
```

### Files
- `src/Oravey2.Core/Input/KeyboardMouseInputProvider.cs` — `ScrollDelta` assignment
- `src/Oravey2.Core/Input/InputUpdateScript.cs` — execution ordering
- `src/Oravey2.Core/Camera/IsometricCameraScript.cs` — `HandleZoomInput()`
- `src/Oravey2.Windows/OraveyAutomationHandler.cs` — add `SetCameraZoom` command

---

## Common Thread

Bugs 2 and 3 likely share the same root cause: **script execution order**. Stride's `SyncScript.Update()` calls execute in entity-add order. In `Program.cs`:

1. `InputManager` entity added (line ~69) → `InputUpdateScript`
2. `Player` entity added (line ~92) → `PlayerMovementScript`
3. Camera entity added (line ~101) → `IsometricCameraScript`

This order looks correct (input → player → camera), but the issue may be that `KeyboardMouseInputProvider` stores a reference to `InputManager` and later reads in the same frame are reading stale state. Snapshotting all per-frame transient state (pressed keys, scroll delta) inside `Update()` would fix both bugs 2 and 3.

## Priority

| Bug | Severity | Complexity |
|-----|----------|------------|
| W/S swapped | High — core movement wrong | Trivial (sign flip) |
| Q/E rotation | High — feature broken | Low (snapshot pressed state or verify ordering) |
| Zoom broken | Medium — feature broken | Low (same fix as Q/E + optional automation command) |
