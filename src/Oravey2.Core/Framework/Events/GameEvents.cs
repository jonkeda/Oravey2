using Stride.Core.Mathematics;

namespace Oravey2.Core.Framework.Events;

public readonly record struct GameStateChangedEvent(
    State.GameState OldState,
    State.GameState NewState
) : IGameEvent;

public readonly record struct PlayerMovedEvent(
    Vector3 OldPosition,
    Vector3 NewPosition
) : IGameEvent;

public readonly record struct CameraZoomChangedEvent(
    float OldZoom,
    float NewZoom
) : IGameEvent;

public readonly record struct CameraRotatedEvent(
    float OldYaw,
    float NewYaw
) : IGameEvent;
