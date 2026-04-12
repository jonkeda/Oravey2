using System.Text.Json;
using System.Text.Json.Serialization;
using Oravey2.Core.Content;
using Oravey2.Core.World.Serialization;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;

namespace Oravey2.Tests.Serialization;

public sealed class DtoCompatibilityTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void BuildingFile_WithInteriorChunkId_DeserializesAsBuildingJson()
    {
        var mapGen = new BuildingFile
        {
            Id = "b1",
            Name = "House",
            MeshAsset = "meshes/house.glb",
            Size = "medium",
            Footprint = [[0, 0], [1, 0], [0, 1]],
            Floors = 2,
            Condition = 0.8f,
            InteriorChunkId = "chunk_interior_42",
            Placement = new PlacementFile { ChunkX = 1, ChunkY = 2, LocalTileX = 3, LocalTileY = 4 },
        };

        var json = JsonSerializer.Serialize(mapGen, Options);
        var core = JsonSerializer.Deserialize<BuildingJson>(json, Options);

        Assert.NotNull(core);
        Assert.Equal("b1", core.Id);
        Assert.Equal("chunk_interior_42", core.InteriorChunkId);
        Assert.Equal(3, core.Footprint.Length);
    }

    [Fact]
    public void PropFile_WithFootprint_DeserializesAsPropJson()
    {
        var mapGen = new PropFile
        {
            Id = "p1",
            MeshAsset = "meshes/barrel.glb",
            Rotation = 90f,
            Scale = 1.5f,
            BlocksWalkability = true,
            Footprint = [[0, 0], [1, 0]],
        };

        var json = JsonSerializer.Serialize(mapGen, Options);
        var core = JsonSerializer.Deserialize<PropJson>(json, Options);

        Assert.NotNull(core);
        Assert.Equal("p1", core.Id);
        Assert.NotNull(core.Footprint);
        Assert.Equal(2, core.Footprint.Length);
    }

    [Fact]
    public void ManifestFile_DeserializesAsContentManifest_PreservesAuthorEngineVersionParent()
    {
        var mapGen = new ManifestFile
        {
            Id = "oravey2.test",
            Name = "Test Pack",
            Version = "1.0.0",
            Description = "A test pack",
            Author = "TestAuthor",
            EngineVersion = ">=2.0.0",
            Parent = "oravey2.base",
        };

        var json = JsonSerializer.Serialize(mapGen, Options);
        var core = JsonSerializer.Deserialize<ContentManifest>(json, Options);

        Assert.NotNull(core);
        Assert.Equal("oravey2.test", core.Id);
        Assert.Equal("TestAuthor", core.Author);
        Assert.Equal(">=2.0.0", core.EngineVersion);
        Assert.Equal("oravey2.base", core.Parent);
    }

    [Fact]
    public void BuildingFile_EmptyFootprint_DeserializesAsEmptyArray()
    {
        var mapGen = new BuildingFile
        {
            Id = "b2",
            Name = "Ruin",
            MeshAsset = "meshes/ruin.glb",
            Size = "small",
            Footprint = [],
            Floors = 1,
            Condition = 0.1f,
        };

        var json = JsonSerializer.Serialize(mapGen, Options);
        var core = JsonSerializer.Deserialize<BuildingJson>(json, Options);

        Assert.NotNull(core);
        Assert.NotNull(core.Footprint);
        Assert.Empty(core.Footprint);
    }
}
