using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.UI;

/// <summary>
/// Manages a modal screen stack. The topmost screen receives input.
/// Pure logic — Stride screens register themselves and render independently.
/// </summary>
public sealed class ScreenManager
{
    private readonly Stack<ScreenId> _stack = new();
    private readonly IEventBus _eventBus;

    public ScreenId ActiveScreen => _stack.Count > 0 ? _stack.Peek() : ScreenId.None;
    public int Count => _stack.Count;
    public IReadOnlyList<ScreenId> Stack => _stack.Reverse().ToList();

    public ScreenManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Pushes a screen onto the modal stack.
    /// </summary>
    public void Push(ScreenId screen)
    {
        if (screen == ScreenId.None) return;
        _stack.Push(screen);
        _eventBus.Publish(new ScreenPushedEvent(screen));
    }

    /// <summary>
    /// Pops the topmost screen. Returns the popped screen, or None if stack empty.
    /// </summary>
    public ScreenId Pop()
    {
        if (_stack.Count == 0) return ScreenId.None;
        var screen = _stack.Pop();
        _eventBus.Publish(new ScreenPoppedEvent(screen));
        return screen;
    }

    /// <summary>
    /// Replaces the topmost screen. If stack is empty, pushes instead.
    /// </summary>
    public void Replace(ScreenId screen)
    {
        if (screen == ScreenId.None) return;
        if (_stack.Count > 0)
        {
            var old = _stack.Pop();
            _eventBus.Publish(new ScreenPoppedEvent(old));
        }
        _stack.Push(screen);
        _eventBus.Publish(new ScreenPushedEvent(screen));
    }

    /// <summary>
    /// Clears all screens from the stack (e.g., returning to gameplay).
    /// Publishes pop events for each.
    /// </summary>
    public void Clear()
    {
        while (_stack.Count > 0)
            Pop();
    }

    /// <summary>
    /// Checks if a specific screen is anywhere in the stack.
    /// </summary>
    public bool Contains(ScreenId screen)
        => _stack.Contains(screen);
}
