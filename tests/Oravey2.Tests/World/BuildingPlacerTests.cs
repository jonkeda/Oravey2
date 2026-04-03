using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class BuildingPlacerTests
{
    private static TileMapData CreateGroundMap(int w, int h)
    {
        var map = new TileMapData(w, h);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                map.SetTileData(x, y, TileDataFactory.Ground());
        return map;
    }

    // --- ApplyFootprint ---

    [Fact]
    public void ApplyFootprint_SetsStructureId()
    {
        var map = CreateGroundMap(8, 8);
        var building = new BuildingDefinition(
            "shop", "Shop", "meshes/shop.glb",
            BuildingSize.Small,
            new[] { (2, 2), (3, 2), (2, 3), (3, 3) },
            1, 1f, null);

        BuildingPlacer.ApplyFootprint(map, building);

        foreach (var (fx, fy) in building.Footprint)
            Assert.NotEqual(0, map.GetTileData(fx, fy).StructureId);
    }

    [Fact]
    public void ApplyFootprint_ClearsWalkable()
    {
        var map = CreateGroundMap(8, 8);
        var building = new BuildingDefinition(
            "shop", "Shop", "meshes/shop.glb",
            BuildingSize.Small,
            new[] { (2, 2), (3, 2) },
            1, 1f, null);

        BuildingPlacer.ApplyFootprint(map, building);

        Assert.False(map.GetTileData(2, 2).IsWalkable);
        Assert.False(map.GetTileData(3, 2).IsWalkable);
    }

    [Fact]
    public void ApplyFootprint_NonFootprintTilesUnchanged()
    {
        var map = CreateGroundMap(8, 8);
        var building = new BuildingDefinition(
            "shop", "Shop", "meshes/shop.glb",
            BuildingSize.Small,
            new[] { (2, 2) },
            1, 1f, null);

        BuildingPlacer.ApplyFootprint(map, building);

        // Adjacent tile unchanged
        Assert.True(map.GetTileData(3, 2).IsWalkable);
        Assert.Equal(0, map.GetTileData(3, 2).StructureId);
    }

    // --- ValidatePlacement ---

    [Fact]
    public void Validate_InBounds_NoOverlap_ReturnsTrue()
    {
        var map = CreateGroundMap(8, 8);
        Assert.True(BuildingPlacer.ValidatePlacement(map, new[] { (2, 2), (3, 2) }));
    }

    [Fact]
    public void Validate_OutOfBounds_ReturnsFalse()
    {
        var map = CreateGroundMap(8, 8);
        Assert.False(BuildingPlacer.ValidatePlacement(map, new[] { (7, 7), (8, 7) }));
    }

    [Fact]
    public void Validate_OverlapsExisting_ReturnsFalse()
    {
        var map = CreateGroundMap(8, 8);

        // Place first building
        var b1 = new BuildingDefinition(
            "b1", "B1", "meshes/b1.glb",
            BuildingSize.Small, new[] { (2, 2), (3, 2) },
            1, 1f, null);
        BuildingPlacer.ApplyFootprint(map, b1);

        // Try to place overlapping
        Assert.False(BuildingPlacer.ValidatePlacement(map, new[] { (3, 2), (4, 2) }));
    }

    // --- ApplyPropFootprint ---

    [Fact]
    public void ApplyPropFootprint_Blocking_MakesTilesNonWalkable()
    {
        var map = CreateGroundMap(8, 8);
        var prop = new PropDefinition(
            "car", "meshes/car.glb", 0, 0, 3, 3, 0f, 1f, true,
            new[] { (3, 3), (4, 3) });

        BuildingPlacer.ApplyPropFootprint(map, prop);

        Assert.False(map.GetTileData(3, 3).IsWalkable);
        Assert.False(map.GetTileData(4, 3).IsWalkable);
    }

    [Fact]
    public void ApplyPropFootprint_NonBlocking_TilesUnchanged()
    {
        var map = CreateGroundMap(8, 8);
        var prop = new PropDefinition(
            "barrel", "meshes/barrel.glb", 0, 0, 5, 5, 0f, 1f, false, null);

        BuildingPlacer.ApplyPropFootprint(map, prop);

        Assert.True(map.GetTileData(5, 5).IsWalkable);
    }
}
