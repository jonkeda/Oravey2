using System.Numerics;
using Oravey2.MapGen.WorldTemplate;
using OsmSharp;
using OsmSharp.Streams;
using Xunit;

namespace Oravey2.Tests.WorldTemplate;

public class OsmParserTests
{
    private static MemoryStream CreateTestPbf(Node[] nodes, Way[] ways)
    {
        var stream = new MemoryStream();
        var target = new PBFOsmStreamTarget(stream);
        target.Initialize();

        foreach (var node in nodes)
            target.AddNode(node);
        foreach (var way in ways)
            target.AddWay(way);

        target.Flush();
        target.Close();
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void ParsePbf_ExtractsTowns()
    {
        var nodes = new[]
        {
            new Node
            {
                Id = 1, Latitude = 52.51, Longitude = 4.96,
                Tags = new OsmSharp.Tags.TagsCollection(
                    new OsmSharp.Tags.Tag("place", "town"),
                    new OsmSharp.Tags.Tag("name", "Purmerend"),
                    new OsmSharp.Tags.Tag("population", "80000"))
            },
            new Node
            {
                Id = 2, Latitude = 52.37, Longitude = 4.90,
                Tags = new OsmSharp.Tags.TagsCollection(
                    new OsmSharp.Tags.Tag("place", "city"),
                    new OsmSharp.Tags.Tag("name", "Amsterdam"),
                    new OsmSharp.Tags.Tag("population", "900000"))
            },
            new Node
            {
                Id = 3, Latitude = 52.63, Longitude = 4.75,
                Tags = new OsmSharp.Tags.TagsCollection(
                    new OsmSharp.Tags.Tag("place", "village"),
                    new OsmSharp.Tags.Tag("name", "Schagen"))
            }
        };

        using var pbf = CreateTestPbf(nodes, []);
        var parser = new OsmParser(new GeoMapper());
        var result = parser.ParsePbf(pbf);

        Assert.Equal(3, result.Towns.Count);
        Assert.Contains(result.Towns, t => t.Name == "Purmerend" && t.Category == TownCategory.Town);
        Assert.Contains(result.Towns, t => t.Name == "Amsterdam" && t.Category == TownCategory.City);
        Assert.Contains(result.Towns, t => t.Name == "Schagen" && t.Category == TownCategory.Village);
    }

    [Fact]
    public void ParsePbf_ExtractsRoads()
    {
        var nodes = new[]
        {
            new Node { Id = 10, Latitude = 52.50, Longitude = 4.95 },
            new Node { Id = 11, Latitude = 52.51, Longitude = 4.96 },
            new Node { Id = 12, Latitude = 52.52, Longitude = 4.97 }
        };

        var ways = new[]
        {
            new Way
            {
                Id = 100, Nodes = [10, 11, 12],
                Tags = new OsmSharp.Tags.TagsCollection(
                    new OsmSharp.Tags.Tag("highway", "primary"),
                    new OsmSharp.Tags.Tag("name", "N235"))
            }
        };

        using var pbf = CreateTestPbf(nodes, ways);
        var parser = new OsmParser(new GeoMapper());
        var result = parser.ParsePbf(pbf);

        Assert.True(result.Roads.Count >= 1);
        Assert.Contains(result.Roads, r => r.RoadClass == RoadClass.Primary);
    }

    [Fact]
    public void TownPositions_ConvertedToGameCoords()
    {
        var nodes = new[]
        {
            new Node
            {
                Id = 1, Latitude = 52.55, Longitude = 5.00,
                Tags = new OsmSharp.Tags.TagsCollection(
                    new OsmSharp.Tags.Tag("place", "town"),
                    new OsmSharp.Tags.Tag("name", "TestTown"))
            }
        };

        using var pbf = CreateTestPbf(nodes, []);
        var parser = new OsmParser(new GeoMapper());
        var result = parser.ParsePbf(pbf);

        Assert.Single(result.Towns);
        var town = result.Towns[0];

        // Position should be non-zero (offset from origin)
        Assert.True(town.GamePosition.X != 0 || town.GamePosition.Y != 0,
            "Town game position should be non-zero when offset from origin");

        // X should be positive (east of origin), Y should be positive (north of origin)
        Assert.True(town.GamePosition.X > 0, "Town east of origin should have positive X");
        Assert.True(town.GamePosition.Y > 0, "Town north of origin should have positive Y");

        // Distance should be in expected range (a few km)
        float dist = town.GamePosition.Length();
        Assert.InRange(dist, 1_000f, 10_000f);
    }
}
