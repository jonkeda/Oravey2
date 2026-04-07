using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Audio;

/// <summary>
/// Manages random weather transitions with configurable timing and weighted probabilities.
/// Pure logic — Stride scripts read Current and apply VFX/audio accordingly.
/// </summary>
public sealed class WeatherProcessor
{
    public const float DefaultMinDuration = 120f;  // 2 min real
    public const float DefaultMaxDuration = 600f;  // 10 min real

    private static readonly Dictionary<WeatherState, (WeatherState state, int weight)[]> TransitionWeights = new()
    {
        [WeatherState.Clear] = [
            (WeatherState.Clear, 40), (WeatherState.Rain, 20), (WeatherState.Snow, 10),
            (WeatherState.Foggy, 10), (WeatherState.DustStorm, 10),
            (WeatherState.RadiationFog, 5), (WeatherState.AcidRain, 5)
        ],
        [WeatherState.Rain] = [
            (WeatherState.Clear, 30), (WeatherState.Rain, 30), (WeatherState.Snow, 5),
            (WeatherState.Foggy, 15), (WeatherState.DustStorm, 5),
            (WeatherState.RadiationFog, 5), (WeatherState.AcidRain, 10)
        ],
        [WeatherState.Snow] = [
            (WeatherState.Clear, 30), (WeatherState.Rain, 5), (WeatherState.Snow, 35),
            (WeatherState.Foggy, 15), (WeatherState.DustStorm, 5),
            (WeatherState.RadiationFog, 5), (WeatherState.AcidRain, 5)
        ],
        [WeatherState.Foggy] = [
            (WeatherState.Clear, 30), (WeatherState.Rain, 15), (WeatherState.Snow, 10),
            (WeatherState.Foggy, 20), (WeatherState.DustStorm, 10),
            (WeatherState.RadiationFog, 10), (WeatherState.AcidRain, 5)
        ],
        [WeatherState.DustStorm] = [
            (WeatherState.Clear, 35), (WeatherState.Rain, 5), (WeatherState.Snow, 5),
            (WeatherState.Foggy, 15), (WeatherState.DustStorm, 25),
            (WeatherState.RadiationFog, 10), (WeatherState.AcidRain, 5)
        ],
        [WeatherState.RadiationFog] = [
            (WeatherState.Clear, 35), (WeatherState.Rain, 10), (WeatherState.Snow, 5),
            (WeatherState.Foggy, 15), (WeatherState.DustStorm, 10),
            (WeatherState.RadiationFog, 20), (WeatherState.AcidRain, 5)
        ],
        [WeatherState.AcidRain] = [
            (WeatherState.Clear, 30), (WeatherState.Rain, 15), (WeatherState.Snow, 5),
            (WeatherState.Foggy, 15), (WeatherState.DustStorm, 10),
            (WeatherState.RadiationFog, 10), (WeatherState.AcidRain, 15)
        ],
    };

    private readonly IEventBus _eventBus;
    private readonly Random _random;
    private readonly float _minDuration;
    private readonly float _maxDuration;

    private WeatherState _current;
    private float _timeRemaining;

    /// <summary>Current weather state.</summary>
    public WeatherState Current => _current;

    /// <summary>Seconds remaining before the next weather transition check.</summary>
    public float TimeRemaining => _timeRemaining;

    public WeatherProcessor(IEventBus eventBus,
        float minDuration = DefaultMinDuration,
        float maxDuration = DefaultMaxDuration,
        Random? random = null)
    {
        _eventBus = eventBus;
        _minDuration = minDuration > 0 ? minDuration : DefaultMinDuration;
        _maxDuration = maxDuration > minDuration ? maxDuration : minDuration + 1f;
        _random = random ?? new Random();
        _current = WeatherState.Clear;
        _timeRemaining = NextDuration();
    }

    /// <summary>
    /// Advances the weather timer. When the timer expires, picks a new weather state
    /// using weighted random selection and publishes WeatherChangedEvent if it differs.
    /// </summary>
    public void Tick(float deltaSec)
    {
        if (deltaSec <= 0) return;

        _timeRemaining -= deltaSec;
        if (_timeRemaining <= 0)
        {
            var next = PickNextState();
            if (next != _current)
            {
                var old = _current;
                _current = next;
                _eventBus.Publish(new WeatherChangedEvent(old, next));
            }
            _timeRemaining = NextDuration();
        }
    }

    /// <summary>
    /// Forces an immediate weather transition. Publishes WeatherChangedEvent if the state differs.
    /// Resets the timer.
    /// </summary>
    public void ForceWeather(WeatherState state)
    {
        if (state == _current) return;
        var old = _current;
        _current = state;
        _timeRemaining = NextDuration();
        _eventBus.Publish(new WeatherChangedEvent(old, state));
    }

    private float NextDuration()
        => _minDuration + (float)_random.NextDouble() * (_maxDuration - _minDuration);

    private WeatherState PickNextState()
    {
        var weights = TransitionWeights[_current];
        int total = 0;
        foreach (var (_, w) in weights) total += w;

        int roll = _random.Next(total);
        int cumulative = 0;
        foreach (var (state, weight) in weights)
        {
            cumulative += weight;
            if (roll < cumulative) return state;
        }
        return WeatherState.Clear;
    }
}
