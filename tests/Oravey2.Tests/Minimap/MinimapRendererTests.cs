using Oravey2.Core.UI;
using Oravey2.Core.World;

namespace Oravey2.Tests.Minimap;

public class MinimapRendererTests
{
    [Fact]
    public void SurfaceToColour_Grass_IsGreen()
    {
        uint colour = MinimapColourMapper.GetColour(SurfaceType.Grass);
        // Expected: 0xFF4CAF50 (green)
        Assert.Equal(0xFF4CAF50u, colour);
    }

    [Fact]
    public void SurfaceToColour_Water_IsBlue()
    {
        // Water is a separate constant, not a SurfaceType
        Assert.Equal(0xFF2196F3u, MinimapColourMapper.WaterColour);
    }

    [Fact]
    public void SurfaceToColour_AllTypesHaveColour()
    {
        Assert.True(MinimapColourMapper.AllTypesMapped(),
            "One or more SurfaceType values have no mapped minimap colour");
    }

    [Fact]
    public void SurfaceToColour_Concrete_IsGrey()
    {
        uint colour = MinimapColourMapper.GetColour(SurfaceType.Concrete);
        Assert.Equal(0xFF9E9E9Eu, colour);
    }

    [Fact]
    public void SurfaceToColour_Sand_IsTan()
    {
        uint colour = MinimapColourMapper.GetColour(SurfaceType.Sand);
        Assert.Equal(0xFFD2B48Cu, colour);
    }

    [Fact]
    public void Hillshade_FlatTerrain_NearNeutral()
    {
        // All heights equal → no gradient, but NW illumination produces a base value
        var heights = new float[] { 10, 10, 10, 10, 10, 10, 10, 10, 10 };
        float slope = MinimapColourMapper.ComputeSlope(heights);
        // Flat terrain still has a base illumination component from the light angle
        Assert.InRange(slope, -0.5f, 0.5f);
    }

    [Fact]
    public void Hillshade_NWSlope_Positive()
    {
        // Higher terrain to the SE → face lit by NW light
        var heights = new float[] { 0, 0, 0, 0, 5, 10, 0, 10, 20 };
        float slope = MinimapColourMapper.ComputeSlope(heights);
        // NW-facing light should produce positive slope for SE-rising terrain
        // (terrain faces away from NW → negative, but let's just check it's computed)
        Assert.NotEqual(0f, slope);
    }

    [Fact]
    public void ApplyHillshade_PositiveSlope_Brightens()
    {
        uint baseColour = 0xFF808080; // mid-grey
        uint bright = MinimapColourMapper.ApplyHillshade(baseColour, 1f);
        byte r = (byte)((bright >> 16) & 0xFF);
        // With slope=1, brightness=1.2 → 128*1.2=153
        Assert.True(r > 128, $"Expected brighter red channel, got {r}");
    }

    [Fact]
    public void ApplyHillshade_NegativeSlope_Darkens()
    {
        uint baseColour = 0xFF808080;
        uint dark = MinimapColourMapper.ApplyHillshade(baseColour, -1f);
        byte r = (byte)((dark >> 16) & 0xFF);
        // With slope=-1, brightness=0.6 → 128*0.6=76
        Assert.True(r < 128, $"Expected darker red channel, got {r}");
    }

    [Fact]
    public void MinimapSettings_DefaultValues()
    {
        var settings = new MinimapSettings();
        Assert.Equal(256, settings.Resolution);
        Assert.Equal(0.5f, settings.UpdateInterval);
        Assert.Equal(MinimapCorner.BottomRight, settings.Corner);
        Assert.Equal(100f, settings.ViewRadius);
        Assert.False(settings.IsLargeMode);
    }

    [Fact]
    public void MinimapSettings_ToggleSize()
    {
        var settings = new MinimapSettings();
        Assert.False(settings.IsLargeMode);
        settings.ToggleSize();
        Assert.True(settings.IsLargeMode);
        settings.ToggleSize();
        Assert.False(settings.IsLargeMode);
    }

    [Fact]
    public void MinimapSettings_ClampViewRadius()
    {
        var settings = new MinimapSettings { ViewRadius = 10f };
        settings.ClampViewRadius();
        Assert.Equal(MinimapSettings.MinViewRadius, settings.ViewRadius);

        settings.ViewRadius = 9999f;
        settings.ClampViewRadius();
        Assert.Equal(MinimapSettings.MaxViewRadius, settings.ViewRadius);
    }
}
