using System.Numerics;
using Oravey2.Core.Audio;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Weather;

namespace Oravey2.Tests.Weather;

public class WeatherServiceTests
{
    [Fact]
    public void DefaultWeather_IsClear()
    {
        var svc = new WeatherService(new EventBus());

        Assert.Equal(WeatherState.Clear, svc.CurrentType);
        Assert.Equal(0f, svc.Intensity);
    }

    [Fact]
    public void SetWeather_Rain_IntensityRamps()
    {
        var svc = new WeatherService(new EventBus());

        svc.SetWeather(WeatherState.Rain, 0.8f);

        // Before any ticks, intensity is still 0 (transition hasn't started)
        Assert.Equal(0f, svc.TransitionProgress);

        // Tick halfway through the transition
        svc.Tick(WeatherService.DefaultTransitionDuration / 2f);

        Assert.True(svc.Intensity > 0f, "Intensity should increase during transition");
        Assert.True(svc.Intensity < 0.8f, "Intensity should not yet be at target");
        Assert.True(svc.TransitionProgress > 0.4f && svc.TransitionProgress < 0.6f);
    }

    [Fact]
    public void Transition_OldTypeFadesOut()
    {
        var bus = new EventBus();
        var svc = new WeatherService(bus);

        // Start at Rain with full intensity
        svc.ForceWeather(WeatherState.Rain, 1.0f);
        Assert.Equal(1.0f, svc.Intensity);

        // Start transitioning to Snow
        svc.SetWeather(WeatherState.Snow, 0.8f);

        // Tick partially — intensity should drop from 1.0 (Rain fading out)
        svc.Tick(WeatherService.DefaultTransitionDuration * 0.3f);
        Assert.True(svc.Intensity < 1.0f,
            $"Old weather intensity should fade during transition, got {svc.Intensity}");
    }

    [Fact]
    public void WindDirection_NormalisedVector()
    {
        var svc = new WeatherService(new EventBus());

        svc.SetWeather(WeatherState.DustStorm, 0.5f,
            windDirection: new Vector2(3f, 4f), windSpeed: 15f);

        float length = svc.WindDirection.Length();
        Assert.Equal(1f, length, 0.001f);
    }

    [Fact]
    public void Transition_CompletePublishesEvent()
    {
        var bus = new EventBus();
        var svc = new WeatherService(bus);
        WeatherChangedEvent? captured = null;
        bus.Subscribe<WeatherChangedEvent>(e => captured = e);

        svc.SetWeather(WeatherState.Rain, 0.5f);

        // Complete the transition
        svc.Tick(WeatherService.DefaultTransitionDuration + 1f);

        Assert.NotNull(captured);
        Assert.Equal(WeatherState.Clear, captured.Value.OldState);
        Assert.Equal(WeatherState.Rain, captured.Value.NewState);
    }

    [Fact]
    public void ForceWeather_SetsImmediately()
    {
        var svc = new WeatherService(new EventBus());

        svc.ForceWeather(WeatherState.Snow, 0.9f);

        Assert.Equal(WeatherState.Snow, svc.CurrentType);
        Assert.Equal(0.9f, svc.Intensity, 0.001f);
        Assert.Equal(1f, svc.TransitionProgress);
    }

    [Fact]
    public void SetWeather_SameTypeAndIntensity_NoOp()
    {
        var svc = new WeatherService(new EventBus());

        svc.ForceWeather(WeatherState.Rain, 0.5f);
        svc.SetWeather(WeatherState.Rain, 0.5f);

        // Should remain at full transition (no new transition started)
        Assert.Equal(1f, svc.TransitionProgress);
    }
}
