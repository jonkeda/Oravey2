using System.Text.Json;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Tools;
using Oravey2.MapGen.Validation;

namespace Oravey2.Tests.MapGen.ToolTests;

public class ValidateBlueprintToolTests
{
    private static ValidateBlueprintTool CreateTool()
    {
        var assets = new AssetRegistry(new Dictionary<string, List<AssetEntry>>
        {
            ["surface"] = new() { new AssetEntry("Rock", "Rock", new[] { "natural" }) },
            ["building"] = new() { new AssetEntry("buildings/shop.glb", "Shop", new[] { "small" }) }
        });
        return new ValidateBlueprintTool(new TerrainBlueprintValidator(assets));
    }

    [Fact]
    public void Handle_ValidBlueprint_ReturnsValid()
    {
        var tool = CreateTool();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(1, 1),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null, null, null, null);

        var json = JsonSerializer.Serialize(bp, BlueprintLoader.WriteOptions);
        var result = tool.Handle(json);

        Assert.Contains("\"valid\":true", result);
    }

    [Fact]
    public void Handle_InvalidBlueprint_ReturnsErrors()
    {
        var tool = CreateTool();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(0, 0),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null, null, null, null);

        var json = JsonSerializer.Serialize(bp, BlueprintLoader.WriteOptions);
        var result = tool.Handle(json);

        Assert.Contains("\"valid\":false", result);
        Assert.Contains("INVALID_DIMENSIONS", result);
    }

    [Fact]
    public void Handle_MalformedJson_ReturnsParseError()
    {
        var tool = CreateTool();
        var result = tool.Handle("not valid json");

        Assert.Contains("\"valid\":false", result);
        Assert.Contains("JSON parse error", result);
    }
}
