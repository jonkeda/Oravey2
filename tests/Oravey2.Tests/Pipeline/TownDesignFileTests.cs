using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Pipeline;

public class TownDesignFileTests
{
    private static TownDesign MakeDesign() => new(
        "Havenburg",
        [new LandmarkBuilding("Fort Kijkduin", "A massive coastal fortress with crumbling stone walls", "large", "", "", "")],
        [
            new KeyLocation("The Drydock Market", "shop", "An old naval drydock converted into a marketplace", "medium", "", "", ""),
            new KeyLocation("Clinic", "medical", "A converted church clinic", "small", "", "", ""),
            new KeyLocation("Barracks", "barracks", "Reinforced concrete bunker", "medium", "", "", ""),
        ],
        "compound",
        [new EnvironmentalHazard("flooding", "The harbour district floods at high tide", "south-west waterfront")]);

    private static TownDesign MakeDesignWithNewFields() => new(
        "Havenburg",
        [
            new LandmarkBuilding("Fort Kijkduin", "A massive coastal fortress", "large",
                "Fort Kijkduin, a 19th-century Napoleonic coastal defence fort", 
                "Ruined stone fortress, crumbling walls, overgrown, low-poly game asset",
                "north-west, near the coastline"),
            new LandmarkBuilding("The Lighthouse", "A crumbling lighthouse tower", "medium",
                "Lange Jaap lighthouse, tallest cast-iron lighthouse in Europe, built 1877",
                "Tall crumbling cast-iron lighthouse, broken glass, rust, low-poly game asset",
                "north, on the harbour pier"),
        ],
        [
            new KeyLocation("The Drydock Market", "shop", "An old naval drydock marketplace", "medium",
                "Willemsoord drydock, former Royal Netherlands Navy shipyard, 19th century",
                "Ruined industrial drydock with market stalls, low-poly game asset",
                "centre, on the main square"),
        ],
        "compound",
        [new EnvironmentalHazard("flooding", "The harbour floods at high tide", "south-west waterfront")]);

    [Fact]
    public void FromTownDesign_MapsAllFields()
    {
        var design = MakeDesign();
        var file = TownDesignFile.FromTownDesign(design);

        Assert.Equal("Havenburg", file.TownName);
        Assert.Equal("Fort Kijkduin", file.Landmarks[0].Name);
        Assert.Equal("large", file.Landmarks[0].SizeCategory);
        Assert.Equal(3, file.KeyLocations.Count);
        Assert.Equal("compound", file.LayoutStyle);
        Assert.Single(file.Hazards);
    }

    [Fact]
    public void ToTownDesign_MapsAllFields()
    {
        var file = TownDesignFile.FromTownDesign(MakeDesign());
        var design = file.ToTownDesign();

        Assert.Equal("Havenburg", design.TownName);
        Assert.Equal("Fort Kijkduin", design.Landmarks[0].Name);
        Assert.Equal("A massive coastal fortress with crumbling stone walls", design.Landmarks[0].VisualDescription);
        Assert.Equal(3, design.KeyLocations.Count);
        Assert.Equal("The Drydock Market", design.KeyLocations[0].Name);
        Assert.Equal("shop", design.KeyLocations[0].Purpose);
        Assert.Equal("compound", design.LayoutStyle);
        Assert.Single(design.Hazards);
        Assert.Equal("flooding", design.Hazards[0].Type);
    }

    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "test-design-file-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "design.json");

        try
        {
            var original = MakeDesign();
            var file = TownDesignFile.FromTownDesign(original);
            file.Save(path);

            Assert.True(File.Exists(path));

            var loaded = TownDesignFile.Load(path);
            var roundTripped = loaded.ToTownDesign();

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
            var file = TownDesignFile.FromTownDesign(MakeDesign());
            file.Save(path);

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
            var file = TownDesignFile.FromTownDesign(MakeDesign());
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
            var file = TownDesignFile.FromTownDesign(original);
            file.Save(path);

            var loaded = TownDesignFile.Load(path);
            var roundTripped = loaded.ToTownDesign();

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

            var file = TownDesignFile.FromTownDesign(original);
            file.Save(path);

            var loaded = TownDesignFile.Load(path);
            var roundTripped = loaded.ToTownDesign();

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
