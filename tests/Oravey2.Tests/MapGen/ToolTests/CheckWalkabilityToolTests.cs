using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Spatial;
using Oravey2.MapGen.Tools;

namespace Oravey2.Tests.MapGen.ToolTests;

public class CheckWalkabilityToolTests
{
    [Fact]
    public void Handle_WaterTile_NotWalkable()
    {
        var tool = new CheckWalkabilityTool();
        var water = new WaterBlueprint(
            null,
            new[] { new LakeBlueprint("l1", 5, 5, 3, 5, 3) });

        var result = tool.Handle(0, 0, 5, 5, 2, 2, water, Array.Empty<BuildingFootprint>());

        Assert.Contains("\"walkable\":false", result);
        Assert.Contains("\"onWater\":true", result);
    }

    [Fact]
    public void Handle_OpenTile_Walkable()
    {
        var tool = new CheckWalkabilityTool();

        var result = tool.Handle(0, 0, 0, 0, 2, 2, null, Array.Empty<BuildingFootprint>());

        Assert.Contains("\"walkable\":true", result);
    }

    [Fact]
    public void Handle_BuildingTile_NotWalkable()
    {
        var tool = new CheckWalkabilityTool();
        var buildings = new[] { new BuildingFootprint("b1", 5, 5, 3, 3) };

        var result = tool.Handle(0, 0, 6, 6, 2, 2, null, buildings);

        Assert.Contains("\"walkable\":false", result);
        Assert.Contains("\"onBuilding\":true", result);
    }
}
