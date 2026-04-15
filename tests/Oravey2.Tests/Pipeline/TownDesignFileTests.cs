using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Pipeline;

public class TownDesignFileTests
{
    private static TownDesign MakeDesign() => new()
    {
        TownName = "Havenburg",
        Landmarks = [new LandmarkBuilding { Name = "Fort Kijkduin", VisualDescription = "A massive coastal fortress with crumbling stone walls", SizeCategory = "large", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" }],
        KeyLocations =
        [
            new KeyLocation { Name = "The Drydock Market", Purpose = "shop", VisualDescription = "An old naval drydock converted into a marketplace", SizeCategory = "medium", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
            new KeyLocation { Name = "Clinic", Purpose = "medical", VisualDescription = "A converted church clinic", SizeCategory = "small", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
            new KeyLocation { Name = "Barracks", Purpose = "barracks", VisualDescription = "Reinforced concrete bunker", SizeCategory = "medium", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
        ],
        LayoutStyle = "compound",
        Hazards = [new EnvironmentalHazard { Type = "flooding", Description = "The harbour district floods at high tide", LocationHint = "south-west waterfront" }],
    };

    private static TownDesign MakeDesignWithNewFields() => new()
    {
        TownName = "Havenburg",
        Landmarks =
        [
            new LandmarkBuilding { Name = "Fort Kijkduin", VisualDescription = "A massive coastal fortress", SizeCategory = "large",
                OriginalDescription = "Fort Kijkduin, a 19th-century Napoleonic coastal defence fort",
                MeshyPrompt = "Ruined stone fortress, crumbling walls, overgrown, low-poly game asset",
                PositionHint = "north-west, near the coastline" },
            new LandmarkBuilding { Name = "The Lighthouse", VisualDescription = "A crumbling lighthouse tower", SizeCategory = "medium",
                OriginalDescription = "Lange Jaap lighthouse, tallest cast-iron lighthouse in Europe, built 1877",
                MeshyPrompt = "Tall crumbling cast-iron lighthouse, broken glass, rust, low-poly game asset",
                PositionHint = "north, on the harbour pier" },
        ],
        KeyLocations =
        [
            new KeyLocation { Name = "The Drydock Market", Purpose = "shop", VisualDescription = "An old naval drydock marketplace", SizeCategory = "medium",
                OriginalDescription = "Willemsoord drydock, former Royal Netherlands Navy shipyard, 19th century",
                MeshyPrompt = "Ruined industrial drydock with market stalls, low-poly game asset",
                PositionHint = "centre, on the main square" },
        ],
        LayoutStyle = "compound",
        Hazards = [new EnvironmentalHazard { Type = "flooding", Description = "The harbour floods at high tide", LocationHint = "south-west waterfront" }],
    };

    [Fact]
    public void Save_MapsAllFields()
    {
        var design = MakeDesign();
        var dir = Path.Combine(Path.GetTempPath(), "test-design-fields-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "design.json");

        try
        {
            design.Save(path);
            var loaded = TownDesign.Load(path);

            Assert.Equal("Havenburg", loaded.TownName);
            Assert.Equal("Fort Kijkduin", loaded.Landmarks[0].Name);
            Assert.Equal("large", loaded.Landmarks[0].SizeCategory);
            Assert.Equal(3, loaded.KeyLocations.Count);
            Assert.Equal("compound", loaded.LayoutStyle);
            Assert.Single(loaded.Hazards);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_MapsAllFields()
    {
        var design = MakeDesign();
        var dir = Path.Combine(Path.GetTempPath(), "test-design-load-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "design.json");

        try
        {
            design.Save(path);
            var loaded = TownDesign.Load(path);

            Assert.Equal("Havenburg", loaded.TownName);
            Assert.Equal("Fort Kijkduin", loaded.Landmarks[0].Name);
            Assert.Equal("A massive coastal fortress with crumbling stone walls", loaded.Landmarks[0].VisualDescription);
            Assert.Equal(3, loaded.KeyLocations.Count);
            Assert.Equal("The Drydock Market", loaded.KeyLocations[0].Name);
            Assert.Equal("shop", loaded.KeyLocations[0].Purpose);
            Assert.Equal("compound", loaded.LayoutStyle);
            Assert.Single(loaded.Hazards);
            Assert.Equal("flooding", loaded.Hazards[0].Type);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "test-design-file-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "design.json");

        try
        {
            var original = MakeDesign();
            original.Save(path);

            Assert.True(File.Exists(path));

            var roundTripped = TownDesign.Load(path);

            Assert.Equal(original.TownName, roundTripped.TownName);
            Assert.Equal(original.Landmarks[0].Name, roundTripped.Landmarks[0].Name);
            Assert.Equal(original.Landmarks[0].VisualDescription, roundTripped.Landmarks[0].VisualDescription);
            Assert.Equal(original.Landmarks[0].SizeCategory, roundTripped.Landmarks[0].SizeCategory);
            Assert.Equal(original.KeyLocations.Count, roundTripped.KeyLocations.Count);
            Assert.Equal(original.LayoutStyle, roundTripped.LayoutStyle);
            Assert.Equal(original.Hazards.Count, roundTripped.Hazards.Count);

            for (int i = 0; i < original.KeyLocations.Count; i++)
            {
                Assert.Equal(original.KeyLocations[i].Name, roundTripped.KeyLocations[i].Name);
                Assert.Equal(original.KeyLocations[i].Purpose, roundTripped.KeyLocations[i].Purpose);
                Assert.Equal(original.KeyLocations[i].VisualDescription, roundTripped.KeyLocations[i].VisualDescription);
                Assert.Equal(original.KeyLocations[i].SizeCategory, roundTripped.KeyLocations[i].SizeCategory);
            }
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "test-design-dir-" + Guid.NewGuid().ToString("N"), "sub");
        var path = Path.Combine(dir, "design.json");

        try
        {
            MakeDesign().Save(path);

            Assert.True(Directory.Exists(dir));
            Assert.True(File.Exists(path));
        }
        finally
        {
            var root = Path.GetDirectoryName(dir)!;
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Save_ProducesValidJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), "test-design-json-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "design.json");

        try
        {
            var file = MakeDesign();
            file.Save(path);

            var json = File.ReadAllText(path);
            Assert.Contains("\"townName\"", json);       // camelCase
            Assert.Contains("\"landmarks\"", json);
            Assert.Contains("\"keyLocations\"", json);
            Assert.Contains("\"layoutStyle\"", json);
            Assert.Contains("\"hazards\"", json);
            Assert.Contains("Fort Kijkduin", json);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void RoundTrip_NewFields_Preserved()
    {
        var dir = Path.Combine(Path.GetTempPath(), "test-design-newfields-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "design.json");

        try
        {
            var original = MakeDesignWithNewFields();
            original.Save(path);

            var roundTripped = TownDesign.Load(path);

            // Landmark new fields
            Assert.Contains("Napoleonic", roundTripped.Landmarks[0].OriginalDescription);
            Assert.Contains("low-poly game asset", roundTripped.Landmarks[0].MeshyPrompt);
            Assert.Equal("north-west, near the coastline", roundTripped.Landmarks[0].PositionHint);

            // Key location new fields
            Assert.Contains("Willemsoord", roundTripped.KeyLocations[0].OriginalDescription);
            Assert.Contains("low-poly game asset", roundTripped.KeyLocations[0].MeshyPrompt);
            Assert.Equal("centre, on the main square", roundTripped.KeyLocations[0].PositionHint);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void RoundTrip_MultiLandmark_Preserved()
    {
        var dir = Path.Combine(Path.GetTempPath(), "test-design-multilm-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "design.json");

        try
        {
            var original = MakeDesignWithNewFields();
            Assert.Equal(2, original.Landmarks.Count);

            original.Save(path);

            var roundTripped = TownDesign.Load(path);

            Assert.Equal(2, roundTripped.Landmarks.Count);
            Assert.Equal("Fort Kijkduin", roundTripped.Landmarks[0].Name);
            Assert.Equal("The Lighthouse", roundTripped.Landmarks[1].Name);
            Assert.Contains("Lange Jaap", roundTripped.Landmarks[1].OriginalDescription);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
