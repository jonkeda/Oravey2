# Step 8 — UI / UX

**Goal:** Full HUD, all game menus, platform-specific input providers, mobile touch layer.

**Depends on:** Steps 1-7

---

## Deliverables

1. HUD: health bar, AP bar, minimap, active quest tracker, quick-slot bar (6 slots)
2. Character sheet screen: stats, skills, perks, level, XP bar
3. Inventory screen: grid/list view, drag-and-drop equip, weight indicator, item tooltips
4. Quest log screen: active/completed/failed tabs, quest detail with stage progress
5. World map screen: fog-of-war overlay, fast travel selection, zone labels
6. Crafting screen: recipe list, ingredient requirements, craft button
7. Dialogue screen: speaker portrait, text area, choice buttons, skill check indicators
8. Pause menu: resume, settings, save, load, quit
9. Settings screen: volume sliders, graphics quality, survival toggle, control remapping
10. `TouchInputProvider` — virtual joystick, tap-to-interact, gesture recognizers (pinch zoom, two-finger rotate)
11. `GamepadInputProvider` — standard gamepad mapping
12. UI scaling system: DPI-aware, safe area insets for notch devices
13. Screen transition system: fade in/out, slide for menus

---

## UI Framework

- Built on Stride's UI system (XAML-like layout).
- All screens are `UIPage` instances managed by a `ScreenManager` service.
- Modal stack: push/pop screens, input routed to topmost.
- Consistent back button: Escape (keyboard), B (gamepad), swipe-right (mobile).
