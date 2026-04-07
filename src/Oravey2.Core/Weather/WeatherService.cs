using System.Numerics;
using Oravey2.Core.Audio;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Weather;

/// <summary>
/// Comprehensive weather service with intensity, wind, and smooth transition blending.
/// Wraps the simpler <see cref="WeatherProcessor"/> for random state transitions, and
/// adds per-tick intensity ramping and cross-fade logic.
/// </summary>
public sealed class WeatherService
{
    /// <summary>Default transition duration in real seconds.</summary>
    public const float DefaultTransitionDuration = 7f;

    private readonly IEventBus _eventBus;

    private WeatherState _currentType = WeatherState.Clear;
    private WeatherState _targetType = WeatherState.Clear;
    private float _currentIntensity;
    private float _targetIntensity;
    private float _transitionProgress = 1f; // 1 = fully arrived
    private float _transitionDuration = DefaultTransitionDuration;

    private Vector2 _windDirection = Vector2.UnitX;
    private float _windSpeed;

    /// <summary>The active weather type (may still be transitioning from a previous type).</summary>
    public WeatherState CurrentType => _currentType;

    /// <summary>The weather type we are transitioning toward.</summary>
    public WeatherState TargetType => _targetType;

    /// <summary>Current intensity of the active weather (0 = none, 1 = maximum).</summary>
    public float Intensity => _currentIntensity;

    /// <summary>The target intensity for the next weather state.</summary>
    public float TargetIntensity => _targetIntensity;

    /// <summary>Transition progress (0 = just started, 1 = complete).</summary>
    public float TransitionProgress => _transitionProgress;

    /// <summary>Normalised wind direction vector.</summary>
    public Vector2 WindDirection => _windDirection;

    /// <summary>Wind speed in m/s.</summary>
    public float WindSpeed => _windSpeed;

    public WeatherService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Sets the target weather with a new intensity and optional wind parameters.
    /// Begins a cross-fade transition from the current state.
    /// </summary>
    public void SetWeather(WeatherState type, float intensity,
        Vector2? windDirection = null, float windSpeed = 0f,
        float transitionDuration = DefaultTransitionDuration)
    {
        intensity = Math.Clamp(intensity, 0f, 1f);

        if (type == _targetType && MathF.Abs(intensity - _targetIntensity) < 0.001f)
            return;

        _targetType = type;
        _targetIntensity = intensity;
        _transitionProgress = 0f;
        _transitionDuration = transitionDuration > 0 ? transitionDuration : DefaultTransitionDuration;

        if (windDirection.HasValue)
        {
            var dir = windDirection.Value;
            float len = dir.Length();
            _windDirection = len > 0.001f ? dir / len : Vector2.UnitX;
        }
        _windSpeed = Math.Max(0f, windSpeed);
    }

    /// <summary>
    /// Ticks the transition blending. Call each frame with real delta time.
    /// </summary>
    public void Tick(float deltaSec)
    {
        if (deltaSec <= 0) return;

        if (_transitionProgress < 1f)
        {
            _transitionProgress += deltaSec / _transitionDuration;
            if (_transitionProgress >= 1f)
            {
                _transitionProgress = 1f;

                var old = _currentType;
                _currentType = _targetType;
                _currentIntensity = _targetIntensity;

                if (old != _currentType)
                    _eventBus.Publish(new WeatherChangedEvent(old, _currentType));
            }
            else
            {
                // Cross-fade: old intensity fades out, new fades in
                float fadeOut = 1f - _transitionProgress;
                float fadeIn = _transitionProgress;

                if (_currentType == _targetType)
                {
                    // Same type, just changing intensity
                    _currentIntensity = MathF.FusedMultiplyAdd(
                        _targetIntensity - _currentIntensity, fadeIn, _currentIntensity);
                }
                else
                {
                    // Blending between two types: use target intensity scaled by fade-in
                    _currentIntensity = _targetIntensity * fadeIn;
                }
            }
        }
    }

    /// <summary>
    /// Forces weather immediately without transition.
    /// </summary>
    public void ForceWeather(WeatherState type, float intensity)
    {
        intensity = Math.Clamp(intensity, 0f, 1f);

        var old = _currentType;
        _currentType = type;
        _targetType = type;
        _currentIntensity = intensity;
        _targetIntensity = intensity;
        _transitionProgress = 1f;

        if (old != type)
            _eventBus.Publish(new WeatherChangedEvent(old, type));
    }
}
