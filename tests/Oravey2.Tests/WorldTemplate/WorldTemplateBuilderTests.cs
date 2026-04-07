using System.Numerics;
using Oravey2.MapGen.WorldTemplate;
using Xunit;

namespace Oravey2.Tests.WorldTemplate;

public class WorldTemplateBuilderTests
{
    [Fact]
    public void SerializeDeserialize_RoundTrips()
    {
        var geoMapper = new GeoMapper();
        var builder = new WorldTemplateBuilder(geoMapper);

        var osmData = new OsmExtract(
            Towns:
            [
                new TownEntry("TestTown", 52.51, 4.96, 5000, new Vector2(100, 200), TownCategory.Town)
            ],
            Roads:
            [
                new RoadSegment(RoadClass.Primary, [new Vector2(0, 0), new Vector2(100, 100)])
            ],
            WaterBodies:
            [
                new WaterBody(WaterType.Lake, [new Vector2(10, 10), new Vector2(20, 20), new Vector2(10, 20)])
            ],
            Railways:
            [
                new RailwaySegment([new Vector2(0, 0), new Vector2(50, 50)])
            ],
            LandUseZones:
            [
                new LandUseZone(LandUseType.Forest, [new Vector2(5, 5), new Vector2(15, 15), new Vector2(5, 15)])
            ]
        );

        var elevationGrid = new float[3, 3]
        {
            { 1f, 2f, 3f },
            { 4f, 5f, 6f },
            { 7f, 8f, 9f }
        };

        var template = builder.Build("TestRegion", elevationGrid, osmData, 52.50, 4.95, 30.0);

        using var stream = new MemoryStream();
        WorldTemplateBuilder.Serialize(template, stream);

        stream.Position = 0;
        var deserialized = WorldTemplateBuilder.Deserialize(stream);

        Assert.Equal("TestRegion", deserialized.Name);
        Assert.Equal(52.50, deserialized.OriginLatitude);
        Assert.Equal(4.95, deserialized.OriginLongitude);
        Assert.Single(deserialized.Regions);

        var region = deserialized.Regions[0];
        Assert.Equal("TestRegion", region.Name);
        Assert.Equal(3, region.ElevationGrid.GetLength(0));
        Assert.Equal(3, region.ElevationGrid.GetLength(1));
        Assert.Equal(5f, region.ElevationGrid[1, 1]);

        Assert.Single(region.Towns);
        Assert.Equal("TestTown", region.Towns[0].Name);
        Assert.Equal(TownCategory.Town, region.Towns[0].Category);

        Assert.Single(region.Roads);
        Assert.Equal(RoadClass.Primary, region.Roads[0].RoadClass);

        Assert.Single(region.WaterBodies);
        Assert.Equal(WaterType.Lake, region.WaterBodies[0].Type);

        Assert.Single(region.Railways);
        Assert.Single(region.LandUseZones);
        Assert.Equal(LandUseType.Forest, region.LandUseZones[0].Type);
    }
}
