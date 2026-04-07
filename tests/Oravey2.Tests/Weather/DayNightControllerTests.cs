using Oravey2.Core.Weather;

namespace Oravey2.Tests.Weather;

public class DayNightControllerTests
{
    [Fact]
    public void Noon_SunAtHighAngle()
    {
        var state = DayNightController.ComputeLighting(12f);

        Assert.True(state.SunElevationDeg > 60f,
            $"Expected sun elevation > 60° at noon, got {state.SunElevationDeg:F1}°");
    }

    [Fact]
    public void Midnight_LowAmbient_MoonDirection()
    {
        var state = DayNightController.ComputeLighting(0f);

        // Night: sun below horizon
        Assert.True(state.SunElevationDeg < 0f,
            $"Expected negative elevation at midnight, got {state.SunElevationDeg:F1}°");

        // Blue ambient
        Assert.True(state.AmbientColour.Z > state.AmbientColour.X,
            "Night ambient should be blue-dominant");

        // Low shadow
        Assert.True(state.ShadowIntensity < 0.3f,
            $"Shadow intensity should be low at night, got {state.ShadowIntensity:F2}");
    }

    [Fact]
    public void Dawn_WarmColour()
    {
        var state = DayNightController.ComputeLighting(6f);

        // Dawn colour should be warm (R > G > B)
        Assert.True(state.SunColour.X > state.SunColour.Z,
            $"Dawn sun should be warm (R > B); got R={state.SunColour.X:F2} B={state.SunColour.Z:F2}");
    }

    [Fact]
    public void TimeAdvance_CyclesCorrectly()
    {
        // Check that the full 24h cycle produces expected progression
        float prevElevation = DayNightController.GetSunElevation(0f);
        bool foundHighNoon = false;
        bool foundNight = false;

        for (float h = 0; h < 48f; h += 1f)
        {
            float el = DayNightController.GetSunElevation(h);
            if (el > 60f) foundHighNoon = true;
            if (el < 0f) foundNight = true;
        }

        Assert.True(foundHighNoon, "Should reach high noon during 48h cycle");
        Assert.True(foundNight, "Should reach night during 48h cycle");
    }

    [Fact]
    public void Dusk_WarmColour()
    {
        var state = DayNightController.ComputeLighting(18f);

        // Dusk colour should be warm red-orange
        Assert.True(state.SunColour.X > state.SunColour.Z,
            "Dusk should have warm colour (R > B)");
    }

    [Fact]
    public void Hour24_WrapsToMidnight()
    {
        var at24 = DayNightController.ComputeLighting(24f);
        var at0 = DayNightController.ComputeLighting(0f);

        Assert.Equal(at0.SunElevationDeg, at24.SunElevationDeg, 0.01f);
    }
}
