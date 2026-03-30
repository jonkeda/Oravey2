using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.World;

/// <summary>
/// Advances in-game time and tracks day phase transitions.
/// Pure logic — no Stride dependencies. A SyncScript wrapper calls Tick() each frame.
/// </summary>
public sealed class DayNightCycleProcessor
{
    // Phase boundaries (in-game hours)
    public const float DawnStart = 6.0f;
    public const float DayStart = 7.0f;
    public const float DuskStart = 20.0f;
    public const float NightStart = 21.0f;

    // Timing
    public const float DefaultRealSecondsPerHour = 120f; // 48 min real = 24h game

    private readonly IEventBus _eventBus;

    private float _inGameHour;
    private DayPhase _currentPhase;
    private float _realSecondsPerHour;

    /// <summary>Current in-game hour (0.0–24.0, wraps).</summary>
    public float InGameHour => _inGameHour;

    /// <summary>Current day phase.</summary>
    public DayPhase CurrentPhase => _currentPhase;

    /// <summary>Real seconds per in-game hour. Default 120 (48 min real = full day).</summary>
    public float RealSecondsPerHour
    {
        get => _realSecondsPerHour;
        set => _realSecondsPerHour = value > 0 ? value : DefaultRealSecondsPerHour;
    }

    public DayNightCycleProcessor(IEventBus eventBus, float startHour = 8.0f,
        float realSecondsPerHour = DefaultRealSecondsPerHour)
    {
        _eventBus = eventBus;
        _realSecondsPerHour = realSecondsPerHour;
        _inGameHour = Math.Clamp(startHour, 0f, 24f);
        _currentPhase = GetPhase(_inGameHour);
    }

    /// <summary>
    /// Advances time by the given real-time delta (in seconds).
    /// Publishes DayPhaseChangedEvent when crossing a boundary.
    /// </summary>
    public void Tick(float realDeltaSeconds)
    {
        if (realDeltaSeconds <= 0) return;

        float hoursElapsed = realDeltaSeconds / _realSecondsPerHour;
        _inGameHour += hoursElapsed;

        // Wrap around midnight
        while (_inGameHour >= 24f)
            _inGameHour -= 24f;

        var newPhase = GetPhase(_inGameHour);
        if (newPhase != _currentPhase)
        {
            var old = _currentPhase;
            _currentPhase = newPhase;
            _eventBus.Publish(new DayPhaseChangedEvent(old, newPhase));
        }
    }

    /// <summary>
    /// Advances time by a given number of in-game hours. Used for fast-travel, sleeping, etc.
    /// May fire multiple phase-change events if crossing several boundaries.
    /// </summary>
    public void AdvanceHours(float inGameHours)
    {
        if (inGameHours <= 0) return;

        // Step in small increments to detect each phase boundary
        const float step = 0.25f; // 15-minute steps
        float remaining = inGameHours;

        while (remaining > 0)
        {
            float advance = Math.Min(remaining, step);
            float realEquivalent = advance * _realSecondsPerHour;
            Tick(realEquivalent);
            remaining -= advance;
        }
    }

    /// <summary>
    /// Sets in-game time directly. Publishes phase change if applicable.
    /// </summary>
    public void SetTime(float hour)
    {
        _inGameHour = Math.Clamp(hour, 0f, 24f);
        var newPhase = GetPhase(_inGameHour);
        if (newPhase != _currentPhase)
        {
            var old = _currentPhase;
            _currentPhase = newPhase;
            _eventBus.Publish(new DayPhaseChangedEvent(old, newPhase));
        }
    }

    /// <summary>
    /// Determines the day phase from an in-game hour.
    /// </summary>
    public static DayPhase GetPhase(float hour)
    {
        return hour switch
        {
            >= DawnStart and < DayStart => DayPhase.Dawn,
            >= DayStart and < DuskStart => DayPhase.Day,
            >= DuskStart and < NightStart => DayPhase.Dusk,
            _ => DayPhase.Night
        };
    }
}
