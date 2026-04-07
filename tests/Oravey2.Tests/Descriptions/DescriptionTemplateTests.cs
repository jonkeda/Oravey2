using Oravey2.Core.Descriptions;
using Oravey2.Core.World;

namespace Oravey2.Tests.Descriptions;

public class DescriptionTemplateTests
{
    [Fact]
    public void GetTemplate_AbandonedTown_Wasteland_ReturnsTemplate()
    {
        var tagline = DescriptionTemplates.GetTagline("town", BiomeType.Wasteland, "Deadwood");

        Assert.Contains("Deadwood", tagline);
        Assert.True(tagline.Length <= 80, $"Tagline too long: {tagline.Length} chars");
    }

    [Fact]
    public void GetTemplate_UnknownCombo_ReturnsFallback()
    {
        var tagline = DescriptionTemplates.GetTagline("alien_hive", BiomeType.Underground, "Xenos");

        Assert.NotNull(tagline);
        Assert.NotEmpty(tagline);
        // Fallback doesn't contain {name} — it's a generic message
        Assert.DoesNotContain("{name}", tagline);
    }

    [Theory]
    [InlineData("town")]
    [InlineData("gas_station")]
    [InlineData("checkpoint")]
    [InlineData("ruin")]
    [InlineData("bunker")]
    [InlineData("camp")]
    public void AllPOITypes_HaveAtLeastFallback(string poiType)
    {
        foreach (var biome in Enum.GetValues<BiomeType>())
        {
            var tagline = DescriptionTemplates.GetTagline(poiType, biome, "TestLocation");
            Assert.NotNull(tagline);
            Assert.DoesNotContain("{name}", tagline);
        }
    }

    [Fact]
    public void GetSummary_Town_Wasteland_ReturnsExpanded()
    {
        var summary = DescriptionTemplates.GetSummary("town", BiomeType.Wasteland, "Dustville");

        Assert.Contains("Dustville", summary);
        Assert.True(summary.Length > 50, "Summary should be a paragraph, not a tagline");
    }

    [Fact]
    public void GetSummary_UnknownType_ReturnsFallback()
    {
        var summary = DescriptionTemplates.GetSummary("spaceship", BiomeType.Coastal, "Wreck");

        Assert.NotNull(summary);
        Assert.DoesNotContain("{name}", summary);
    }

    [Theory]
    [InlineData("town", BiomeType.RuinedCity)]
    [InlineData("town", BiomeType.Settlement)]
    [InlineData("town", BiomeType.ForestOvergrown)]
    [InlineData("town", BiomeType.Coastal)]
    [InlineData("gas_station", BiomeType.Wasteland)]
    public void KnownCombinations_ReturnSpecificTemplate(string poiType, BiomeType biome)
    {
        var tagline = DescriptionTemplates.GetTagline(poiType, biome, "TestLoc");
        var fallback = "A forgotten place in the wasteland.";

        Assert.NotEqual(fallback, tagline);
    }
}
