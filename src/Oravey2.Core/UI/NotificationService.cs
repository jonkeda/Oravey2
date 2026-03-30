namespace Oravey2.Core.UI;

/// <summary>
/// Queues timed notifications for the HUD to display (e.g., "Item picked up", "Quest updated").
/// </summary>
public sealed class NotificationService
{
    public sealed record Notification(string Message, float DurationSeconds, float TimeRemaining);

    private readonly List<(string message, float duration, float remaining)> _active = new();
    private readonly Queue<(string message, float duration)> _pending = new();

    /// <summary>Maximum notifications displayed at once.</summary>
    public int MaxVisible { get; }

    public NotificationService(int maxVisible = 5)
    {
        MaxVisible = maxVisible;
    }

    /// <summary>
    /// Enqueues a notification. If there's room, it goes active immediately.
    /// </summary>
    public void Add(string message, float durationSeconds = 3f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (durationSeconds <= 0) durationSeconds = 3f;

        if (_active.Count < MaxVisible)
            _active.Add((message, durationSeconds, durationSeconds));
        else
            _pending.Enqueue((message, durationSeconds));
    }

    /// <summary>
    /// Ticks all active notifications, removing expired ones and promoting pending.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0) return;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var (msg, dur, rem) = _active[i];
            rem -= deltaSeconds;
            if (rem <= 0)
                _active.RemoveAt(i);
            else
                _active[i] = (msg, dur, rem);
        }

        // Promote pending
        while (_active.Count < MaxVisible && _pending.Count > 0)
        {
            var (msg, dur) = _pending.Dequeue();
            _active.Add((msg, dur, dur));
        }
    }

    /// <summary>
    /// Returns currently visible notifications as read-only snapshots.
    /// </summary>
    public IReadOnlyList<Notification> GetActive()
        => _active.Select(a => new Notification(a.message, a.duration, a.remaining)).ToList();

    /// <summary>Number of pending (not yet shown) notifications.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Removes all active and pending notifications.
    /// </summary>
    public void Clear()
    {
        _active.Clear();
        _pending.Clear();
    }
}
