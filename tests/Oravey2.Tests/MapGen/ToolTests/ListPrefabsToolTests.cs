using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Tools;

namespace Oravey2.Tests.MapGen.ToolTests;

public class ListPrefabsToolTests
{
    [Fact]
    public void Handle_KnownCategory_ReturnsEntries()
    {
        var registry = new AssetRegistry(new Dictionary<string, List<AssetEntry>>
        {
            ["building"] = new()
            {
                new AssetEntry("buildings/shop.glb", "Shop", new[] { "small" }),
                new AssetEntry("buildings/office.glb", "Office", new[] { "large" })
            }
        });
        var tool = new ListPrefabsTool(registry);

        var result = tool.Handle("building");

        Assert.Contains("\"count\":2", result);
        Assert.Contains("shop.glb", result);
        Assert.Contains("office.glb", result);
    }

    [Fact]
    public void Handle_UnknownCategory_ReturnsEmpty()
    {
        var registry = new AssetRegistry(new Dictionary<string, List<AssetEntry>>());
        var tool = new ListPrefabsTool(registry);

        var result = tool.Handle("weapon");

        Assert.Contains("\"count\":0", result);
    }
}
