using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Save;

/// <summary>
/// Tracks auto-save timing and event-driven triggers.
/// Pure logic — the actual save is performed by the caller when ShouldSave is true.
/// </summary>
public sealed class AutoSaveTracker
{
    public const float DefaultIntervalSeconds = 300f; // 5 minutes

    private readonly IEventBus _eventBus;
    private readonly float _intervalSeconds;
    private float _elapsed;
    private bool _pendingSave;
    private bool _enabled;

    /// <summary>Whether auto-save is enabled.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value) _pendingSave = false;
        }
    }

    /// <summary>When true, Tick and TriggerNow are suppressed (e.g., during death/respawn).</summary>
    public bool Paused { get; set; }

    /// <summary>Seconds elapsed since last save.</summary>
    public float Elapsed => _elapsed;

    /// <summary>True if an auto-save should be performed.</summary>
    public bool ShouldSave => _pendingSave;

    /// <summary>Configured interval in seconds.</summary>
    public float IntervalSeconds => _intervalSeconds;

    public AutoSaveTracker(IEventBus eventBus, float intervalSeconds = DefaultIntervalSeconds)
    {
        _eventBus = eventBus;
        _intervalSeconds = intervalSeconds > 0 ? intervalSeconds : DefaultIntervalSeconds;
        _enabled = true;
    }

    /// <summary>
    /// Advances the timer. Sets ShouldSave = true when the interval elapses.
    /// </summary>
    public void Tick(float deltaSec)
    {
        if (!_enabled || Paused || deltaSec <= 0) return;

        _elapsed += deltaSec;
        if (_elapsed >= _intervalSeconds)
        {
            _pendingSave = true;
            _eventBus.Publish(new AutoSaveTriggeredEvent());
        }
    }

    /// <summary>
    /// Triggers an immediate auto-save request (e.g., on zone transition or before quit).
    /// </summary>
    public void TriggerNow()
    {
        if (!_enabled || Paused) return;
        _pendingSave = true;
        _eventBus.Publish(new AutoSaveTriggeredEvent());
    }

    /// <summary>
    /// Acknowledges that the save was performed. Resets the timer and pending flag.
    /// </summary>
    public void Acknowledge()
    {
        _pendingSave = false;
        _elapsed = 0f;
    }
}
