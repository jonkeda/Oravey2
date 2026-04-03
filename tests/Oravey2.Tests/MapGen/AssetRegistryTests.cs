using Oravey2.MapGen.Assets;

namespace Oravey2.Tests.MapGen;

public class AssetRegistryTests
{
    private static IAssetRegistry CreateTestRegistry()
    {
        var catalog = new Dictionary<string, List<AssetEntry>>
        {
            ["building"] = new()
            {
                new AssetEntry("buildings/ruined_office.glb", "Multi-story ruined office block", new[] { "large", "ruin" }),
                new AssetEntry("buildings/radio_tower.glb", "Small communication tower", new[] { "small", "infrastructure" }),
                new AssetEntry("buildings/elder_house.glb", "Two-story residential building", new[] { "large", "residential" })
            },
            ["surface"] = new()
            {
                new AssetEntry("Asphalt", "Cracked road surface", new[] { "road" }),
                new AssetEntry("Concrete", "Broken concrete", new[] { "urban" }),
                new AssetEntry("Dirt", "Packed dirt ground", new[] { "natural" }),
                new AssetEntry("Grass", "Overgrown grass", new[] { "natural" })
            }
        };
        return new AssetRegistry(catalog);
    }

    [Fact]
    public void Search_Building_Office_ReturnsRuinedOffice()
    {
        var registry = CreateTestRegistry();
        var results = registry.Search("building", "office");

        Assert.Single(results);
        Assert.Equal("buildings/ruined_office.glb", results[0].Id);
    }

    [Fact]
    public void Search_Surface_Road_ReturnsAsphalt()
    {
        var registry = CreateTestRegistry();
        var results = registry.Search("surface", "road");

        Assert.Single(results);
        Assert.Equal("Asphalt", results[0].Id);
    }

    [Fact]
    public void ListPrefabs_Building_ReturnsAll()
    {
        var registry = CreateTestRegistry();
        var results = registry.ListPrefabs("building");

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Exists_Surface_Asphalt_ReturnsTrue()
    {
        var registry = CreateTestRegistry();
        Assert.True(registry.Exists("surface", "Asphalt"));
    }

    [Fact]
    public void Exists_Surface_Nonexistent_ReturnsFalse()
    {
        var registry = CreateTestRegistry();
        Assert.False(registry.Exists("surface", "Nonexistent"));
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsAllInCategory()
    {
        var registry = CreateTestRegistry();
        var results = registry.Search("surface", "");

        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void Search_UnknownCategory_ReturnsEmpty()
    {
        var registry = CreateTestRegistry();
        var results = registry.Search("weapon", "sword");

        Assert.Empty(results);
    }

    [Fact]
    public void ListPrefabs_UnknownCategory_ReturnsEmpty()
    {
        var registry = CreateTestRegistry();
        var results = registry.ListPrefabs("weapon");

        Assert.Empty(results);
    }

    [Fact]
    public void Search_ByTag_ReturnsMatches()
    {
        var registry = CreateTestRegistry();
        var results = registry.Search("building", "residential");

        Assert.Single(results);
        Assert.Equal("buildings/elder_house.glb", results[0].Id);
    }

    [Fact]
    public void EmbeddedCatalog_Loads()
    {
        var registry = new AssetRegistry();

        Assert.True(registry.Exists("building", "buildings/ruined_office.glb"));
        Assert.True(registry.Exists("surface", "Asphalt"));
        Assert.True(registry.ListPrefabs("building").Count > 0);
    }
}
