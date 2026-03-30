namespace Oravey2.Core.Combat;

public sealed class ActionQueue
{
    private readonly Queue<CombatAction> _queue = new();

    public int Count => _queue.Count;
    public IReadOnlyList<CombatAction> PendingActions => [.. _queue];

    public void Enqueue(CombatAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _queue.Enqueue(action);
    }

    public CombatAction? Dequeue()
        => _queue.Count > 0 ? _queue.Dequeue() : null;

    public CombatAction? Peek()
        => _queue.Count > 0 ? _queue.Peek() : null;

    public void Clear() => _queue.Clear();
}
