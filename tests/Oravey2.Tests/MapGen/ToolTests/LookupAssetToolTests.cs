using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Tools;

namespace Oravey2.Tests.MapGen.ToolTests;

public class LookupAssetToolTests
{
    private static LookupAssetTool CreateTool()
    {
        var registry = new AssetRegistry(new Dictionary<string, List<AssetEntry>>
        {
            ["building"] = new()
            {
                new AssetEntry("buildings/ruined_office.glb", "Ruined office", new[] { "large", "ruin" }),
                new AssetEntry("buildings/shop.glb", "Corner shop", new[] { "small" })
            }
        });
        return new LookupAssetTool(registry);
    }

    [Fact]
    public void Handle_KnownAsset_ReturnsMatch()
    {
        var tool = CreateTool();
        var result = tool.Handle("building", "office");

        Assert.Contains("ruined_office", result);
        Assert.Contains("\"count\":1", result);
    }

    [Fact]
    public void Handle_UnknownAsset_ReturnsEmpty()
    {
        var tool = CreateTool();
        var result = tool.Handle("building", "castle");

        Assert.Contains("\"count\":0", result);
    }
}
