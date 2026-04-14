using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.Generation;

public class RoadSelectorTests
{
    private static (RegionTemplate Region, CuratedRegion Curated) CreateTestData()
    {
        var elevation = new float[10, 10];

        var townA = new TownEntry("TownA", 52.50, 4.95, 50000, new Vector2(0, 0), TownCategory.Town);
        var townB = new TownEntry("TownB", 52.60, 5.05, 30000, new Vector2(8000, 11000), TownCategory.Village);
        var townC = new TownEntry("Excluded", 52.70, 5.15, 5000, new Vector2(50000, 50000), TownCategory.Hamlet);

        var region = new RegionTemplate
        {
            Name = "TestRegion",
            ElevationGrid = elevation,
            GridOriginLat = 52.50,
            GridOriginLon = 4.95,
            GridCellSizeMetres = 30.0,
            Towns = [townA, townB, townC],
            Roads =
            [
                // Motorway — always kept
                new RoadSegment(LinearFeatureType.Motorway,
                    [new Vector2(-5000, 0), new Vector2(20000, 0)]),
                // Road connecting TownA and TownB — should be kept
                new RoadSegment(LinearFeatureType.Primary,
                    [new Vector2(0, 0), new Vector2(4000, 5500), new Vector2(8000, 11000)]),
                // Road only near excluded town — should be dropped
                new RoadSegment(LinearFeatureType.Secondary,
                    [new Vector2(49500, 49500), new Vector2(50500, 50500)])
            ],
            WaterBodies = [],
            Railways = [],
            LandUseZones = []
        };

        var curatedA = new CuratedTown { GameName = "HavenA", RealName = "TownA", Latitude = 52.50, Longitude = 4.95, GamePosition = new Vector2(0, 0),
            Description = "Safe town", Size = TownCategory.Town, Inhabitants = 5000, Destruction = DestructionLevel.Pristine };
        var curatedB = new CuratedTown { GameName = "HavenB", RealName = "TownB", Latitude = 52.60, Longitude = 5.05, GamePosition = new Vector2(8000, 11000),
            Description = "Military base", Size = TownCategory.Town, Inhabitants = 3000, Destruction = DestructionLevel.Moderate };

        var curated = new CuratedRegion { Name = "TestRegion",
            BoundsMin = new Vector2(-5000, -5000), BoundsMax = new Vector2(55000, 55000),
            Towns = [curatedA, curatedB] };

        return (region, curated);
    }

    [Fact]
    public void AllCuratedTowns_AreConnected()
    {
        var (region, curated) = CreateTestData();
        var selector = new RoadSelector();

        var roads = selector.Select(region, curated);

        // TownA at (0,0) — check a road passes within 500m
        bool townAConnected = roads.Any(r =>
            r.Nodes.Any(n => Vector2.Distance(n, new Vector2(0, 0)) < 500));
        Assert.True(townAConnected, "TownA should be connected by at least one road");

        // TownB at (8000, 11000)
        bool townBConnected = roads.Any(r =>
            r.Nodes.Any(n => Vector2.Distance(n, new Vector2(8000, 11000)) < 500));
        Assert.True(townBConnected, "TownB should be connected by at least one road");
    }

    [Fact]
    public void NonCuratedTowns_RoadsExcluded()
    {
        var (region, curated) = CreateTestData();
        var selector = new RoadSelector();

        var roads = selector.Select(region, curated);

        // The secondary road near (50000, 50000) should not be included
        bool excludedRoadPresent = roads.Any(r =>
            r.Nodes.Any(n => Vector2.Distance(n, new Vector2(50000, 50000)) < 1000));
        Assert.False(excludedRoadPresent, "Roads only connecting to non-curated towns should be excluded");
    }
}
