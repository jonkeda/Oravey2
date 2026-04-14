using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.Tests.World;

public class BuildingSerializationTests
{
    [Fact]
    public void BuildingDto_RoundTrip()
    {
        var original = new BuildingDto
        {
            Id = "shop_001", Name = "Corner Shop", MeshAsset = "meshes/shop.glb", Size = "small",
            Footprint = [[2, 2], [3, 2], [2, 3], [3, 3]],
            Floors = 1, Condition = 0.8f, InteriorChunkId = null,
            Placement = new PlacementDto { ChunkX = 0, ChunkY = 0, LocalTileX = 2, LocalTileY = 2 },
        };

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<BuildingDto>(json, ContentPackSerializer.ReadOptions)!;

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.MeshAsset, restored.MeshAsset);
        Assert.Equal(original.Size, restored.Size);
        Assert.Equal(original.Floors, restored.Floors);
        Assert.Equal(original.Condition, restored.Condition);
        Assert.Equal(original.InteriorChunkId, restored.InteriorChunkId);
        Assert.Equal(original.Placement!.ChunkX, restored.Placement!.ChunkX);
        Assert.Equal(original.Placement.ChunkY, restored.Placement.ChunkY);
        Assert.Equal(original.Placement.LocalTileX, restored.Placement.LocalTileX);
        Assert.Equal(original.Placement.LocalTileY, restored.Placement.LocalTileY);
        Assert.Equal(original.Footprint!.Length, restored.Footprint!.Length);
    }

    [Fact]
    public void PropDto_RoundTrip()
    {
        var original = new PropDto
        {
            Id = "car_001", MeshAsset = "meshes/car.glb",
            Placement = new PlacementDto { ChunkX = 0, ChunkY = 0, LocalTileX = 5, LocalTileY = 7 },
            Rotation = 45f, Scale = 1.5f, BlocksWalkability = true,
            Footprint = [[5, 7], [6, 7]],
        };

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<PropDto>(json, ContentPackSerializer.ReadOptions)!;

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.MeshAsset, restored.MeshAsset);
        Assert.Equal(original.Placement!.ChunkX, restored.Placement!.ChunkX);
        Assert.Equal(original.Placement.ChunkY, restored.Placement.ChunkY);
        Assert.Equal(original.Placement.LocalTileX, restored.Placement.LocalTileX);
        Assert.Equal(original.Placement.LocalTileY, restored.Placement.LocalTileY);
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
            new BuildingDto { Id = "b1", Name = "Building 1", MeshAsset = "meshes/b1.glb", Size = "small",
                Footprint = [[0, 0], [1, 0]], Floors = 1, Condition = 1f, InteriorChunkId = null,
                Placement = new PlacementDto { ChunkX = 0, ChunkY = 0, LocalTileX = 0, LocalTileY = 0 } },
            new BuildingDto { Id = "b2", Name = "Building 2", MeshAsset = "meshes/b2.glb", Size = "large",
                Footprint = [[4, 4], [5, 4], [6, 4], [4, 5], [5, 5]],
                Floors = 2, Condition = 0.5f, InteriorChunkId = "interior_b2",
                Placement = new PlacementDto { ChunkX = 0, ChunkY = 0, LocalTileX = 4, LocalTileY = 4 } },
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
            new PropDto { Id = "p1", MeshAsset = "meshes/barrel.glb",
                Placement = new PlacementDto { ChunkX = 0, ChunkY = 0, LocalTileX = 3, LocalTileY = 3 },
                Rotation = 0f, Scale = 1f, BlocksWalkability = false, Footprint = null },
            new PropDto { Id = "p2", MeshAsset = "meshes/car.glb",
                Placement = new PlacementDto { ChunkX = 0, ChunkY = 0, LocalTileX = 7, LocalTileY = 2 },
                Rotation = 90f, Scale = 1f, BlocksWalkability = true,
                Footprint = [[7, 2], [8, 2]] },
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
        var building = new BuildingDto
        {
            Id = "b3", Name = "Ruin", MeshAsset = "meshes/ruin.glb", Size = "small",
            Footprint = null, Floors = 1, Condition = 0.1f, InteriorChunkId = null,
            Placement = new PlacementDto { ChunkX = 0, ChunkY = 0, LocalTileX = 0, LocalTileY = 0 },
        };

        var json = JsonSerializer.Serialize(building, ContentPackSerializer.WriteOptions);

        Assert.DoesNotContain("footprint", json);
        Assert.DoesNotContain("interiorChunkId", json);
    }
}
