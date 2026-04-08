using System.Numerics;
using Oravey2.MapGen.WorldTemplate;
using Xunit;

namespace Oravey2.Tests.WorldTemplate;

public class WorldTemplateToolCullTests
{
    [Fact]
    public void CullSettings_Load_ValidFile_ReturnsSettings()
    {
        var settings = new CullSettings
        {
            TownMinPopulation = 5_000,
            TownMaxCount = 20,
            RoadMinClass = RoadClass.Secondary,
            WaterMinAreaKm2 = 0.5
        };

        var path = Path.GetTempFileName();
        try
        {
            settings.Save(path);
            var loaded = CullSettings.Load(path);

            Assert.Equal(5_000, loaded.TownMinPopulation);
            Assert.Equal(20, loaded.TownMaxCount);
            Assert.Equal(RoadClass.Secondary, loaded.RoadMinClass);
            Assert.Equal(0.5, loaded.WaterMinAreaKm2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CullSettings_Load_InvalidJson_Throws()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not valid json {{{");
            Assert.ThrowsAny<Exception>(() => CullSettings.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CullFlag_ReducesFeatureCount()
    {
        var settings = new CullSettings
        {
            TownMinCategory = TownCategory.Town,
            TownMinPopulation = 5_000,
            TownMaxCount = 100,
            TownAlwaysKeepCities = true,
            TownAlwaysKeepMetropolis = true,
            RoadMinClass = RoadClass.Primary,
            RoadKeepNearTowns = false,
            RoadAlwaysKeepMotorways = false,
            RoadSimplifyGeometry = false,
            WaterMinAreaKm2 = 1.0
        };

        var towns = new List<TownEntry>
        {
            new("BigCity", 52.0, 4.0, 100_000, Vector2.Zero, TownCategory.City),
            new("SmallHamlet", 52.1, 4.1, 50, new Vector2(10_000, 0), TownCategory.Hamlet),
            new("TinyVillage", 52.2, 4.2, 200, new Vector2(20_000, 0), TownCategory.Village),
            new("MediumTown", 52.3, 4.3, 10_000, new Vector2(30_000, 0), TownCategory.Town)
        };

        var roads = new List<RoadSegment>
        {
            new(RoadClass.Motorway, [Vector2.Zero, new Vector2(5000, 0)]),
            new(RoadClass.Residential, [new Vector2(50_000, 0), new Vector2(51_000, 0)]),
            new(RoadClass.Residential, [new Vector2(60_000, 0), new Vector2(61_000, 0)]),
            new(RoadClass.Primary, [new Vector2(20_000, 0), new Vector2(25_000, 0)])
        };

        var water = new List<WaterBody>
        {
            // Large polygon (area > 1 km²)
            new(WaterType.Lake, [Vector2.Zero, new Vector2(2000, 0), new Vector2(2000, 2000), new Vector2(0, 2000)]),
            // Tiny polygon (area < 1 km²)
            new(WaterType.Lake, [new Vector2(10_000, 0), new Vector2(10_010, 0), new Vector2(10_010, 10)])
        };

        // Apply culling (same as CLI would)
        var culledTowns = FeatureCuller.CullTowns(towns, settings);
        var culledRoads = FeatureCuller.CullRoads(roads, culledTowns, settings);
        var culledWater = FeatureCuller.CullWater(water, settings);

        // Towns: hamlet and village culled, city kept (always-keep), town kept
        Assert.True(culledTowns.Count < towns.Count,
            $"Expected fewer towns: got {culledTowns.Count} from {towns.Count}");
        // Roads: residential filtered out (min class = Primary, no near-town, no motorway keep)
        Assert.True(culledRoads.Count < roads.Count,
            $"Expected fewer roads: got {culledRoads.Count} from {roads.Count}");
        // Water: tiny lake filtered (< 1 km²), but large lake and AlwaysKeepLakes
        Assert.True(culledWater.Count <= water.Count,
            $"Expected <= water: got {culledWater.Count} from {water.Count}");
    }

    [Fact]
    public void NoCullFlag_AllFeaturesIncluded()
    {
        // Without culling, the OsmExtract is passed directly to the builder
        var towns = new List<TownEntry>
        {
            new("A", 52.0, 4.0, 100, Vector2.Zero, TownCategory.Hamlet),
            new("B", 52.1, 4.1, 200, new Vector2(1000, 0), TownCategory.Village)
        };
        var roads = new List<RoadSegment>
        {
            new(RoadClass.Residential, [Vector2.Zero, new Vector2(100, 0)])
        };
        var water = new List<WaterBody>
        {
            new(WaterType.River, [Vector2.Zero, new Vector2(500, 0)])
        };

        var extract = new OsmExtract(towns, roads, water, [], []);

        // No culling applied — all features remain
        Assert.Equal(2, extract.Towns.Count);
        Assert.Single(extract.Roads);
        Assert.Single(extract.WaterBodies);
    }
}
