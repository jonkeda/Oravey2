using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class BuildingDefinitionTests
{
    [Fact]
    public void SmallBuilding_FootprintLessThanOrEqual4_InteriorNull()
    {
        var building = new BuildingDefinition(
            "shop_001", "Corner Shop", "meshes/shop.glb",
            BuildingSize.Small,
            new[] { (2, 2), (3, 2), (2, 3), (3, 3) },
            1, 0.8f, null);

        Assert.Equal(BuildingSize.Small, building.Size);
        Assert.True(building.Footprint.Length <= 4);
        Assert.Null(building.InteriorChunkId);
    }

    [Fact]
    public void LargeBuilding_FootprintGreaterThanOrEqual5()
    {
        var building = new BuildingDefinition(
            "warehouse_001", "Abandoned Warehouse", "meshes/warehouse.glb",
            BuildingSize.Large,
            new[] { (4, 4), (5, 4), (6, 4), (4, 5), (5, 5), (6, 5) },
            2, 0.5f, "interior_warehouse_001");

        Assert.Equal(BuildingSize.Large, building.Size);
        Assert.True(building.Footprint.Length >= 5);
    }

    [Fact]
    public void MeshAssetPath_NotNullOrEmpty()
    {
        var building = new BuildingDefinition(
            "hut_001", "Scrap Hut", "meshes/hut.glb",
            BuildingSize.Small,
            new[] { (1, 1) },
            1, 1.0f, null);

        Assert.False(string.IsNullOrEmpty(building.MeshAssetPath));
    }

    [Fact]
    public void Condition_StoredAsProvided()
    {
        var building = new BuildingDefinition(
            "ruin_001", "Ruins", "meshes/ruin.glb",
            BuildingSize.Small,
            new[] { (0, 0), (1, 0) },
            1, 0.3f, null);

        Assert.Equal(0.3f, building.Condition);
    }
}
