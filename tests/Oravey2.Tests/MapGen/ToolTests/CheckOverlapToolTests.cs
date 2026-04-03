using Oravey2.MapGen.Spatial;
using Oravey2.MapGen.Tools;

namespace Oravey2.Tests.MapGen.ToolTests;

public class CheckOverlapToolTests
{
    [Fact]
    public void Handle_OverlappingFootprints_Detected()
    {
        var tool = new CheckOverlapTool();
        var footprints = new[]
        {
            new BuildingFootprint("a", 0, 0, 5, 5),
            new BuildingFootprint("b", 3, 3, 5, 5)
        };

        var result = tool.Handle(footprints);
        Assert.Contains("\"hasOverlaps\":true", result);
    }

    [Fact]
    public void Handle_NoOverlaps_Clean()
    {
        var tool = new CheckOverlapTool();
        var footprints = new[]
        {
            new BuildingFootprint("a", 0, 0, 3, 3),
            new BuildingFootprint("b", 10, 10, 3, 3)
        };

        var result = tool.Handle(footprints);
        Assert.Contains("\"hasOverlaps\":false", result);
    }
}
