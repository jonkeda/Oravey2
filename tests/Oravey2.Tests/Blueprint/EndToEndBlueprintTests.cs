using Oravey2.Core.World;
using Oravey2.Core.World.Blueprint;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.Blueprint;

public class EndToEndBlueprintTests : IDisposable
{
    private readonly string _outputDir;

    public EndToEndBlueprintTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "E2EBlueprint_" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blueprints", name);

    [Fact]
    public void LoadFixture_ValidatesSuccessfully()
    {
        var blueprint = BlueprintLoader.Load(FixturePath("sample_portland.json"));
        var result = BlueprintValidator.Validate(blueprint);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void LoadFixture_CompilesToDisk()
    {
        var blueprint = BlueprintLoader.Load(FixturePath("sample_portland.json"));
        var result = MapCompiler.Compile(blueprint, _outputDir);

        Assert.True(result.Success);
        Assert.Equal(4, result.ChunksGenerated);
        Assert.Equal(1, result.BuildingsPlaced);
        Assert.Equal(2, result.PropsPlaced);
    }

    [Fact]
    public void CompiledOutput_LoadableByMapLoader()
    {
        var blueprint = BlueprintLoader.Load(FixturePath("sample_portland.json"));
        MapCompiler.Compile(blueprint, _outputDir);

        var world = MapLoader.LoadWorldJson(_outputDir);
        Assert.Equal(2, world.ChunksWide);
        Assert.Equal(2, world.ChunksHigh);
    }

    [Fact]
    public void CompiledChunks_HaveCorrectBaseElevation()
    {
        var blueprint = BlueprintLoader.Load(FixturePath("sample_portland.json"));
        MapCompiler.Compile(blueprint, _outputDir);

        var chunk = ChunkSerializer.LoadChunk(Path.Combine(_outputDir, "chunks"), 0, 0);
        // Most tiles at baseElevation=3 (those not near road/river)
        var td = chunk.Tiles.GetTileData(15, 0);
        Assert.True(td.HeightLevel >= 1, "Height should be at least 1 (smoothed or base)");
    }

    [Fact]
    public void CompiledChunks_RiverHasWater()
    {
        var blueprint = BlueprintLoader.Load(FixturePath("sample_portland.json"));
        MapCompiler.Compile(blueprint, _outputDir);

        var chunk = ChunkSerializer.LoadChunk(Path.Combine(_outputDir, "chunks"), 0, 0);
        // River runs at x=5. Check center tile for water.
        var td = chunk.Tiles.GetTileData(5, 8);
        Assert.True(td.WaterLevel > 0, "River tile should have water");
    }

    [Fact]
    public void CompiledChunks_RoadHasSurfaceType()
    {
        var blueprint = BlueprintLoader.Load(FixturePath("sample_portland.json"));
        MapCompiler.Compile(blueprint, _outputDir);

        var chunk = ChunkSerializer.LoadChunk(Path.Combine(_outputDir, "chunks"), 0, 0);
        // Road "burnside" at y=15, x=0..31
        var td = chunk.Tiles.GetTileData(0, 15);
        Assert.Equal(SurfaceType.Asphalt, td.Surface);
    }

    [Fact]
    public void FullRoundTrip_BlueprintToLoadedWorld()
    {
        // Load fixture → compile → reload → verify structure
        var blueprint = BlueprintLoader.Load(FixturePath("sample_portland.json"));
        var compileResult = MapCompiler.Compile(blueprint, _outputDir);

        Assert.True(compileResult.Success);

        // Verify all files exist
        Assert.True(File.Exists(Path.Combine(_outputDir, "world.json")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "buildings.json")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "props.json")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "zones.json")));

        // Load and verify world
        var world = MapLoader.LoadWorld(_outputDir);
        Assert.Equal(2, world.ChunksWide);
        Assert.Equal(2, world.ChunksHigh);

        // Load each chunk
        var chunksDir = Path.Combine(_outputDir, "chunks");
        for (int cx = 0; cx < 2; cx++)
        {
            for (int cy = 0; cy < 2; cy++)
            {
                var chunk = ChunkSerializer.LoadChunk(chunksDir, cx, cy);
                Assert.Equal(cx, chunk.ChunkX);
                Assert.Equal(cy, chunk.ChunkY);
                Assert.Equal(ChunkData.Size, chunk.Tiles.Width);
                Assert.Equal(ChunkData.Size, chunk.Tiles.Height);
            }
        }
    }
}
