# Design: Region Map Overlay

Full-screen map overlay toggled by the **M** key, showing the current region with town locations and the player's position. Gives the player a quick strategic overview without leaving the game world.

**Depends on:** Phase B (HUD/Overlay system), World/Region data, CuratedTown data, Player position tracking

---

## Scope

| Sub-task | Summary |
|----------|---------|
| R1 | Add `OpenMap` to `GameAction` enum and bind to `Keys.M` |
| R2 | Create `RegionMapOverlayScript` — full-screen Stride UI overlay |
| R3 | Render region background (terrain thumbnail or stylised outline) |
| R4 | Render town markers with labels scaled by `TownCategory` |
| R5 | Render player position indicator with pulsing animation |
| R6 | Wire overlay into `Program.cs` entity setup |

### What's Deferred

| Item | Deferred To |
|------|-------------|
| Fog of war (hide unvisited areas) | Post-M0 |
| Map markers / custom waypoints | Post-M0 |
| Zoom & pan within the map overlay | Post-M0 |
| Road / river overlays on map | Post-M0 |
| Quest objective markers | Post-M0 |
| Continent-level (L3) world map | Post-M0 |
| Fast-travel from map | Post-M0 |
| Map legend panel | Post-M0 |

---

## File Layout

```
src/Oravey2.Core/
├── Input/
│   └── GameAction.cs                          # MODIFY — add OpenMap action
│   └── KeyboardMouseInputProvider.cs           # MODIFY — bind Keys.M → OpenMap
├── UI/
│   └── Stride/
│       └── RegionMapOverlayScript.cs           # NEW — full-screen map overlay
│       └── RegionMapRenderer.cs                # NEW — draws map content (towns, player)
src/Oravey2.Windows/
└── Program.cs                                  # MODIFY — create & wire map overlay entity
```

---

## Existing APIs We'll Use

### IInputProvider

```csharp
bool IsActionPressed(GameAction action)   // true on the frame the key goes down
bool IsActionHeld(GameAction action)      // true every frame the key is held
```

### CuratedTown

```csharp
string GameName { get; }         // Display name for the map label
Vector2 GamePosition { get; }    // World-space position to plot on map
TownCategory Size { get; }       // Hamlet → Metropolis — controls icon size
DestructionLevel Destruction { get; }
```

### WorldMapData

```csharp
int ChunksWide { get; }
int ChunksHigh { get; }
ChunkData? GetChunk(int cx, int cy)
```

### SaveStateStore (player position)

```csharp
// Position stored per-region as JSON: { x, y, z }
// Key: "pos:{regionName}"
// Current region: save_meta["current_region"]
```

---

## R1 — Add `OpenMap` GameAction

Add a new action to the `GameAction` enum and bind it to `Keys.M`.

**GameAction.cs** — append:

```csharp
OpenMap
```

**KeyboardMouseInputProvider.cs** — add binding in the key-bindings dictionary:

```csharp
{ GameAction.OpenMap, new[] { Keys.M } }
```

The overlay script will check `IsActionPressed(GameAction.OpenMap)` to toggle visibility.

---

## R2 — RegionMapOverlayScript

A `SyncScript` attached to a dedicated entity with a `UIComponent`. Follows the same pattern as `InventoryOverlayScript`.

### Lifecycle

```
Start()
  └─ BuildUI()          → creates the Stride UI element tree
       ├─ Background     → semi-transparent black border (full-screen)
       ├─ Title          → TextBlock "Region Map: {regionName}"
       ├─ MapCanvas      → Canvas panel for positioned elements
       └─ Legend          → "(M) Close  •  Towns  •  ◆ You"

Update()
  ├─ Skip if GameState is GameOver or InDialogue
  ├─ if IsActionPressed(OpenMap) → toggle _visible
  ├─ if _visible → RefreshMap()
  └─ set RootElement.Visibility accordingly
```

### Properties

```csharp
public class RegionMapOverlayScript : SyncScript
{
    public IInputProvider? InputProvider { get; set; }
    public GameStateManager? StateManager { get; set; }
    public SpriteFont? Font { get; set; }
    public WorldMapData? WorldMap { get; set; }
    public List<CuratedTown>? Towns { get; set; }

    // Injected: a delegate or service to get current player world position
    public Func<Vector3>? GetPlayerPosition { get; set; }

    public bool IsVisible => _visible;
}
```

### Coordinate Mapping

World coordinates must be projected onto the overlay canvas. The map occupies a fixed screen rectangle with padding.

```
MapX = padding + (worldX / worldWidth) * canvasWidth
MapY = padding + (worldZ / worldDepth) * canvasHeight     // Z is the "north-south" axis
```

The world bounds are derived from `WorldMapData`:

```csharp
float worldWidth  = WorldMap.ChunksWide * ChunkData.Size;  // tiles
float worldDepth  = WorldMap.ChunksHigh * ChunkData.Size;
```

---

## R3 — Region Background

For M0, use a **solid dark background** with a subtle grid to convey scale. Each grid cell represents one chunk (16×16 tiles).

```
┌─────────────────────────────────────┐
│  ·   ·   ·   ·   ·   ·   ·   ·    │
│  ·   ·   ·   ·   ·   ·   ·   ·    │
│  ·   ·   ·   ·   ·   ·   ·   ·    │
│  ·   ·   ·   ·   ·   ·   ·   ·    │
│  ·   ·   ·   ·   ·   ·   ·   ·    │
└─────────────────────────────────────┘
```

Implementation: A `Canvas` panel with a `Border` background color `Color(20, 25, 20, 220)`. Grid lines drawn as thin `Border` elements with `Color(60, 80, 60, 80)`.

**Post-M0 upgrade path:** Replace the grid with a pre-rendered terrain thumbnail generated from heightmap + biome data at region load time.

---

## R4 — Town Markers

Each `CuratedTown` is rendered as a positioned marker on the canvas.

### Marker Sizing by TownCategory

| TownCategory | Marker Size (px) | Label Size (pt) | Color |
|--------------|-------------------|------------------|-------|
| Hamlet | 6 | 12 | `#88AA88` (muted green) |
| Village | 8 | 13 | `#AABB99` |
| Town | 10 | 14 | `#CCCC88` (warm yellow) |
| City | 14 | 16 | `#DDBB66` (amber) |
| Metropolis | 18 | 18 | `#FFCC44` (gold) |

### Marker Shape

A square `Border` element with `CornerRadius` to form a diamond/circle shape, positioned absolutely on the `Canvas` using `Canvas.LeftProperty` / `Canvas.TopProperty`.

### Label

A `TextBlock` positioned adjacent to the marker (offset right + down by half marker size) showing `CuratedTown.GameName`. Labels for Hamlet-sized towns are hidden by default to avoid clutter (shown only on hover/post-M0).

### Destruction Tinting

Apply a red tint to the marker border proportional to `DestructionLevel`:

| DestructionLevel | Border Tint |
|------------------|-------------|
| Pristine | None |
| Light | `Color(180, 160, 140)` |
| Moderate | `Color(180, 120, 80)` |
| Heavy | `Color(180, 80, 50)` |
| Devastated | `Color(180, 40, 40)` |

---

## R5 — Player Position Indicator

### Icon

A diamond shape (`◆`) rendered as a `TextBlock` with a larger font size (20pt), colored bright green `Color(100, 255, 100)`.

### Position

Projected from the live player world position using the same coordinate mapping as town markers:

```csharp
var pos = GetPlayerPosition!();
float mapX = padding + (pos.X / worldWidth) * canvasWidth;
float mapY = padding + (pos.Z / worldDepth) * canvasHeight;
```

Updated every frame while the overlay is visible.

### Pulse Animation

A simple scale pulse driven in `Update()`:

```csharp
float pulse = 1.0f + 0.15f * MathF.Sin((float)Game.UpdateTime.Total.TotalSeconds * 4f);
_playerMarker.RenderTransform = new StripTransform { ScaleX = pulse, ScaleY = pulse };
```

This makes the player marker gently breathe so it's easy to spot among town markers.

---

## R6 — Wire into Program.cs

Follow the same entity-creation pattern used for the HUD and inventory overlays:

```csharp
// Region Map overlay
var regionMapEntity = new Entity("RegionMapOverlay");
var regionMapScript = new RegionMapOverlayScript
{
    InputProvider = inputProvider,
    StateManager  = stateManager,
    Font          = font,
    WorldMap      = worldMap,
    Towns         = curatedTowns,
    GetPlayerPosition = () => playerEntity.Transform.Position
};
regionMapEntity.Add(regionMapScript);
scene.Entities.Add(regionMapEntity);
```

---

## Visual Mockup

```
┌──────────────────────────────────────────────────┐
│                Region Map: Ruïne Noord            │
│                                                    │
│    ·   ·   ·   ·   ·   ·   ·   ·   ·   ·   ·    │
│    ·   ·   ·   ·   ·   ·   ·   ·   ·   ·   ·    │
│    ·   ·   ·   ■ Ashfall Crossing  ·   ·   ·     │
│    ·   ·   ·   ·   ·   ·   ·   ·   ·   ·   ·    │
│    ·   ·   ·   ·   ·   ◆   ·   ·   ·   ·   ·    │
│    ·   ·   ·   ·   ·   ·   ·   ·   ·   ·   ·    │
│    ·   ·  ● Duskhold  ·   ·   ·   ·   ·   ·     │
│    ·   ·   ·   ·   ·   ·   ·   ·   ·   ·   ·    │
│    ·   ·   ·   ·   ·   · ▪ Haven  ·   ·   ·     │
│    ·   ·   ·   ·   ·   ·   ·   ·   ·   ·   ·    │
│                                                    │
│   (M) Close         ● Town  ▪ Village  ◆ You      │
└──────────────────────────────────────────────────┘
```

- `■` = City marker  
- `●` = Town marker  
- `▪` = Village/Hamlet marker  
- `◆` = Player position (pulsing)

---

## Input Behaviour

| State | M Key Behaviour |
|-------|-----------------|
| Normal gameplay | Opens map overlay, pauses input to movement |
| Map overlay open | Closes map overlay, resumes gameplay |
| In dialogue | Ignored (no map during conversations) |
| Game over | Ignored |
| Inventory open | Closes inventory, opens map (or: ignored — TBD) |

While the map overlay is visible, movement and attack inputs are suppressed. Camera input is also suppressed.

---

## Acceptance Criteria

1. Pressing **M** during normal gameplay opens a full-screen region map overlay.
2. Pressing **M** again (or **Escape**) closes the overlay.
3. All towns in the current region are shown as markers at their correct relative positions.
4. Town markers are sized/colored according to `TownCategory`.
5. Town labels display the `GameName` of each town.
6. The player's current position is shown as a pulsing green diamond.
7. The player marker updates position if the overlay stays open while the game updates.
8. The overlay is suppressed during dialogue and game-over states.
9. Movement/attack inputs are blocked while the overlay is open.

---

## Testing

### Unit Tests

- `RegionMapRenderer` coordinate mapping: given known world bounds and town positions, verify projected map coordinates are proportional.
- Marker sizing: verify each `TownCategory` maps to the expected marker size.

### UI Tests (Brinell.Stride)

```
1. Start game → verify map overlay is NOT visible
2. Press M → verify overlay appears (IsVisible == true)
3. Verify town markers are present on the overlay
4. Verify player marker is present
5. Press M → verify overlay closes (IsVisible == false)
6. Enter dialogue → press M → verify overlay does NOT open
```

### Manual Verification

```
1. Load a region with multiple towns of varying sizes
2. Press M — map should appear with correct town layout
3. Walk to a different position, press M again — player marker should reflect new position
4. Verify town labels are readable and don't heavily overlap
5. Verify the grid gives a sense of scale
```
