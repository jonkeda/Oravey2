using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.Tests.MapGen;

public class GeofabrikIndexTests
{
    private static string MakeGeoJson(params string[] features)
        => $$"""{"type":"FeatureCollection","features":[{{string.Join(",", features)}}]}""";

    private static string MakeFeature(string id, string name, string? parent = null,
        string? pbfUrl = null, string[]? iso2 = null, string? geometryJson = null)
    {
        var props = new List<string>
        {
            $"\"id\":\"{id}\"",
            $"\"name\":\"{name}\""
        };
        if (parent is not null) props.Add($"\"parent\":\"{parent}\"");
        if (pbfUrl is not null) props.Add($"\"urls\":{{\"pbf\":\"{pbfUrl}\"}}");
        if (iso2 is not null)
        {
            var codes = string.Join(",", iso2.Select(c => $"\"{c}\""));
            props.Add($"\"iso3166-1:alpha2\":[{codes}]");
        }
        var geom = geometryJson ?? "null";
        return $$"""{"type":"Feature","properties":{{{string.Join(",", props)}}},"geometry":{{geom}}}""";
    }

    [Fact]
    public void Parse_BuildsHierarchy()
    {
        var json = MakeGeoJson(
            MakeFeature("europe", "Europe"),
            MakeFeature("netherlands", "Netherlands", parent: "europe"),
            MakeFeature("noord-holland", "Noord-Holland", parent: "netherlands",
                pbfUrl: "https://download.geofabrik.de/europe/netherlands/noord-holland-latest.osm.pbf"));

        var index = GeofabrikIndex.Parse(json);

        Assert.Single(index.Roots);
        Assert.Equal("europe", index.Roots[0].Id);
        Assert.Single(index.Roots[0].Children);
        Assert.Equal("netherlands", index.Roots[0].Children[0].Id);
        Assert.Single(index.Roots[0].Children[0].Children);
        Assert.Equal("noord-holland", index.Roots[0].Children[0].Children[0].Id);
    }

    [Fact]
    public void Search_ByName_FindsRegion()
    {
        var json = MakeGeoJson(
            MakeFeature("europe", "Europe"),
            MakeFeature("netherlands", "Netherlands", parent: "europe"),
            MakeFeature("noord-holland", "Noord-Holland", parent: "netherlands"));

        var index = GeofabrikIndex.Parse(json);
        var results = index.Search("noord").ToList();

        Assert.Single(results);
        Assert.Equal("noord-holland", results[0].Id);
    }

    [Fact]
    public void Search_ByIsoCode_FindsRegion()
    {
        var json = MakeGeoJson(
            MakeFeature("netherlands", "Netherlands", iso2: ["NL"]));

        var index = GeofabrikIndex.Parse(json);
        var results = index.Search("NL").ToList();

        Assert.Single(results);
        Assert.Equal("netherlands", results[0].Id);
    }

    [Fact]
    public void Parse_RootHasNoParent()
    {
        var json = MakeGeoJson(
            MakeFeature("africa", "Africa"),
            MakeFeature("europe", "Europe"));

        var index = GeofabrikIndex.Parse(json);

        Assert.Equal(2, index.Roots.Count);
        Assert.All(index.Roots, r => Assert.Null(r.Parent));
    }

    [Fact]
    public void ToRegionPreset_FromGeometry()
    {
        var polygon = """{"type":"Polygon","coordinates":[[[4.0,52.0],[5.0,52.0],[5.0,53.0],[4.0,53.0],[4.0,52.0]]]}""";
        var json = MakeGeoJson(
            MakeFeature("noord-holland", "Noord-Holland",
                pbfUrl: "https://download.geofabrik.de/europe/netherlands/noord-holland-latest.osm.pbf",
                geometryJson: polygon));

        var index = GeofabrikIndex.Parse(json);
        var region = index.ById["noord-holland"];
        var preset = region.ToRegionPreset();

        Assert.Equal("noord-holland", preset.Name);
        Assert.Equal("Noord-Holland", preset.DisplayName);
        Assert.Equal(53.0, preset.NorthLat);
        Assert.Equal(52.0, preset.SouthLat);
        Assert.Equal(5.0, preset.EastLon);
        Assert.Equal(4.0, preset.WestLon);
        Assert.Contains("noord-holland", preset.OsmDownloadUrl);
    }
}
