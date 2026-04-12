using System.Numerics;
using System.Text.Json;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.Generation;

public class TownCuratorTests
{
    private static readonly TownGenerationParams DefaultParams = TownGenerationParams.Apocalyptic;

    private static RegionTemplate CreateRegionWith20Towns()
    {
        var towns = new List<TownEntry>();
        for (int i = 0; i < 20; i++)
        {
            double lat = 52.30 + i * 0.15; // ~16.7 km apart
            double lon = 4.80 + i * 0.05;
            int pop = 10000 + i * 5000;
            var category = i switch
            {
                < 3 => TownCategory.Hamlet,
                < 8 => TownCategory.Village,
                < 15 => TownCategory.Town,
                _ => TownCategory.City
            };
            towns.Add(new TownEntry(
                $"Town{i}", lat, lon, pop,
                new Vector2(i * 20000f, i * 16700f),
                category));
        }

        return new RegionTemplate
        {
            Name = "TestRegion",
            ElevationGrid = new float[1, 1],
            GridOriginLat = 52.50,
            GridOriginLon = 4.95,
            GridCellSizeMetres = 30.0,
            Towns = towns,
            Roads = [],
            WaterBodies = [],
            Railways = [],
            LandUseZones = []
        };
    }

    private static string BuildFakeLlmResponse(RegionTemplate region, int count = 10)
    {
        var entries = region.Towns.Take(count).Select((t, i) => new
        {
            gameName = $"Haven-{t.Name}",
            realName = t.Name,
            latitude = t.Latitude,
            longitude = t.Longitude,
            description = $"A test settlement based on {t.Name}",
            size = "Town",
            inhabitants = 5000 + i * 1000,
            destruction = "Moderate"
        });
        return JsonSerializer.Serialize(entries);
    }

    [Fact]
    public void CuratedTowns_CountInRange()
    {
        var region = CreateRegionWith20Towns();
        var json = BuildFakeLlmResponse(region, 10);

        var towns = TownCurator.ParseResponse(json, region);
        TownCurator.Validate(towns, DefaultParams);

        Assert.InRange(towns.Count, 8, 15);
    }

    [Fact]
    public void ParseResponse_HandlesMarkdownFences()
    {
        var region = CreateRegionWith20Towns();
        var rawJson = BuildFakeLlmResponse(region, 10);
        var wrapped = $"```json\n{rawJson}\n```";

        var towns = TownCurator.ParseResponse(wrapped, region);
        Assert.NotEmpty(towns);
    }

    [Fact]
    public void BuildPrompt_ContainsTownNames()
    {
        var region = CreateRegionWith20Towns();
        var prompt = TownCurator.BuildPrompt(region, DefaultParams);

        Assert.Contains("Town0", prompt);
        Assert.Contains("Town19", prompt);
        Assert.Contains("TestRegion", prompt);
    }

    [Fact]
    public void BuildPrompt_UsesGenreFromParams()
    {
        var region = CreateRegionWith20Towns();
        var fantasy = new TownGenerationParams
        {
            Genre = "Fantasy",
            ThemeDescription = "A medieval realm.",
            Roles = ["market_town", "fortress", "wizard_tower", "thieves_guild"],
            NamingInstruction = "a fantasy rename",
        };

        var prompt = TownCurator.BuildPrompt(region, fantasy);

        Assert.Contains("Fantasy", prompt);
        Assert.Contains("A medieval realm.", prompt);
        Assert.Contains("a fantasy rename", prompt);
        Assert.Contains("destruction", prompt);
        Assert.DoesNotContain("post-apocalyptic", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_UsesParamsForMinMax()
    {
        var towns = Enumerable.Range(0, 5).Select(i =>
            new CuratedTown($"T{i}", $"R{i}", 0, 0,
                new Vector2(i * 20000f, 0), "desc",
                TownCategory.Village, 1000, DestructionLevel.Moderate)).ToList();

        var narrow = DefaultParams with { MinTowns = 3 };
        TownCurator.Validate(towns, narrow); // should not throw with min=3
        Assert.Equal(5, towns.Count);
    }
}
