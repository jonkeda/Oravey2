using Stride.Core.Mathematics;
using Stride.Input;

namespace Oravey2.Core.Input;

public interface IInputProvider
{
    Vector2 MovementAxis { get; }
    float RotationAxis { get; }
    float ZoomAxis { get; }
    bool IsActionPressed(GameAction action);
    bool IsActionHeld(GameAction action);
    bool IsActionReleased(GameAction action);
    Vector2 PointerScreenPosition { get; }
    float ScrollDelta { get; }
    void Update(InputManager input);
}
