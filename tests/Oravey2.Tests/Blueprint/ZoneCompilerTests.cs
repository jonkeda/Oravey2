using Oravey2.Core.World;
using Oravey2.Core.World.Blueprint;

namespace Oravey2.Tests.Blueprint;

public class ZoneCompilerTests
{
    [Fact]
    public void ZoneBlueprint_MapsToZoneDefinition()
    {
        var zones = new[]
        {
            new ZoneBlueprint("downtown", "Downtown", "RuinedCity", 0.1f, 2, true, 0, 0, 1, 1)
        };

        var defs = ZoneCompiler.CompileZones(zones);

        Assert.Single(defs);
        Assert.Equal("downtown", defs[0].Id);
        Assert.Equal("Downtown", defs[0].Name);
        Assert.Equal(BiomeType.RuinedCity, defs[0].Biome);
        Assert.Equal(0.1f, defs[0].RadiationLevel);
        Assert.Equal(2, defs[0].EnemyDifficultyTier);
        Assert.True(defs[0].IsFastTravelTarget);
    }

    [Fact]
    public void MultipleZones_AllCompiled()
    {
        var zones = new[]
        {
            new ZoneBlueprint("z1", "Zone 1", "Wasteland", 0f, 1, false, 0, 0, 0, 0),
            new ZoneBlueprint("z2", "Zone 2", "Bunker", 0.5f, 3, true, 1, 0, 1, 0)
        };

        var defs = ZoneCompiler.CompileZones(zones);

        Assert.Equal(2, defs.Length);
        Assert.Equal("z1", defs[0].Id);
        Assert.Equal("z2", defs[1].Id);
    }

    [Fact]
    public void BiomeString_MapsToEnum()
    {
        var zones = new[]
        {
            new ZoneBlueprint("coast", "Coast", "Coastal", 0f, 1, false, 0, 0, 0, 0)
        };

        var defs = ZoneCompiler.CompileZones(zones);
        Assert.Equal(BiomeType.Coastal, defs[0].Biome);
    }

    [Fact]
    public void NullZones_ReturnsEmpty()
    {
        var defs = ZoneCompiler.CompileZones(null);
        Assert.Empty(defs);
    }
}
