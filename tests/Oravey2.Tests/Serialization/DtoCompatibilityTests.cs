using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.Tests.Serialization;

public sealed class DtoCompatibilityTests
{
    [Fact]
    public void BuildingDto_RoundTrips_AllFields()
    {
        var original = new BuildingDto(
            "b1", "House", "meshes/house.glb", "medium",
            [[0, 0], [1, 0], [0, 1]],
            Floors: 2, Condition: 0.8f, InteriorChunkId: "chunk_interior_42",
            new PlacementDto(1, 2, 3, 4));

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
    public void PropDto_RoundTrips_AllFields()
    {
        var original = new PropDto(
            "p1", "meshes/barrel.glb", new PlacementDto(0, 0, 3, 4),
            Rotation: 90f, Scale: 1.5f, BlocksWalkability: true,
            Footprint: [[0, 0], [1, 0]]);

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
        var original = new BuildingDto(
            "b2", "Ruin", "meshes/ruin.glb", "small",
            Footprint: [], Floors: 1, Condition: 0.1f, InteriorChunkId: null,
            new PlacementDto(0, 0, 0, 0));

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<BuildingDto>(json, ContentPackSerializer.ReadOptions);

        Assert.NotNull(restored);
        Assert.Empty(restored.Footprint!);
    }
}
