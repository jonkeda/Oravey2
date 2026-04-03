using Oravey2.Core.World.Rendering;

namespace Oravey2.Tests.Rendering;

public class QualitySettingsTests
{
    [Fact]
    public void LowPreset_SubTileOff_DetailDensityZero()
    {
        var settings = QualitySettings.FromPreset(QualityPreset.Low);
        Assert.Equal(QualityPreset.Low, settings.Preset);
        Assert.False(settings.SubTileAssembly);
        Assert.False(settings.EdgeJitter);
        Assert.Equal(0f, settings.DetailDensity);
        Assert.Equal(0f, settings.DetailRange);
        Assert.Equal(1, settings.LodRings);
    }

    [Fact]
    public void MediumPreset_SubTileOn_DetailDensityHalf()
    {
        var settings = QualitySettings.FromPreset(QualityPreset.Medium);
        Assert.Equal(QualityPreset.Medium, settings.Preset);
        Assert.True(settings.SubTileAssembly);
        Assert.True(settings.EdgeJitter);
        Assert.Equal(0.5f, settings.DetailDensity);
        Assert.Equal(10f, settings.DetailRange);
        Assert.Equal(2, settings.LodRings);
    }

    [Fact]
    public void HighPreset_EverythingOn_DetailDensityFull()
    {
        var settings = QualitySettings.FromPreset(QualityPreset.High);
        Assert.Equal(QualityPreset.High, settings.Preset);
        Assert.True(settings.SubTileAssembly);
        Assert.True(settings.EdgeJitter);
        Assert.Equal(1.0f, settings.DetailDensity);
        Assert.Equal(20f, settings.DetailRange);
        Assert.Equal(3, settings.LodRings);
    }

    [Fact]
    public void DefaultSettings_AreMedium()
    {
        var settings = new QualitySettings();
        Assert.Equal(QualityPreset.Medium, settings.Preset);
        Assert.True(settings.SubTileAssembly);
    }
}
