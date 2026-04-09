using System.Text.Json;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.RegionTemplates;

public class CullSettingsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var settings = new CullSettings();

        Assert.Equal(TownCategory.Village, settings.TownMinCategory);
        Assert.Equal(1_000, settings.TownMinPopulation);
        Assert.Equal(5.0, settings.TownMinSpacingKm);
        Assert.Equal(30, settings.TownMaxCount);
        Assert.Equal(CullPriority.Category, settings.TownPriority);
        Assert.True(settings.TownAlwaysKeepCities);
        Assert.True(settings.TownAlwaysKeepMetropolis);

        Assert.Equal(RoadClass.Primary, settings.RoadMinClass);
        Assert.True(settings.RoadAlwaysKeepMotorways);
        Assert.True(settings.RoadKeepNearTowns);
        Assert.Equal(2.0, settings.RoadTownProximityKm);
        Assert.True(settings.RoadRemoveDeadEnds);
        Assert.Equal(1.0, settings.RoadDeadEndMinKm);
        Assert.True(settings.RoadSimplifyGeometry);
        Assert.Equal(50.0, settings.RoadSimplifyToleranceM);

        Assert.Equal(0.1, settings.WaterMinAreaKm2);
        Assert.Equal(2.0, settings.WaterMinRiverLengthKm);
        Assert.True(settings.WaterAlwaysKeepSea);
        Assert.True(settings.WaterAlwaysKeepLakes);
    }

    [Fact]
    public void RoundTrip_Json_PreservesAllProperties()
    {
        var original = new CullSettings
        {
            TownMinCategory = TownCategory.Hamlet,
            TownMinPopulation = 500,
            TownMinSpacingKm = 3.5,
            TownMaxCount = 50,
            TownPriority = CullPriority.Spacing,
            TownAlwaysKeepCities = false,
            TownAlwaysKeepMetropolis = false,
            RoadMinClass = RoadClass.Tertiary,
            RoadAlwaysKeepMotorways = false,
            RoadKeepNearTowns = false,
            RoadTownProximityKm = 5.0,
            RoadRemoveDeadEnds = false,
            RoadDeadEndMinKm = 2.5,
            RoadSimplifyGeometry = false,
            RoadSimplifyToleranceM = 100.0,
            WaterMinAreaKm2 = 0.5,
            WaterMinRiverLengthKm = 5.0,
            WaterAlwaysKeepSea = false,
            WaterAlwaysKeepLakes = false
        };

        var tempPath = Path.Combine(Path.GetTempPath(), $"cull_{Guid.NewGuid()}.json");
        try
        {
            original.Save(tempPath);
            var loaded = CullSettings.Load(tempPath);

            Assert.Equal(original, loaded);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Load_FromFile_ReturnsExpectedValues()
    {
        var json = """
        {
          "townMinCategory": "Town",
          "townMinPopulation": 5000,
          "townMinSpacingKm": 10.0,
          "townMaxCount": 15,
          "townPriority": "Population",
          "townAlwaysKeepCities": true,
          "townAlwaysKeepMetropolis": false,
          "roadMinClass": "Motorway",
          "roadAlwaysKeepMotorways": true,
          "roadKeepNearTowns": false,
          "roadTownProximityKm": 1.0,
          "roadRemoveDeadEnds": false,
          "roadDeadEndMinKm": 0.5,
          "roadSimplifyGeometry": false,
          "roadSimplifyToleranceM": 25.0,
          "waterMinAreaKm2": 1.0,
          "waterMinRiverLengthKm": 3.0,
          "waterAlwaysKeepSea": false,
          "waterAlwaysKeepLakes": true
        }
        """;

        var tempPath = Path.Combine(Path.GetTempPath(), $"cull_{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempPath, json);
            var settings = CullSettings.Load(tempPath);

            Assert.Equal(TownCategory.Town, settings.TownMinCategory);
            Assert.Equal(5000, settings.TownMinPopulation);
            Assert.Equal(10.0, settings.TownMinSpacingKm);
            Assert.Equal(15, settings.TownMaxCount);
            Assert.Equal(CullPriority.Population, settings.TownPriority);
            Assert.True(settings.TownAlwaysKeepCities);
            Assert.False(settings.TownAlwaysKeepMetropolis);
            Assert.Equal(RoadClass.Motorway, settings.RoadMinClass);
            Assert.False(settings.RoadKeepNearTowns);
            Assert.Equal(25.0, settings.RoadSimplifyToleranceM);
            Assert.Equal(1.0, settings.WaterMinAreaKm2);
            Assert.False(settings.WaterAlwaysKeepSea);
            Assert.True(settings.WaterAlwaysKeepLakes);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Save_CreatesValidJson()
    {
        var settings = new CullSettings { TownMinPopulation = 2000 };
        var tempPath = Path.Combine(Path.GetTempPath(), $"cull_{Guid.NewGuid()}.json");
        try
        {
            settings.Save(tempPath);
            var rawText = File.ReadAllText(tempPath);

            // Should be valid JSON
            var doc = JsonDocument.Parse(rawText);
            Assert.NotNull(doc);

            // Should contain the overridden value
            Assert.Contains("2000", rawText);

            // Should be indented (WriteIndented = true)
            Assert.Contains("\n", rawText);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
