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
        var original = new ManifestDto(
            "oravey2.test", "Test Pack", "1.0.0",
            "A test pack", "TestAuthor", "oravey2.base");

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<ManifestDto>(json, ContentPackSerializer.ReadOptions);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void ManifestDto_EngineVersion_RoundTrips()
    {
        var original = new ManifestDto(
            "oravey2.test", "Test Pack", "1.0.0",
            "A test pack", "TestAuthor", "oravey2.base",
            EngineVersion: ">=2.0.0");

        var json = JsonSerializer.Serialize(original, ContentPackSerializer.WriteOptions);
        var restored = JsonSerializer.Deserialize<ManifestDto>(json, ContentPackSerializer.ReadOptions);

        Assert.Equal(original, restored);
        Assert.Equal(">=2.0.0", restored!.EngineVersion);
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
