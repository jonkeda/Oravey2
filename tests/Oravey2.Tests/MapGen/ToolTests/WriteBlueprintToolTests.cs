using System.Text.Json;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Tools;
using Oravey2.MapGen.Validation;

namespace Oravey2.Tests.MapGen.ToolTests;

public class WriteBlueprintToolTests
{
    private static WriteBlueprintTool CreateTool()
    {
        var assets = new AssetRegistry(new Dictionary<string, List<AssetEntry>>
        {
            ["surface"] = new() { new AssetEntry("Rock", "Rock", new[] { "natural" }) },
            ["building"] = new() { new AssetEntry("buildings/shop.glb", "Shop", new[] { "small" }) }
        });
        return new WriteBlueprintTool(new TerrainBlueprintValidator(assets));
    }

    [Fact]
    public void Handle_ValidBlueprint_Accepted()
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

        Assert.Contains("\"accepted\":true", result);
        Assert.NotNull(tool.LastAcceptedBlueprint);
        Assert.NotNull(tool.LastAcceptedJson);
    }

    [Fact]
    public void Handle_InvalidDimensions_Rejected()
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

        Assert.Contains("\"accepted\":false", result);
        Assert.Contains("INVALID_DIMENSIONS", result);
        Assert.Null(tool.LastAcceptedBlueprint);
    }

    [Fact]
    public void Handle_OverlappingBuildings_Rejected()
    {
        var tool = CreateTool();
        var buildings = new[]
        {
            new BuildingBlueprint("b1", "Shop A", "buildings/shop.glb", "Small", 0, 0, 3, 3, 1, 0.8f, null),
            new BuildingBlueprint("b2", "Shop B", "buildings/shop.glb", "Small", 1, 1, 3, 3, 1, 0.7f, null)
        };
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(2, 2),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null, buildings, null, null);

        var json = JsonSerializer.Serialize(bp, BlueprintLoader.WriteOptions);
        var result = tool.Handle(json);

        Assert.Contains("\"accepted\":false", result);
        Assert.Contains("overlap", result, StringComparison.OrdinalIgnoreCase);
        Assert.Null(tool.LastAcceptedBlueprint);
    }

    [Fact]
    public void Handle_MalformedJson_Rejected()
    {
        var tool = CreateTool();
        var result = tool.Handle("not valid json");

        Assert.Contains("\"accepted\":false", result);
        Assert.Contains("parse error", result, StringComparison.OrdinalIgnoreCase);
        Assert.Null(tool.LastAcceptedBlueprint);
    }

    [Fact]
    public void Handle_SecondCall_OverwritesPrevious()
    {
        var tool = CreateTool();
        var bp1 = new MapBlueprint(
            "First", "First map",
            new BlueprintSource("X", null),
            new BlueprintDimensions(1, 1),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null, null, null, null);
        var bp2 = new MapBlueprint(
            "Second", "Second map",
            new BlueprintSource("Y", null),
            new BlueprintDimensions(2, 2),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null, null, null, null);

        tool.Handle(JsonSerializer.Serialize(bp1, BlueprintLoader.WriteOptions));
        Assert.Equal("First", tool.LastAcceptedBlueprint!.Name);

        tool.Handle(JsonSerializer.Serialize(bp2, BlueprintLoader.WriteOptions));
        Assert.Equal("Second", tool.LastAcceptedBlueprint!.Name);
    }
}
