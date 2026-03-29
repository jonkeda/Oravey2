# RCA: Zoom Does Not Work

## Symptom

Scrolling the mouse wheel during manual play does not change the camera zoom level. There are no keyboard keys bound for zoom either.

## Investigation

### Zoom input chain

1. `IsometricCameraScript.HandleZoomInput()` reads `_inputProvider.ScrollDelta`
2. `KeyboardMouseInputProvider.Update()` sets `ScrollDelta = input.MouseWheelDelta`
3. `InputUpdateScript.Update()` calls `_inputProvider.Update(Input)`
4. `IsometricCameraScript.Update()` calls `HandleZoomInput()` which reads `ScrollDelta`

### Root Causes

**Cause 1: Script execution timing (mouse scroll)**

`MouseWheelDelta` in Stride's `InputManager` is a per-frame transient value — it resets to 0 after the frame. `KeyboardMouseInputProvider.Update()` snapshots it into `ScrollDelta`, but the timing depends on script execution order:

- `InputUpdateScript` (on "InputManager" entity, added first)
- `PlayerMovementScript` (on "Player" entity, added second)
- `IsometricCameraScript` (on camera entity, added third)

This order is correct — `InputUpdateScript` runs first. So `ScrollDelta` should be populated before the camera reads it. **However**, Stride's `InputManager.MouseWheelDelta` accumulates scroll events from the OS message pump. If the game window doesn't have focus, or if the Stride `InputManager` doesn't process mouse wheel events from the injected keyboard device (automation uses `HandleKeyDown`/`HandleKeyUp`, not mouse events), the value stays 0.

For manual play: the mouse scroll should work IF the game window has focus. If it doesn't work, the likely cause is that the Stride window's message pump isn't receiving `WM_MOUSEWHEEL` events — possibly because the window doesn't have input focus (e.g., running headless or in background).

**Cause 2: No keyboard bindings for zoom (keyboard)**

`GameAction.ZoomIn` and `GameAction.ZoomOut` exist in the enum, and `IsActionPressed()` handles them by checking `ScrollDelta > 0` / `ScrollDelta < 0`. But:

- There are **no keyboard key bindings** for these actions in `_keyBindings`
- `HandleZoomInput()` doesn't call `IsActionPressed(ZoomIn/ZoomOut)` — it reads `ScrollDelta` directly
- There is no way to zoom via keyboard

**Cause 3: No automation support for scroll injection**

The Brinell automation system injects keys via `HandleKeyDown`/`HandleKeyUp` on the keyboard device. There is no API to inject mouse scroll events. This means automated tests cannot currently test zoom via scroll wheel input.

## Fix

### 1. Add keyboard bindings for zoom (e.g., +/- or PageUp/PageDown)

In `KeyboardMouseInputProvider._keyBindings`:
```csharp
{ GameAction.ZoomIn, [Keys.OemPlus, Keys.PageUp] },
{ GameAction.ZoomOut, [Keys.OemMinus, Keys.PageDown] },
```

### 2. Use IsActionPressed in HandleZoomInput

In `IsometricCameraScript.HandleZoomInput()`, check both scroll delta AND keyboard zoom:
```csharp
private void HandleZoomInput()
{
    if (_inputProvider == null) return;

    float zoomDelta = 0f;

    // Mouse scroll
    if (_inputProvider.ScrollDelta != 0)
        zoomDelta = -_inputProvider.ScrollDelta;

    // Keyboard zoom
    if (_inputProvider.IsActionPressed(GameAction.ZoomIn))
        zoomDelta = -1f;
    if (_inputProvider.IsActionPressed(GameAction.ZoomOut))
        zoomDelta = 1f;

    if (Math.Abs(zoomDelta) > 0.001f)
    {
        var oldZoom = CurrentZoom;
        CurrentZoom += zoomDelta * ZoomSpeed;
        CurrentZoom = MathUtil.Clamp(CurrentZoom, ZoomMin, ZoomMax);
        if (Math.Abs(oldZoom - CurrentZoom) > 0.01f)
            _eventBus?.Publish(new CameraZoomChangedEvent(oldZoom, CurrentZoom));
    }
}
```

### 3. Enable automated zoom testing

With keyboard zoom bindings, the existing Brinell key injection can test zoom:
```csharp
_fixture.Context.PressKey(VirtualKey.OemPlus);  // zoom in
_fixture.Context.PressKey(VirtualKey.OemMinus); // zoom out
```

The skipped `ZoomOut_WouldShowMoreWorld` test can then be unskipped.

## Impact

| Fix | Effort | Unblocks |
|-----|--------|----------|
| Add ZoomIn/ZoomOut key bindings | Trivial (2 lines) | Keyboard zoom for manual play |
| Rewrite HandleZoomInput | Low (10 lines) | Both scroll + keyboard zoom |
| Unskip zoom test | Trivial | Automated zoom regression testing |
