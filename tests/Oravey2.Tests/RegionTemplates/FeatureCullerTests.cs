using System.Numerics;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.RegionTemplates;

public class FeatureCullerTests
{
    // --- Helper factories ---

    private static TownEntry MakeTown(string name, TownCategory cat, int pop,
        float gameX = 0, float gameZ = 0, double lat = 52.5, double lon = 4.9)
        => new(name, lat, lon, pop, new Vector2(gameX, gameZ), cat);

    private static RoadSegment MakeRoad(RoadClass cls, params Vector2[] nodes)
        => new(cls, nodes);

    private static WaterBody MakePolygonWater(WaterType type, float sideMetres)
    {
        // Square polygon of known area = sideMetres²
        var pts = new[]
        {
            new Vector2(0, 0),
            new Vector2(sideMetres, 0),
            new Vector2(sideMetres, sideMetres),
            new Vector2(0, sideMetres)
        };
        return new WaterBody(type, pts);
    }

    private static WaterBody MakeRiverWater(WaterType type, float lengthMetres)
    {
        // Straight polyline of known length
        return new WaterBody(type, [new Vector2(0, 0), new Vector2(lengthMetres, 0)]);
    }

    // ===== Town culling tests =====

    [Fact]
    public void CullTowns_BelowMinCategory_Removed()
    {
        var towns = new List<TownEntry>
        {
            MakeTown("Hamlet1", TownCategory.Hamlet, 200, gameX: 0),
            MakeTown("Village1", TownCategory.Village, 5000, gameX: 100_000),
            MakeTown("Town1", TownCategory.Town, 20000, gameX: 200_000)
        };
        var settings = new CullSettings
        {
            TownMinCategory = TownCategory.Village,
            TownMinPopulation = 0,
            TownMinSpacingKm = 0,
            TownMaxCount = 999
        };

        var result = FeatureCuller.CullTowns(towns, settings);

        Assert.DoesNotContain(result, t => t.Name == "Hamlet1");
        Assert.Contains(result, t => t.Name == "Village1");
        Assert.Contains(result, t => t.Name == "Town1");
    }

    [Fact]
    public void CullTowns_BelowMinPopulation_Removed()
    {
        var towns = new List<TownEntry>
        {
            MakeTown("Small", TownCategory.Village, 200),
            MakeTown("Big", TownCategory.Village, 5000)
        };
        var settings = new CullSettings { TownMinCategory = TownCategory.Hamlet, TownMinPopulation = 1000, TownMaxCount = 999 };

        var result = FeatureCuller.CullTowns(towns, settings);

        Assert.Single(result);
        Assert.Equal("Big", result[0].Name);
    }

    [Fact]
    public void CullTowns_ProtectedCategory_NotRemoved()
    {
        var towns = new List<TownEntry>
        {
            MakeTown("SmallCity", TownCategory.City, 50), // low pop but City
            MakeTown("Village1", TownCategory.Village, 200)
        };
        var settings = new CullSettings
        {
            TownMinCategory = TownCategory.Hamlet,
            TownMinPopulation = 1000,
            TownAlwaysKeepCities = true,
            TownMaxCount = 999
        };

        var result = FeatureCuller.CullTowns(towns, settings);

        Assert.Contains(result, t => t.Name == "SmallCity");
        Assert.DoesNotContain(result, t => t.Name == "Village1");
    }

    [Fact]
    public void CullTowns_SpacingEnforced_TooCloseRemoved()
    {
        // Two towns 2km apart, min spacing 5km — second should be removed
        var towns = new List<TownEntry>
        {
            MakeTown("A", TownCategory.Town, 10000, gameX: 0, gameZ: 0),
            MakeTown("B", TownCategory.Town, 5000, gameX: 2000, gameZ: 0) // 2km away
        };
        var settings = new CullSettings
        {
            TownMinCategory = TownCategory.Hamlet,
            TownMinPopulation = 0,
            TownMinSpacingKm = 5.0,
            TownPriority = CullPriority.Population,
            TownMaxCount = 999
        };

        var result = FeatureCuller.CullTowns(towns, settings);

        Assert.Single(result);
        Assert.Equal("A", result[0].Name); // higher population wins
    }

    [Fact]
    public void CullTowns_MaxCount_Honored()
    {
        var towns = Enumerable.Range(0, 50).Select(i =>
            MakeTown($"Town{i}", TownCategory.Town, 10000 + i, gameX: i * 100_000f)).ToList();
        var settings = new CullSettings
        {
            TownMinCategory = TownCategory.Hamlet,
            TownMinPopulation = 0,
            TownMinSpacingKm = 0,
            TownMaxCount = 10
        };

        var result = FeatureCuller.CullTowns(towns, settings);

        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void CullTowns_EmptyInput_ReturnsEmpty()
    {
        var result = FeatureCuller.CullTowns([], new CullSettings());
        Assert.Empty(result);
    }

    [Fact]
    public void CullTowns_AllKept_WhenSettingsPermissive()
    {
        var towns = new List<TownEntry>
        {
            MakeTown("H", TownCategory.Hamlet, 10, gameX: 0),
            MakeTown("V", TownCategory.Village, 100, gameX: 100_000),
            MakeTown("T", TownCategory.Town, 1000, gameX: 200_000)
        };
        var settings = new CullSettings
        {
            TownMinCategory = TownCategory.Hamlet,
            TownMinPopulation = 0,
            TownMinSpacingKm = 0,
            TownMaxCount = 999
        };

        var result = FeatureCuller.CullTowns(towns, settings);

        Assert.Equal(3, result.Count);
    }

    // ===== Road culling tests =====

    [Fact]
    public void CullRoads_BelowMinClass_Removed()
    {
        var roads = new List<RoadSegment>
        {
            MakeRoad(RoadClass.Primary, new(0,0), new(1000,0)),
            MakeRoad(RoadClass.Residential, new(0,0), new(500,0))
        };
        var settings = new CullSettings { RoadMinClass = RoadClass.Primary, RoadKeepNearTowns = false };

        var result = FeatureCuller.CullRoads(roads, [], settings);

        Assert.Single(result);
        Assert.Equal(RoadClass.Primary, result[0].RoadClass);
    }

    [Fact]
    public void CullRoads_Motorway_AlwaysKept()
    {
        var roads = new List<RoadSegment>
        {
            MakeRoad(RoadClass.Motorway, new(0,0), new(5000,0)),
            MakeRoad(RoadClass.Residential, new(0,0), new(500,0))
        };
        // Set min class to something that would exclude motorway if the enum order matters
        // RoadClass enum: Motorway=0, so all should pass. Test that motorway protection works
        // by also checking the residential is excluded
        var settings = new CullSettings
        {
            RoadMinClass = RoadClass.Primary, // Motorway(0) <= Primary(2), so motorway passes class filter too
            RoadAlwaysKeepMotorways = true,
            RoadKeepNearTowns = false
        };

        var result = FeatureCuller.CullRoads(roads, [], settings);

        Assert.Contains(result, r => r.RoadClass == RoadClass.Motorway);
        Assert.DoesNotContain(result, r => r.RoadClass == RoadClass.Residential);
    }

    [Fact]
    public void CullRoads_NearTown_Kept()
    {
        var townPos = new Vector2(1000, 1000);
        var towns = new List<TownEntry> { MakeTown("T1", TownCategory.Town, 5000, gameX: 1000, gameZ: 1000) };
        var roads = new List<RoadSegment>
        {
            // Road 500m from town — within 2km proximity
            MakeRoad(RoadClass.Secondary, new(1500, 1000), new(2000, 1000))
        };
        var settings = new CullSettings
        {
            RoadMinClass = RoadClass.Motorway, // very strict — would exclude Secondary
            RoadKeepNearTowns = true,
            RoadTownProximityKm = 2.0,
            RoadAlwaysKeepMotorways = false,
            RoadRemoveDeadEnds = false,
            RoadSimplifyGeometry = false
        };

        var result = FeatureCuller.CullRoads(roads, towns, settings);

        Assert.Single(result);
    }

    [Fact]
    public void CullRoads_FarFromTown_Removed()
    {
        var towns = new List<TownEntry> { MakeTown("T1", TownCategory.Town, 5000, gameX: 0, gameZ: 0) };
        var roads = new List<RoadSegment>
        {
            // Road 10km from town
            MakeRoad(RoadClass.Tertiary, new(10_000, 10_000), new(11_000, 10_000))
        };
        var settings = new CullSettings
        {
            RoadMinClass = RoadClass.Primary,
            RoadKeepNearTowns = true,
            RoadTownProximityKm = 2.0,
            RoadAlwaysKeepMotorways = false,
            RoadRemoveDeadEnds = false,
            RoadSimplifyGeometry = false
        };

        var result = FeatureCuller.CullRoads(roads, towns, settings);

        Assert.Empty(result);
    }

    [Fact]
    public void CullRoads_DeadEnd_Removed()
    {
        // A short dead-end (200m) connected to one main road
        var mainRoad = MakeRoad(RoadClass.Primary, new(0, 0), new(5000, 0));
        var deadEnd = MakeRoad(RoadClass.Primary, new(0, 0), new(0, 200)); // branches off start, only 200m

        var roads = new List<RoadSegment> { mainRoad, deadEnd };
        var settings = new CullSettings
        {
            RoadMinClass = RoadClass.Residential,
            RoadKeepNearTowns = false,
            RoadRemoveDeadEnds = true,
            RoadDeadEndMinKm = 1.0, // anything below 1km dead-end removed
            RoadAlwaysKeepMotorways = false,
            RoadSimplifyGeometry = false
        };

        var result = FeatureCuller.CullRoads(roads, [], settings);

        // Main road (5km) should be kept, dead-end (200m, 1 connection) should be removed
        Assert.Single(result);
        Assert.Equal(5000f, result[0].Nodes[^1].X, 0.1);
    }

    [Fact]
    public void CullRoads_GeometrySimplified()
    {
        // Road with many redundant points along a straight line
        var nodes = Enumerable.Range(0, 100).Select(i => new Vector2(i * 10, 0)).ToArray();
        var roads = new List<RoadSegment> { MakeRoad(RoadClass.Primary, nodes) };
        var settings = new CullSettings
        {
            RoadMinClass = RoadClass.Residential,
            RoadKeepNearTowns = false,
            RoadRemoveDeadEnds = false,
            RoadSimplifyGeometry = true,
            RoadSimplifyToleranceM = 1.0,
            RoadAlwaysKeepMotorways = false
        };

        var result = FeatureCuller.CullRoads(roads, [], settings);

        Assert.Single(result);
        Assert.True(result[0].Nodes.Length < 100); // simplified
        Assert.Equal(2, result[0].Nodes.Length); // straight line → just endpoints
    }

    // ===== Water culling tests =====

    [Fact]
    public void CullWater_SmallArea_Removed()
    {
        // Square pond: 100m × 100m = 0.01 km², below 0.1 km² threshold
        var water = new List<WaterBody> { MakePolygonWater(WaterType.Lake, 100) };
        var settings = new CullSettings { WaterMinAreaKm2 = 0.1, WaterAlwaysKeepLakes = false };

        var result = FeatureCuller.CullWater(water, settings);

        Assert.Empty(result);
    }

    [Fact]
    public void CullWater_ShortRiver_Removed()
    {
        // River 500m long, threshold is 2km
        var water = new List<WaterBody> { MakeRiverWater(WaterType.River, 500) };
        var settings = new CullSettings { WaterMinRiverLengthKm = 2.0 };

        var result = FeatureCuller.CullWater(water, settings);

        Assert.Empty(result);
    }

    [Fact]
    public void CullWater_Sea_AlwaysKept()
    {
        // Tiny sea polygon — should be kept because of protection
        var water = new List<WaterBody> { MakePolygonWater(WaterType.Sea, 10) }; // 100 m² — tiny
        var settings = new CullSettings { WaterMinAreaKm2 = 999.0, WaterAlwaysKeepSea = true };

        var result = FeatureCuller.CullWater(water, settings);

        Assert.Single(result);
        Assert.Equal(WaterType.Sea, result[0].Type);
    }

    // ===== Douglas-Peucker tests =====

    [Fact]
    public void SimplifyLine_StraightLine_ReturnsEndpoints()
    {
        var points = Enumerable.Range(0, 10).Select(i => new Vector2(i * 100, 0)).ToArray();

        var result = FeatureCuller.SimplifyLine(points, 1.0);

        Assert.Equal(2, result.Length);
        Assert.Equal(points[0], result[0]);
        Assert.Equal(points[^1], result[^1]);
    }

    [Fact]
    public void SimplifyLine_ZigZag_KeepsPeaks()
    {
        // Zig-zag: 0,0 → 100,200 → 200,0 → 300,200 → 400,0
        var points = new[]
        {
            new Vector2(0, 0),
            new Vector2(100, 200),
            new Vector2(200, 0),
            new Vector2(300, 200),
            new Vector2(400, 0)
        };

        var result = FeatureCuller.SimplifyLine(points, 10.0);

        // All peaks are >10m from the baseline, so all should be kept
        Assert.Equal(5, result.Length);
    }

    [Fact]
    public void SimplifyLine_SinglePoint_ReturnsSame()
    {
        var points = new[] { new Vector2(42, 17) };

        var result = FeatureCuller.SimplifyLine(points, 10.0);

        Assert.Single(result);
        Assert.Equal(new Vector2(42, 17), result[0]);
    }

    [Fact]
    public void SimplifyLine_TwoPoints_ReturnsSame()
    {
        var points = new[] { new Vector2(0, 0), new Vector2(100, 100) };

        var result = FeatureCuller.SimplifyLine(points, 10.0);

        Assert.Equal(2, result.Length);
    }
}
