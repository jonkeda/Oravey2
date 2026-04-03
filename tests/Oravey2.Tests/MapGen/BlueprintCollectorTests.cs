using System.Text.Json;
using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Services;

namespace Oravey2.Tests.MapGen;

public class BlueprintCollectorTests
{
    [Fact]
    public void CollectFromResponse_JsonInCodeFence_ExtractsCorrectly()
    {
        var collector = new BlueprintCollector();
        var bp = MinimalBlueprint();
        var json = JsonSerializer.Serialize(bp, BlueprintLoader.WriteOptions);
        var response = $"Here is the blueprint:\n```json\n{json}\n```\nDone!";

        var result = collector.CollectFromResponse(response, TimeSpan.FromSeconds(5));

        Assert.True(result.Success);
        Assert.NotNull(result.Blueprint);
        Assert.Equal("Test", result.Blueprint!.Name);
    }

    [Fact]
    public void CollectFromResponse_BareJson_ExtractsCorrectly()
    {
        var collector = new BlueprintCollector();
        var bp = MinimalBlueprint();
        var json = JsonSerializer.Serialize(bp, BlueprintLoader.WriteOptions);
        var response = $"Blueprint output: {json}";

        var result = collector.CollectFromResponse(response, TimeSpan.FromSeconds(3));

        Assert.True(result.Success);
        Assert.NotNull(result.Blueprint);
    }

    [Fact]
    public void CollectFromResponse_NoJson_ReturnsError()
    {
        var collector = new BlueprintCollector();
        var result = collector.CollectFromResponse("No JSON here, just text.", TimeSpan.FromSeconds(1));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("No JSON", result.ErrorMessage);
    }

    [Fact]
    public void CollectFromResponse_MalformedJson_ReturnsError()
    {
        var collector = new BlueprintCollector();
        var response = "```json\n{ broken: not-json }\n```";

        var result = collector.CollectFromResponse(response, TimeSpan.FromSeconds(1));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ExtractJson_CodeFence_ExtractsContent()
    {
        var json = "{\"name\":\"test\"}";
        var response = $"Some text\n```json\n{json}\n```\nMore text";

        var extracted = BlueprintCollector.ExtractJson(response);

        Assert.Equal(json, extracted);
    }

    [Fact]
    public void ExtractJson_NoJson_ReturnsNull()
    {
        var extracted = BlueprintCollector.ExtractJson("No JSON here at all");
        Assert.Null(extracted);
    }

    [Fact]
    public void CollectFromResponse_SetsElapsed()
    {
        var collector = new BlueprintCollector();
        var elapsed = TimeSpan.FromSeconds(12.3);
        var result = collector.CollectFromResponse("no json", elapsed);

        Assert.Equal(elapsed, result.Elapsed);
    }

    [Fact]
    public void CollectFromResponse_SetsSessionId()
    {
        var collector = new BlueprintCollector();
        var result = collector.CollectFromResponse("no json", TimeSpan.Zero, "sess-123");

        Assert.Equal("sess-123", result.SessionId);
    }

    private static MapBlueprint MinimalBlueprint() => new(
        "Test", "Test map",
        new BlueprintSource("Testville", null),
        new BlueprintDimensions(1, 1),
        new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
        null, null, null, null, null);
}
