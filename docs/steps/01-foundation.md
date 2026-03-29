# Step 1 — Foundation

**Goal:** Compilable solution that renders an isometric tile map with a controllable player entity.

---

## Deliverables

1. Solution & project structure (Core, Windows, Assets, Tests)
2. Core framework: EventBus, ServiceLocator, GameState machine
3. Input abstraction layer with keyboard/mouse implementation
4. Isometric camera system (orthographic, ~30° pitch / 45° yaw, smooth follow)
5. Player entity with movement controller
6. Tile map data model and renderer (single chunk, no streaming yet)
7. Windows launcher that boots into the game scene

---

## 1. Solution Structure

```
Oravey2.sln
├── src/
│   ├── Oravey2.Core/              # Shared game logic — targets net8.0
│   │   ├── Oravey2.Core.csproj
│   │   ├── Framework/
│   │   │   ├── Events/
│   │   │   │   ├── IEventBus.cs
│   │   │   │   ├── EventBus.cs
│   │   │   │   └── GameEvents.cs
│   │   │   ├── Services/
│   │   │   │   ├── IServiceLocator.cs
│   │   │   │   └── ServiceLocator.cs
│   │   │   └── State/
│   │   │       ├── GameState.cs
│   │   │       └── GameStateManager.cs
│   │   ├── Input/
│   │   │   ├── GameAction.cs
│   │   │   ├── IInputProvider.cs
│   │   │   └── KeyboardMouseInputProvider.cs
│   │   ├── Camera/
│   │   │   ├── IsometricCameraComponent.cs
│   │   │   └── IsometricCameraProcessor.cs
│   │   ├── Player/
│   │   │   ├── PlayerComponent.cs
│   │   │   └── PlayerMovementProcessor.cs
│   │   └── World/
│   │       ├── TileType.cs
│   │       ├── TileMapData.cs
│   │       ├── TileMapComponent.cs
│   │       └── TileMapProcessor.cs
│   │
│   └── Oravey2.Windows/           # Windows launcher
│       ├── Oravey2.Windows.csproj
│       └── Program.cs
│
├── tests/
│   └── Oravey2.Tests/
│       ├── Oravey2.Tests.csproj
│       └── Framework/
│           ├── EventBusTests.cs
│           └── ServiceLocatorTests.cs
│
└── docs/                           # Already exists
```

---

## 2. Core Framework

### 2.1 EventBus

Simple pub/sub for decoupled communication between systems.

```csharp
// Marker interface for all game events
public interface IGameEvent { }

public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : IGameEvent;
    void Unsubscribe<T>(Action<T> handler) where T : IGameEvent;
    void Publish<T>(T gameEvent) where T : IGameEvent;
}
```

- Dictionary<Type, List<Delegate>> internally.
- No async, no queuing — immediate dispatch (sufficient for single-player).
- Thread-safe not required (single game thread).

### 2.2 ServiceLocator

Minimal registration and retrieval of global services.

```csharp
public interface IServiceLocator
{
    void Register<T>(T service) where T : class;
    T Get<T>() where T : class;
    bool TryGet<T>(out T service) where T : class;
}
```

- Backed by `Dictionary<Type, object>`.
- Registered at startup: `IEventBus`, `IInputProvider`, and future services.
- Singleton static accessor: `Services.Instance`.

### 2.3 GameState

Enum-based state plus a manager that fires transition events.

```csharp
public enum GameState
{
    Loading,
    Exploring,
    InCombat,
    InDialogue,
    InMenu,
    Paused
}
```

- `GameStateManager` tracks current state, validates transitions, publishes `GameStateChangedEvent`.

---

## 3. Input Abstraction

### 3.1 GameAction Enum

```csharp
public enum GameAction
{
    MoveUp, MoveDown, MoveLeft, MoveRight,
    Interact, Attack, Pause, Inventory,
    RotateCameraLeft, RotateCameraRight,
    ZoomIn, ZoomOut
}
```

### 3.2 IInputProvider

```csharp
public interface IInputProvider
{
    Vector2 MovementAxis { get; }
    bool IsActionPressed(GameAction action);
    bool IsActionHeld(GameAction action);
    bool IsActionReleased(GameAction action);
    Vector2 PointerScreenPosition { get; }
    void Update(InputManager strideInput);
}
```

### 3.3 KeyboardMouseInputProvider

- Maps WASD / arrow keys → MovementAxis.
- Maps E → Interact, Space → Attack, Esc → Pause, I → Inventory.
- Q/E → RotateCameraLeft/Right, scroll → ZoomIn/ZoomOut.
- Reads pointer position from mouse.

---

## 4. Isometric Camera

### Parameters

| Parameter | Default | Notes |
|-----------|---------|-------|
| Pitch | 30° | Angle from horizontal |
| Yaw | 45° | Rotation around Y axis |
| Distance | 20 units | Orthographic size proxy |
| Follow Smoothing | 5.0 | Lerp speed per second |
| Deadzone | 0.5 units | Target must move this far before camera moves |
| Zoom Min/Max | 10 / 40 | Orthographic size range |
| Rotation Snap | 90° | Discrete yaw increments |

### IsometricCameraComponent

Stores camera config. Attached to camera entity.

### IsometricCameraProcessor

Stride `EntityProcessor` that runs each frame:
1. Read target entity position (player).
2. Apply deadzone check.
3. Lerp camera position toward target offset by pitch/yaw/distance.
4. Handle zoom input (clamp to min/max).
5. Handle rotation input (snap yaw in 90° increments).
6. Set OrthographicSize on the camera component.

---

## 5. Player

### PlayerComponent

```csharp
public class PlayerComponent : EntityComponent
{
    public float MoveSpeed { get; set; } = 5.0f;
}
```

### PlayerMovementProcessor

- Reads `IInputProvider.MovementAxis`.
- Converts screen-space input to isometric world-space movement (rotate by camera yaw).
- Applies velocity to entity transform: `entity.Transform.Position += worldDir * speed * dt`.
- Publishes `PlayerMovedEvent` for camera and other systems.

### Isometric Movement Conversion

```
worldX = inputX * cos(yaw) - inputY * sin(yaw)
worldZ = inputX * sin(yaw) + inputY * cos(yaw)
```

---

## 6. Tile Map

### Constants

| Constant | Value |
|----------|-------|
| Tile Size | 1.0 × 1.0 world units |
| Chunk Size | 16 × 16 tiles |
| Initial Map | 1 chunk (16×16) |

### TileType Enum

```csharp
public enum TileType : byte
{
    Empty = 0,
    Ground = 1,
    Road = 2,
    Rubble = 3,
    Water = 4,
    Wall = 5
}
```

### TileMapData

- `TileType[,]` 2D array.
- `int Width`, `int Height`.
- Factory method: `TileMapData.CreateDefault(int w, int h)` fills with Ground + some random Rubble.

### TileMapComponent

Attached to a root entity. Holds reference to `TileMapData`.

### TileMapProcessor

On initialization:
1. Read `TileMapData` from component.
2. For each tile, instantiate or pool a visual prefab/primitive at `(x, 0, y)`.
3. Assign material/color based on `TileType`.

For this step, tiles are **flat colored quads** (procedural mesh or Stride primitives). Real art comes later.

---

## 7. Windows Launcher

```csharp
static class Program
{
    static void Main(string[] args)
    {
        using var game = new Oravey2Game();
        game.Run();
    }
}
```

### Oravey2Game : Game

Custom `Game` subclass:
1. In `BeginRun()` — register services, create scene, spawn camera + player + tile map entities.
2. Registers processors: `IsometricCameraProcessor`, `PlayerMovementProcessor`, `TileMapProcessor`.

---

## 8. Verification Criteria

| # | Criterion | How to Verify |
|---|-----------|---------------|
| 1 | Solution builds without errors | `dotnet build` succeeds |
| 2 | Windows app launches and renders a tile grid | Visual inspection |
| 3 | Player entity visible and controllable with WASD | Move in all 4 isometric directions |
| 4 | Camera follows player smoothly | Walk to edges of map, camera tracks |
| 5 | Camera zoom works | Scroll wheel changes visible area |
| 6 | Camera rotation works | Q/E snaps view 90° |
| 7 | EventBus unit tests pass | `dotnet test` |
| 8 | ServiceLocator unit tests pass | `dotnet test` |
