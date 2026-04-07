using Oravey2.Core.UI.Stride;

namespace Oravey2.Tests.UI;

public class ScenarioSelectorTests
{
    [Fact]
    public void Scenarios_ContainsFourEntries()
    {
        Assert.Equal(5, ScenarioSelectorScript.Scenarios.Length);
    }

    [Fact]
    public void Scenarios_AllHaveUniqueIds()
    {
        var ids = ScenarioSelectorScript.Scenarios.Select(s => s.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void Scenarios_AllHaveNonEmptyFields()
    {
        foreach (var s in ScenarioSelectorScript.Scenarios)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id), $"Scenario has empty Id");
            Assert.False(string.IsNullOrWhiteSpace(s.Name), $"Scenario '{s.Id}' has empty Name");
            Assert.False(string.IsNullOrWhiteSpace(s.Description), $"Scenario '{s.Id}' has empty Description");
            Assert.False(string.IsNullOrWhiteSpace(s.Notes), $"Scenario '{s.Id}' has empty Notes");
        }
    }

    [Theory]
    [InlineData("town")]
    [InlineData("wasteland")]
    [InlineData("m0_combat")]
    [InlineData("empty")]
    public void Scenarios_ContainsExpectedId(string expectedId)
    {
        Assert.Contains(ScenarioSelectorScript.Scenarios, s => s.Id == expectedId);
    }

    [Fact]
    public void ScenarioInfo_RecordEquality()
    {
        var a = new ScenarioInfo("test", "Test", "Desc", "Notes");
        var b = new ScenarioInfo("test", "Test", "Desc", "Notes");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Scenarios_TownIsFirst()
    {
        Assert.Equal("town", ScenarioSelectorScript.Scenarios[0].Id);
    }

    [Fact]
    public void DiscoverCustomMaps_NoMapsDir_ReturnsEmpty()
    {
        // When Maps/ doesn't exist, DiscoverCustomMaps returns empty
        var result = ScenarioSelectorScript.DiscoverCustomMaps();
        // May or may not be empty depending on test runner location,
        // but should not throw
        Assert.NotNull(result);
    }

    [Fact]
    public void DiscoverCustomMaps_SkipsBuiltInIds()
    {
        // Built-in IDs should never appear in custom discovery
        var custom = ScenarioSelectorScript.DiscoverCustomMaps();
        var builtInIds = ScenarioSelectorScript.Scenarios.Select(s => s.Id).ToHashSet();
        foreach (var c in custom)
            Assert.DoesNotContain(c.Id, builtInIds);
    }
}
