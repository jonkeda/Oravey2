using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.Tests.Serialization;

public sealed class DtoCompatibilityTests
{
    [Fact]
    public void BuildingDto_RoundTrips_AllFields()
    {
        var original = new BuildingDto
        {
            Id = "b1", Name = "House", MeshAsset = "meshes/house.glb", Size = "medium",
            Footprint = [[0, 0], [1, 0], [0, 1]],
            Floors = 2, Condition = 0.8f, InteriorChunkId = "chunk_interior_42",
            Placement = new PlacementDto { ChunkX = 1, ChunkY = 2, LocalTileX = 3, LocalTileY = 4 },
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
    public void PropDto_RoundTrips_AllFields()
    {
        var original = new PropDto
        {
            Id = "p1", MeshAsset = "meshes/barrel.glb",
            Placement = new PlacementDto { ChunkX = 0, ChunkY = 0, LocalTileX = 3, LocalTileY = 4 },
            Rotation = 90f, Scale = 1.5f, BlocksWalkability = true,
            Footprint = [[0, 0], [1, 0]],
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
    public void ManifestDto_RoundTrips_AllFields()
    {
        var original = new ManifestDto
        {
            Id = "oravey2.test",
            Name = "Test Pack",
            Version = "1.0.0",
            Description = "A test pack",
            Author = "TestAuthor",
            Parent = "oravey2.base",
        };

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<ManifestDto>(json, ContentPackSerializer.ReadOptions)!;

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Version, restored.Version);
        Assert.Equal(original.Description, restored.Description);
        Assert.Equal(original.Author, restored.Author);
        Assert.Equal(original.Parent, restored.Parent);
    }

    [Fact]
    public void ManifestDto_EngineVersion_RoundTrips()
    {
        var original = new ManifestDto
        {
            Id = "oravey2.test",
            Name = "Test Pack",
            Version = "1.0.0",
            Description = "A test pack",
            Author = "TestAuthor",
            Parent = "oravey2.base",
            EngineVersion = ">=2.0.0",
        };

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<ManifestDto>(json, ContentPackSerializer.ReadOptions)!;

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(">=2.0.0", restored.EngineVersion);
    }

    [Fact]
    public void ManifestDto_ExtensionData_PreservesUnknownFields()
    {
        var json = """{"id":"test","name":"Test","palette":{"primary":"#FF0000"}}""";
        var manifest = JsonSerializer.Deserialize<ManifestDto>(json, ContentPackSerializer.ReadOptions)!;

        Assert.Equal("test", manifest.Id);
        Assert.NotNull(manifest.ExtensionData);
        Assert.True(manifest.ExtensionData.ContainsKey("palette"));

        // Round-trip preserves the unknown field
        var reJson = JsonSerializer.Serialize(manifest, ContentPackSerializer.WriteOptions);
        Assert.Contains("palette", reJson);
    }

    [Fact]
    public void BuildingDto_EmptyFootprint_RoundTrips()
    {
        var original = new BuildingDto
        {
            Id = "b2", Name = "Ruin", MeshAsset = "meshes/ruin.glb", Size = "small",
            Footprint = [], Floors = 1, Condition = 0.1f, InteriorChunkId = null,
            Placement = new PlacementDto { ChunkX = 0, ChunkY = 0, LocalTileX = 0, LocalTileY = 0 },
        };

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<BuildingDto>(json, ContentPackSerializer.ReadOptions);

        Assert.NotNull(restored);
        Assert.Empty(restored.Footprint!);
    }
}
