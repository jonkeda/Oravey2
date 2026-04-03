using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.World;

public class BuildingSerializationTests
{
    [Fact]
    public void BuildingDefinition_RoundTrip()
    {
        var building = new BuildingDefinition(
            "shop_001", "Corner Shop", "meshes/shop.glb",
            BuildingSize.Small,
            new[] { (2, 2), (3, 2), (2, 3), (3, 3) },
            1, 0.8f, null);

        var json = BuildingSerializer.ToBuildingJson(building, 0, 0);
        var restored = BuildingSerializer.FromBuildingJson(json);

        Assert.Equal(building.Id, restored.Id);
        Assert.Equal(building.Name, restored.Name);
        Assert.Equal(building.MeshAssetPath, restored.MeshAssetPath);
        Assert.Equal(building.Size, restored.Size);
        Assert.Equal(building.Footprint.Length, restored.Footprint.Length);
        Assert.Equal(building.Floors, restored.Floors);
        Assert.Equal(building.Condition, restored.Condition);
        Assert.Equal(building.InteriorChunkId, restored.InteriorChunkId);
    }

    [Fact]
    public void PropDefinition_RoundTrip()
    {
        var prop = new PropDefinition(
            "car_001", "meshes/car.glb",
            0, 0, 5, 7, 45f, 1.5f, true,
            new[] { (5, 7), (6, 7) });

        var json = BuildingSerializer.ToPropJson(prop);
        var restored = BuildingSerializer.FromPropJson(json);

        Assert.Equal(prop.Id, restored.Id);
        Assert.Equal(prop.MeshAssetPath, restored.MeshAssetPath);
        Assert.Equal(prop.ChunkX, restored.ChunkX);
        Assert.Equal(prop.ChunkY, restored.ChunkY);
        Assert.Equal(prop.LocalTileX, restored.LocalTileX);
        Assert.Equal(prop.LocalTileY, restored.LocalTileY);
        Assert.Equal(prop.RotationDegrees, restored.RotationDegrees);
        Assert.Equal(prop.Scale, restored.Scale);
        Assert.Equal(prop.BlocksWalkability, restored.BlocksWalkability);
        Assert.Equal(prop.Footprint!.Length, restored.Footprint!.Length);
    }

    [Fact]
    public void SerializeDeserialize_Buildings_Json()
    {
        var buildings = new[]
        {
            new BuildingJson("b1", "Building 1", "meshes/b1.glb", "Small",
                new[] { new[] { 0, 0 }, new[] { 1, 0 } }, 1, 1f, null,
                new PlacementJson(0, 0, 0, 0)),
            new BuildingJson("b2", "Building 2", "meshes/b2.glb", "Large",
                new[] { new[] { 4, 4 }, new[] { 5, 4 }, new[] { 6, 4 }, new[] { 4, 5 }, new[] { 5, 5 } },
                2, 0.5f, "interior_b2",
                new PlacementJson(0, 0, 4, 4))
        };

        var json = BuildingSerializer.SerializeBuildings(buildings);
        var restored = BuildingSerializer.DeserializeBuildings(json);

        Assert.Equal(2, restored.Length);
        Assert.Equal("b1", restored[0].Id);
        Assert.Equal("b2", restored[1].Id);
    }

    [Fact]
    public void SerializeDeserialize_Props_Json()
    {
        var props = new[]
        {
            new PropJson("p1", "meshes/barrel.glb",
                new PlacementJson(0, 0, 3, 3), 0f, 1f, false, null),
            new PropJson("p2", "meshes/car.glb",
                new PlacementJson(0, 0, 7, 2), 90f, 1f, true,
                new[] { new[] { 7, 2 }, new[] { 8, 2 } })
        };

        var json = BuildingSerializer.SerializeProps(props);
        var restored = BuildingSerializer.DeserializeProps(json);

        Assert.Equal(2, restored.Length);
        Assert.Equal("p1", restored[0].Id);
        Assert.False(restored[0].BlocksWalkability);
        Assert.Equal("p2", restored[1].Id);
        Assert.True(restored[1].BlocksWalkability);
    }

    [Fact]
    public void LoadBuildings_MissingFile_ReturnsEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"oravey_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = BuildingSerializer.LoadBuildings(tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadProps_MissingFile_ReturnsEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"oravey_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = BuildingSerializer.LoadProps(tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
