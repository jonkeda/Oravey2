using Stride.Core.Mathematics;
using Stride.Input;

namespace Oravey2.Core.Input;

public sealed class KeyboardMouseInputProvider : IInputProvider
{
    public Vector2 MovementAxis { get; private set; }
    public Vector2 PointerScreenPosition { get; private set; }
    public float ScrollDelta { get; private set; }

    private readonly Dictionary<GameAction, Keys[]> _keyBindings = new()
    {
        { GameAction.MoveUp, [Keys.W, Keys.Up] },
        { GameAction.MoveDown, [Keys.S, Keys.Down] },
        { GameAction.MoveLeft, [Keys.A, Keys.Left] },
        { GameAction.MoveRight, [Keys.D, Keys.Right] },
        { GameAction.Interact, [Keys.E] },
        { GameAction.Attack, [Keys.Space] },
        { GameAction.Pause, [Keys.Escape] },
        { GameAction.Inventory, [Keys.I] },
        { GameAction.RotateCameraLeft, [Keys.Q] },
        { GameAction.RotateCameraRight, [Keys.R] },
    };

    private InputManager? _input;

    public void Update(InputManager input)
    {
        _input = input;

        // Movement axis from WASD/arrows
        var movement = Vector2.Zero;
        if (IsKeyHeld(Keys.W) || IsKeyHeld(Keys.Up)) movement.Y += 1f;
        if (IsKeyHeld(Keys.S) || IsKeyHeld(Keys.Down)) movement.Y -= 1f;
        if (IsKeyHeld(Keys.A) || IsKeyHeld(Keys.Left)) movement.X -= 1f;
        if (IsKeyHeld(Keys.D) || IsKeyHeld(Keys.Right)) movement.X += 1f;

        if (movement.LengthSquared() > 0f)
            movement.Normalize();

        MovementAxis = movement;

        // Mouse position
        PointerScreenPosition = input.MousePosition;

        // Scroll wheel
        ScrollDelta = input.MouseWheelDelta;
    }

    public bool IsActionPressed(GameAction action)
    {
        if (_input == null) return false;

        if (action == GameAction.ZoomIn) return _input.MouseWheelDelta > 0;
        if (action == GameAction.ZoomOut) return _input.MouseWheelDelta < 0;

        if (_keyBindings.TryGetValue(action, out var keys))
            return keys.Any(k => _input.IsKeyPressed(k));

        return false;
    }

    public bool IsActionHeld(GameAction action)
    {
        if (_input == null) return false;

        if (_keyBindings.TryGetValue(action, out var keys))
            return keys.Any(k => _input.IsKeyDown(k));

        return false;
    }

    public bool IsActionReleased(GameAction action)
    {
        if (_input == null) return false;

        if (_keyBindings.TryGetValue(action, out var keys))
            return keys.Any(k => _input.IsKeyReleased(k));

        return false;
    }

    private bool IsKeyHeld(Keys key) => _input?.IsKeyDown(key) ?? false;
}
