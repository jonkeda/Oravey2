using Stride.Core.Mathematics;
using Stride.Input;

namespace Oravey2.Core.Input;

public sealed class KeyboardMouseInputProvider : IInputProvider
{
    public Vector2 MovementAxis { get; private set; }
    public float RotationAxis { get; private set; }
    public float ZoomAxis { get; private set; }
    public Vector2 PointerScreenPosition { get; private set; }
    public float ScrollDelta { get; private set; }

    private readonly Dictionary<GameAction, Keys[]> _keyBindings = new()
    {
        { GameAction.MoveUp, [Keys.W, Keys.Up] },
        { GameAction.MoveDown, [Keys.S, Keys.Down] },
        { GameAction.MoveLeft, [Keys.A, Keys.Left] },
        { GameAction.MoveRight, [Keys.D, Keys.Right] },
        { GameAction.Interact, [Keys.F] },
        { GameAction.Attack, [Keys.Space] },
        { GameAction.Pause, [Keys.Escape] },
        { GameAction.Inventory, [Keys.I] },
        { GameAction.ToggleFullscreen, [Keys.F11] },
        { GameAction.QuickSave, [Keys.F5] },
        { GameAction.QuickLoad, [Keys.F9] },
        { GameAction.DialogueChoice1, [Keys.D1] },
        { GameAction.DialogueChoice2, [Keys.D2] },
        { GameAction.DialogueChoice3, [Keys.D3] },
        { GameAction.DialogueChoice4, [Keys.D4] },
        { GameAction.OpenJournal, [Keys.J] },
        { GameAction.Info, [Keys.N] },
    };

    private InputManager? _input;
    private readonly HashSet<GameAction> _pressedThisFrame = [];

    public void Update(InputManager input)
    {
        _input = input;

        // Snapshot per-frame pressed actions (IsKeyPressed is transient)
        _pressedThisFrame.Clear();
        foreach (var (action, keys) in _keyBindings)
        {
            if (keys.Any(k => input.IsKeyPressed(k)))
                _pressedThisFrame.Add(action);
        }

        // Movement axis from WASD/arrows (held state)
        var movement = Vector2.Zero;
        if (IsKeyHeld(Keys.W) || IsKeyHeld(Keys.Up)) movement.Y += 1f;
        if (IsKeyHeld(Keys.S) || IsKeyHeld(Keys.Down)) movement.Y -= 1f;
        if (IsKeyHeld(Keys.A) || IsKeyHeld(Keys.Left)) movement.X -= 1f;
        if (IsKeyHeld(Keys.D) || IsKeyHeld(Keys.Right)) movement.X += 1f;

        if (movement.LengthSquared() > 0f)
            movement.Normalize();

        MovementAxis = movement;

        // Rotation axis from Q/E (held state)
        float rot = 0f;
        if (IsKeyHeld(Keys.Q)) rot -= 1f;
        if (IsKeyHeld(Keys.E)) rot += 1f;
        RotationAxis = rot;

        // Zoom axis from keyboard (held) + scroll
        float zoom = 0f;
        if (IsKeyHeld(Keys.OemPlus) || IsKeyHeld(Keys.PageUp)) zoom -= 1f;
        if (IsKeyHeld(Keys.OemMinus) || IsKeyHeld(Keys.PageDown)) zoom += 1f;
        if (input.MouseWheelDelta != 0) zoom -= input.MouseWheelDelta;
        ZoomAxis = zoom;

        // Mouse position
        PointerScreenPosition = input.MousePosition;

        // Scroll wheel (raw)
        ScrollDelta = input.MouseWheelDelta;
    }

    public bool IsActionPressed(GameAction action)
    {
        if (_input == null) return false;
        return _pressedThisFrame.Contains(action);
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
