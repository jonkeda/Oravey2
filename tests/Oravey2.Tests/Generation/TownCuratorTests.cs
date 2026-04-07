using System.Numerics;
using System.Text.Json;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.WorldTemplate;
using Xunit;

namespace Oravey2.Tests.Generation;

public class TownCuratorTests
{
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
            role = "trading_hub",
            faction = "Test Faction",
            threatLevel = Math.Clamp(i + 1, 1, 10),
            description = $"A test settlement based on {t.Name}"
        });
        return JsonSerializer.Serialize(entries);
    }

    [Fact]
    public void CuratedTowns_CountInRange()
    {
        var region = CreateRegionWith20Towns();
        var json = BuildFakeLlmResponse(region, 10);

        var towns = TownCurator.ParseResponse(json, region);
        TownCurator.Validate(towns);

        Assert.InRange(towns.Count, 8, 15);
    }

    [Fact]
    public void CuratedTowns_WithinSpacingLimits()
    {
        var region = CreateRegionWith20Towns();
        var json = BuildFakeLlmResponse(region, 10);

        var towns = TownCurator.ParseResponse(json, region);
        TownCurator.Validate(towns);

        for (int i = 0; i < towns.Count; i++)
        {
            for (int j = i + 1; j < towns.Count; j++)
            {
                var dist = Vector2.Distance(towns[i].GamePosition, towns[j].GamePosition);
                Assert.True(dist >= 15_000,
                    $"Towns {towns[i].GameName} and {towns[j].GameName} are only {dist:F0}m apart (min 15000m)");
            }
        }
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
        var prompt = TownCurator.BuildPrompt(region, 42);

        Assert.Contains("Town0", prompt);
        Assert.Contains("Town19", prompt);
        Assert.Contains("TestRegion", prompt);
    }
}
