using Oravey2.Core.AI.Pathfinding;
using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.World;

public class BuildingFixtureTests
{
    private static string GetFixturesDir()
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Maps");

    private static (TileMapData Map, BuildingDefinition[] Buildings, PropDefinition[] Props) LoadFixture()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_buildings");
        var world = MapLoader.LoadWorldFull(dir);
        var map = world.GetChunk(0, 0)!.Tiles;

        var buildingJsons = BuildingSerializer.LoadBuildings(dir);
        var buildings = buildingJsons.Select(BuildingSerializer.FromBuildingJson).ToArray();

        var propJsons = BuildingSerializer.LoadProps(dir);
        var props = propJsons.Select(BuildingSerializer.FromPropJson).ToArray();

        // Apply footprints to map
        foreach (var b in buildings)
            BuildingPlacer.ApplyFootprint(map, b);
        foreach (var p in props)
            BuildingPlacer.ApplyPropFootprint(map, p);

        return (map, buildings, props);
    }

    [Fact]
    public void LoadFixture_HasTwoBuildings()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_buildings");
        var buildingJsons = BuildingSerializer.LoadBuildings(dir);
        Assert.Equal(2, buildingJsons.Length);
    }

    [Fact]
    public void LoadFixture_BuildingRegistryPopulated()
    {
        var dir = Path.Combine(GetFixturesDir(), "test_buildings");
        var buildingJsons = BuildingSerializer.LoadBuildings(dir);
        var registry = new BuildingRegistry();
        foreach (var bj in buildingJsons)
        {
            var building = BuildingSerializer.FromBuildingJson(bj);
            registry.RegisterForChunk(building, bj.Placement.ChunkX, bj.Placement.ChunkY);
        }

        Assert.Equal(2, registry.GetAll().Count);
        Assert.NotNull(registry.GetById("shop_001"));
        Assert.NotNull(registry.GetById("warehouse_001"));
    }

    [Fact]
    public void SmallBuilding_FootprintNonWalkable()
    {
        var (map, buildings, _) = LoadFixture();
        var shop = buildings.First(b => b.Id == "shop_001");

        foreach (var (fx, fy) in shop.Footprint)
        {
            Assert.False(map.GetTileData(fx, fy).IsWalkable,
                $"Building footprint tile ({fx},{fy}) should not be walkable");
        }
    }

    [Fact]
    public void LargeBuilding_HasInteriorChunkId()
    {
        var (_, buildings, _) = LoadFixture();
        var warehouse = buildings.First(b => b.Id == "warehouse_001");
        Assert.Equal("interior_warehouse_001", warehouse.InteriorChunkId);
    }

    [Fact]
    public void LargeBuilding_FootprintNonWalkable()
    {
        var (map, buildings, _) = LoadFixture();
        var warehouse = buildings.First(b => b.Id == "warehouse_001");

        foreach (var (fx, fy) in warehouse.Footprint)
        {
            Assert.False(map.GetTileData(fx, fy).IsWalkable,
                $"Warehouse footprint tile ({fx},{fy}) should not be walkable");
        }
    }

    [Fact]
    public void BlockingProp_FootprintNonWalkable()
    {
        var (map, _, props) = LoadFixture();
        var car = props.First(p => p.Id == "car_wreck_001");

        Assert.True(car.BlocksWalkability);
        foreach (var (fx, fy) in car.Footprint!)
        {
            Assert.False(map.GetTileData(fx, fy).IsWalkable,
                $"Blocking prop tile ({fx},{fy}) should not be walkable");
        }
    }

    [Fact]
    public void NonBlockingProp_TileStillWalkable()
    {
        var (map, _, props) = LoadFixture();
        var barrel = props.First(p => p.Id == "barrel_001");

        Assert.False(barrel.BlocksWalkability);
        Assert.True(map.GetTileData(barrel.LocalTileX, barrel.LocalTileY).IsWalkable);
    }

    [Fact]
    public void Pathfinder_RoutesAroundBuildingFootprint()
    {
        var (map, _, _) = LoadFixture();
        var pathfinder = new TileGridPathfinder();

        // Try to path through shop footprint area (2,2)-(3,3)
        // From (0,2) to (5,2) — must go around the shop
        var result = pathfinder.FindPath(0, 2, 5, 2, map);
        Assert.True(result.Found);

        // Path should not pass through any building footprint tile
        foreach (var step in result.Path)
        {
            var tile = map.GetTileData(step.X, step.Y);
            Assert.True(tile.IsWalkable, $"Path step ({step.X},{step.Y}) should be walkable");
        }
    }
}
