using Oravey2.Core.World;
using Oravey2.Core.World.Liquids;

namespace Oravey2.Tests.Liquids;

public class LiquidPropertiesTests
{
    [Theory]
    [InlineData(LiquidType.Water)]
    [InlineData(LiquidType.Toxic)]
    [InlineData(LiquidType.Acid)]
    [InlineData(LiquidType.Sewage)]
    [InlineData(LiquidType.Lava)]
    [InlineData(LiquidType.Oil)]
    [InlineData(LiquidType.Frozen)]
    [InlineData(LiquidType.Anomaly)]
    public void AllLiquidTypes_HaveProperties(LiquidType type)
    {
        Assert.True(LiquidProperties.HasProperties(type),
            $"LiquidType.{type} has no property entry");

        var props = LiquidProperties.Get(type);
        Assert.True(props.Opacity > 0f, $"{type} opacity should be > 0");
    }

    [Fact]
    public void Lava_IsEmissive()
    {
        var props = LiquidProperties.Get(LiquidType.Lava);
        Assert.True(props.Emissive);
    }

    [Fact]
    public void Water_NoDamage()
    {
        var props = LiquidProperties.Get(LiquidType.Water);
        Assert.Equal(0f, props.DamagePerSecond);
    }
}
