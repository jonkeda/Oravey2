using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.Tests.World;

public class BuildingSerializationTests
{
    [Fact]
    public void BuildingDto_RoundTrip()
    {
        var original = new BuildingDto(
            "shop_001", "Corner Shop", "meshes/shop.glb", "small",
            [[2, 2], [3, 2], [2, 3], [3, 3]],
            Floors: 1, Condition: 0.8f, InteriorChunkId: null,
            new PlacementDto(0, 0, 2, 2));

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<BuildingDto>(json, ContentPackSerializer.ReadOptions)!;

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.MeshAsset, restored.MeshAsset);
        Assert.Equal(original.Size, restored.Size);
        Assert.Equal(original.Floors, restored.Floors);
        Assert.Equal(original.Condition, restored.Condition);
        Assert.Equal(original.InteriorChunkId, restored.InteriorChunkId);
        Assert.Equal(original.Placement, restored.Placement);
        Assert.Equal(original.Footprint!.Length, restored.Footprint!.Length);
    }

    [Fact]
    public void PropDto_RoundTrip()
    {
        var original = new PropDto(
            "car_001", "meshes/car.glb",
            new PlacementDto(0, 0, 5, 7),
            Rotation: 45f, Scale: 1.5f, BlocksWalkability: true,
            Footprint: [[5, 7], [6, 7]]);

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<PropDto>(json, ContentPackSerializer.ReadOptions)!;

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.MeshAsset, restored.MeshAsset);
        Assert.Equal(original.Placement, restored.Placement);
        Assert.Equal(original.Rotation, restored.Rotation);
        Assert.Equal(original.Scale, restored.Scale);
        Assert.Equal(original.BlocksWalkability, restored.BlocksWalkability);
        Assert.Equal(original.Footprint!.Length, restored.Footprint!.Length);
    }

    [Fact]
    public void SerializeDeserialize_Buildings_Collection()
    {
        var buildings = new[]
        {
            new BuildingDto("b1", "Building 1", "meshes/b1.glb", "small",
                [[0, 0], [1, 0]], 1, 1f, null, new PlacementDto(0, 0, 0, 0)),
            new BuildingDto("b2", "Building 2", "meshes/b2.glb", "large",
                [[4, 4], [5, 4], [6, 4], [4, 5], [5, 5]],
                2, 0.5f, "interior_b2", new PlacementDto(0, 0, 4, 4)),
        };

        var json = JsonSerializer.Serialize(buildings, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<BuildingDto[]>(json, ContentPackSerializer.ReadOptions);

        Assert.NotNull(restored);
        Assert.Equal(2, restored.Length);
        Assert.Equal("b1", restored[0].Id);
        Assert.Equal("b2", restored[1].Id);
        Assert.Equal("interior_b2", restored[1].InteriorChunkId);
    }

    [Fact]
    public void SerializeDeserialize_Props_Collection()
    {
        var props = new[]
        {
            new PropDto("p1", "meshes/barrel.glb",
                new PlacementDto(0, 0, 3, 3), 0f, 1f, false, null),
            new PropDto("p2", "meshes/car.glb",
                new PlacementDto(0, 0, 7, 2), 90f, 1f, true,
                [[7, 2], [8, 2]]),
        };

        var json = JsonSerializer.Serialize(props, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<PropDto[]>(json, ContentPackSerializer.ReadOptions);

        Assert.NotNull(restored);
        Assert.Equal(2, restored.Length);
        Assert.Equal("p1", restored[0].Id);
        Assert.False(restored[0].BlocksWalkability);
        Assert.Equal("p2", restored[1].Id);
        Assert.True(restored[1].BlocksWalkability);
    }

    [Fact]
    public void BuildingDto_NullOptionalFields_OmittedInJson()
    {
        var building = new BuildingDto(
            "b3", "Ruin", "meshes/ruin.glb", "small",
            Footprint: null, Floors: 1, Condition: 0.1f, InteriorChunkId: null,
            new PlacementDto(0, 0, 0, 0));

        var json = JsonSerializer.Serialize(building, ContentPackSerializer.WriteOptions);

        Assert.DoesNotContain("footprint", json);
        Assert.DoesNotContain("interiorChunkId", json);
    }
}
