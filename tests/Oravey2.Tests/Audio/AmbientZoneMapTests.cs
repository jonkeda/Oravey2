using Oravey2.Core.Audio;
using Oravey2.Core.World;

namespace Oravey2.Tests.Audio;

public class AmbientZoneMapTests
{
    [Fact]
    public void RuinedCityReturnsTwoIds()
    {
        var ids = AmbientZoneMap.GetAmbientIds(BiomeType.RuinedCity);
        Assert.Equal(2, ids.Length);
        Assert.Contains("amb_wind_urban", ids);
        Assert.Contains("amb_creaking_metal", ids);
    }

    [Fact]
    public void WastelandReturnsTwoIds()
    {
        var ids = AmbientZoneMap.GetAmbientIds(BiomeType.Wasteland);
        Assert.Equal(2, ids.Length);
        Assert.Contains("amb_wind_open", ids);
        Assert.Contains("amb_distant_thunder", ids);
    }

    [Fact]
    public void BunkerReturnsTwoIds()
    {
        var ids = AmbientZoneMap.GetAmbientIds(BiomeType.Bunker);
        Assert.Equal(2, ids.Length);
        Assert.Contains("amb_hum_mechanical", ids);
        Assert.Contains("amb_dripping_water", ids);
    }

    [Fact]
    public void SettlementReturnsTwoIds()
    {
        var ids = AmbientZoneMap.GetAmbientIds(BiomeType.Settlement);
        Assert.Equal(2, ids.Length);
        Assert.Contains("amb_crowd_murmur", ids);
        Assert.Contains("amb_campfire_crackle", ids);
    }

    [Fact]
    public void IndustrialReturnsTwoIds()
    {
        var ids = AmbientZoneMap.GetAmbientIds(BiomeType.Industrial);
        Assert.Equal(2, ids.Length);
        Assert.Contains("amb_machinery_hum", ids);
        Assert.Contains("amb_steam_hiss", ids);
    }

    [Fact]
    public void ForestOvergrownReturnsTwoIds()
    {
        var ids = AmbientZoneMap.GetAmbientIds(BiomeType.ForestOvergrown);
        Assert.Equal(2, ids.Length);
        Assert.Contains("amb_wind_leaves", ids);
        Assert.Contains("amb_insects", ids);
    }

    [Fact]
    public void IrradiatedCraterReturnsTwoIds()
    {
        var ids = AmbientZoneMap.GetAmbientIds(BiomeType.IrradiatedCrater);
        Assert.Equal(2, ids.Length);
        Assert.Contains("amb_geiger_crackle", ids);
        Assert.Contains("amb_eerie_hum", ids);
    }

    [Fact]
    public void UndergroundReturnsTwoIds()
    {
        var ids = AmbientZoneMap.GetAmbientIds(BiomeType.Underground);
        Assert.Equal(2, ids.Length);
        Assert.Contains("amb_cave_echo", ids);
        Assert.Contains("amb_dripping_water", ids);
    }

    [Fact]
    public void CoastalReturnsTwoIds()
    {
        var ids = AmbientZoneMap.GetAmbientIds(BiomeType.Coastal);
        Assert.Equal(2, ids.Length);
        Assert.Contains("amb_waves", ids);
        Assert.Contains("amb_seabirds", ids);
    }

    [Fact]
    public void UnknownBiomeReturnsEmpty()
    {
        var ids = AmbientZoneMap.GetAmbientIds((BiomeType)999);
        Assert.Empty(ids);
    }
}
