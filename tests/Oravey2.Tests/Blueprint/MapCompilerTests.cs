using System.Text.Json;
using Oravey2.Core.World;
using Oravey2.Core.World.Blueprint;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.Blueprint;

public class MapCompilerTests : IDisposable
{
    private readonly string _outputDir;

    public MapCompilerTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "MapCompilerTests_" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
    }

    [Fact]
    public void Compile_MinimalBlueprint_Succeeds()
    {
        var result = MapCompiler.Compile(TestBlueprints.Minimal(), _outputDir);

        Assert.True(result.Success);
        Assert.Equal(1, result.ChunksGenerated);
    }

    [Fact]
    public void Compile_MinimalBlueprint_CreatesWorldJson()
    {
        MapCompiler.Compile(TestBlueprints.Minimal(), _outputDir);

        var worldPath = Path.Combine(_outputDir, "world.json");
        Assert.True(File.Exists(worldPath));

        var worldJson = MapLoader.LoadWorldJson(_outputDir);
        Assert.Equal(1, worldJson.ChunksWide);
        Assert.Equal(1, worldJson.ChunksHigh);
    }

    [Fact]
    public void Compile_MinimalBlueprint_CreatesChunkFile()
    {
        MapCompiler.Compile(TestBlueprints.Minimal(), _outputDir);

        var chunkPath = Path.Combine(_outputDir, "chunks", "0_0.json");
        Assert.True(File.Exists(chunkPath));
    }

    [Fact]
    public void Compile_Full2x2_Generates4Chunks()
    {
        var result = MapCompiler.Compile(TestBlueprints.Full2x2(), _outputDir);

        Assert.True(result.Success);
        Assert.Equal(4, result.ChunksGenerated);

        // Verify all chunk files exist
        for (int cx = 0; cx < 2; cx++)
            for (int cy = 0; cy < 2; cy++)
                Assert.True(File.Exists(Path.Combine(_outputDir, "chunks", $"{cx}_{cy}.json")));
    }

    [Fact]
    public void Compile_Full2x2_WritesBuildings()
    {
        var result = MapCompiler.Compile(TestBlueprints.Full2x2(), _outputDir);

        Assert.Equal(1, result.BuildingsPlaced);
        Assert.True(File.Exists(Path.Combine(_outputDir, "buildings.json")));
    }

    [Fact]
    public void Compile_Full2x2_WritesProps()
    {
        var result = MapCompiler.Compile(TestBlueprints.Full2x2(), _outputDir);

        Assert.Equal(2, result.PropsPlaced);
        Assert.True(File.Exists(Path.Combine(_outputDir, "props.json")));
    }

    [Fact]
    public void Compile_Full2x2_WritesZones()
    {
        MapCompiler.Compile(TestBlueprints.Full2x2(), _outputDir);

        Assert.True(File.Exists(Path.Combine(_outputDir, "zones.json")));
    }

    [Fact]
    public void Compile_ChunksLoadableByChunkSerializer()
    {
        MapCompiler.Compile(TestBlueprints.Minimal(), _outputDir);

        var chunksDir = Path.Combine(_outputDir, "chunks");
        var chunk = ChunkSerializer.LoadChunk(chunksDir, 0, 0);

        Assert.Equal(0, chunk.ChunkX);
        Assert.Equal(0, chunk.ChunkY);
        Assert.Equal(ChunkData.Size, chunk.Tiles.Width);
    }

    [Fact]
    public void Compile_TerrainBaseElevation_AppliedToAllTiles()
    {
        MapCompiler.Compile(TestBlueprints.Minimal(), _outputDir);

        var chunk = ChunkSerializer.LoadChunk(Path.Combine(_outputDir, "chunks"), 0, 0);
        // Minimal blueprint has baseElevation = 1
        var td = chunk.Tiles.GetTileData(0, 0);
        Assert.Equal(1, td.HeightLevel);
    }

    [Fact]
    public void Compile_InvalidBlueprint_ReturnsFalse()
    {
        var badBlueprint = new MapBlueprint(
            "Bad", "Bad map",
            new BlueprintSource("Nowhere", null),
            new BlueprintDimensions(0, 0), // invalid dimensions
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null, null, null, null);

        var result = MapCompiler.Compile(badBlueprint, _outputDir);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Compile_NoBuildingsOrProps_NoExtraFiles()
    {
        MapCompiler.Compile(TestBlueprints.Minimal(), _outputDir);

        Assert.False(File.Exists(Path.Combine(_outputDir, "buildings.json")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "props.json")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "zones.json")));
    }

    [Fact]
    public void Compile_WorldJson_IncludesMetadata()
    {
        MapCompiler.Compile(TestBlueprints.Full2x2(), _outputDir);

        var worldJson = MapLoader.LoadWorldJson(_outputDir);
        Assert.Equal("Portland", worldJson.Name);
        Assert.Equal("Small Portland map", worldJson.Description);
        Assert.Equal("Portland, OR", worldJson.Source);
    }

    [Fact]
    public void Compile_Minimal_WorldJson_IncludesMetadata()
    {
        MapCompiler.Compile(TestBlueprints.Minimal(), _outputDir);

        var worldJson = MapLoader.LoadWorldJson(_outputDir);
        Assert.Equal("Test", worldJson.Name);
        Assert.Equal("Test map", worldJson.Description);
        Assert.Equal("Testville", worldJson.Source);
    }
}
