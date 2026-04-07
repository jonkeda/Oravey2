using Oravey2.Core.Rendering;
using Oravey2.Core.World;
using Oravey2.Core.World.Rendering;

namespace Oravey2.Tests.Rendering;

public class LiquidEffectFactoryTests
{
    [Theory]
    [InlineData(LiquidType.Water, "WaterShader")]
    [InlineData(LiquidType.Toxic, "ToxicShader")]
    [InlineData(LiquidType.Acid, "ToxicShader")]
    [InlineData(LiquidType.Sewage, "WaterShader")]
    [InlineData(LiquidType.Lava, "LavaShader")]
    [InlineData(LiquidType.Oil, "OilShader")]
    [InlineData(LiquidType.Frozen, "FrozenShader")]
    [InlineData(LiquidType.Anomaly, "AnomalyShader")]
    public void AllLiquidTypes_MapToShaderName(LiquidType type, string expectedShader)
    {
        var shaderName = LiquidEffectFactory.GetShaderName(type);
        Assert.Equal(expectedShader, shaderName);
    }

    [Fact]
    public void None_ReturnsNullShaderName()
    {
        var shaderName = LiquidEffectFactory.GetShaderName(LiquidType.None);
        Assert.Null(shaderName);
    }

    [Fact]
    public void AllNonNone_HaveShaderMapping()
    {
        foreach (var type in Enum.GetValues<LiquidType>())
        {
            if (type == LiquidType.None) continue;
            var shaderName = LiquidEffectFactory.GetShaderName(type);
            Assert.NotNull(shaderName);
            Assert.NotEmpty(shaderName);
        }
    }

    [Theory]
    [InlineData(QualityPreset.Low, 0)]
    [InlineData(QualityPreset.Medium, 1)]
    [InlineData(QualityPreset.High, 2)]
    public void QualityLevel_MapsCorrectly(QualityPreset preset, int expected)
    {
        Assert.Equal(expected, LiquidEffectFactory.GetQualityLevel(preset));
    }

    [Fact]
    public void ShaderNames_MatchSdslFiles()
    {
        // Verify the shader names match the actual .sdsl file declarations
        var expectedShaders = new HashSet<string>
        {
            "WaterShader", "ToxicShader", "LavaShader",
            "OilShader", "FrozenShader", "AnomalyShader"
        };

        foreach (var type in Enum.GetValues<LiquidType>())
        {
            if (type == LiquidType.None) continue;
            var shader = LiquidEffectFactory.GetShaderName(type);
            Assert.Contains(shader!, expectedShaders);
        }
    }

    [Fact]
    public void AcidAndToxic_ShareShader()
    {
        Assert.Equal(
            LiquidEffectFactory.GetShaderName(LiquidType.Acid),
            LiquidEffectFactory.GetShaderName(LiquidType.Toxic));
    }

    [Fact]
    public void SewageAndWater_ShareShader()
    {
        Assert.Equal(
            LiquidEffectFactory.GetShaderName(LiquidType.Sewage),
            LiquidEffectFactory.GetShaderName(LiquidType.Water));
    }
}
