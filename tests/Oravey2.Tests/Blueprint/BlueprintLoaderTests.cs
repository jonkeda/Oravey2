using Oravey2.Core.World.Blueprint;

namespace Oravey2.Tests.Blueprint;

public class BlueprintLoaderTests
{
    [Fact]
    public void LoadFromString_ValidJson_ParsesCorrectly()
    {
        var bp = TestBlueprints.Minimal();
        var json = System.Text.Json.JsonSerializer.Serialize(bp, BlueprintLoader.WriteOptions);
        var loaded = BlueprintLoader.LoadFromString(json);

        Assert.Equal("Test", loaded.Name);
        Assert.Equal(1, loaded.Dimensions.ChunksWide);
    }

    [Fact]
    public void LoadFromString_MinimalBlueprint_OnlyRequiredFields()
    {
        var bp = TestBlueprints.Minimal();
        var json = System.Text.Json.JsonSerializer.Serialize(bp, BlueprintLoader.WriteOptions);
        var loaded = BlueprintLoader.LoadFromString(json);

        Assert.Null(loaded.Water);
        Assert.Null(loaded.Roads);
        Assert.Null(loaded.Buildings);
        Assert.Null(loaded.Props);
        Assert.Null(loaded.Zones);
    }

    [Fact]
    public void LoadFromString_InvalidJson_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            BlueprintLoader.LoadFromString("not json"));
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            BlueprintLoader.Load("nonexistent_blueprint.json"));
    }
}
