using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.Serialization;

/// <summary>
/// Generates the JSON fixture files for town and wasteland maps.
/// Run with: dotnet test --filter "FullyQualifiedName~FixtureGenerator" 
/// Then commit the generated files.
/// </summary>
public class FixtureGenerator
{
    private static string GetFixturesDir()
    {
        // Navigate from test bin to the source Fixtures directory
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.Name != "Oravey2.Tests")
            dir = dir.Parent;

        // If running from bin, go to project root
        if (dir == null)
        {
            // Try to find via known structure: bin/Debug/net10.0 → project root
            var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
            dir = baseDir.Parent?.Parent?.Parent;
        }

        return Path.Combine(dir!.FullName, "Fixtures", "Maps");
    }

    [Fact]
    public void GenerateTownFixture()
    {
        var fixturesDir = GetFixturesDir();
        var outputDir = Path.Combine(fixturesDir, "test_town");

        var townMap = TownMapBuilder.CreateTownMap();
        var world = new WorldMapData(1, 1);
        world.SetChunk(0, 0, new ChunkData(0, 0, townMap));

        MapExporter.ExportWorld(world, outputDir,
            playerStart: new PlayerStartJson(0, 0, 12, 17));

        Assert.True(File.Exists(Path.Combine(outputDir, "world.json")));
        Assert.True(File.Exists(Path.Combine(outputDir, "chunks", "0_0.json")));
    }

    [Fact]
    public void GenerateWastelandFixture()
    {
        var fixturesDir = GetFixturesDir();
        var outputDir = Path.Combine(fixturesDir, "test_wasteland");

        var wastelandMap = WastelandMapBuilder.CreateWastelandMap();
        var world = new WorldMapData(1, 1);
        world.SetChunk(0, 0, new ChunkData(0, 0, wastelandMap));

        MapExporter.ExportWorld(world, outputDir,
            playerStart: new PlayerStartJson(0, 0, 8, 17));

        Assert.True(File.Exists(Path.Combine(outputDir, "world.json")));
        Assert.True(File.Exists(Path.Combine(outputDir, "chunks", "0_0.json")));
    }
}
