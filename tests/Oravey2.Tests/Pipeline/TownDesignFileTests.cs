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
}
