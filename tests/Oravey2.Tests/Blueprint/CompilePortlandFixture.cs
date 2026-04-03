using Oravey2.Core.World.Blueprint;

namespace Oravey2.Tests.Blueprint;

/// <summary>
/// Not a real test — compiles portland blueprint to a known output directory.
/// Run with: dotnet test --filter "CompilePortlandMap"
/// </summary>
public class CompilePortlandFixture
{
    [Fact]
    public void CompilePortlandMap()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blueprints", "sample_portland.json");
        var outputDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Oravey2.Windows", "Maps", "portland");

        // Ensure clean output
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, true);

        var blueprint = BlueprintLoader.Load(fixturePath);
        var result = MapCompiler.Compile(blueprint, outputDir);

        Assert.True(result.Success, "Blueprint compilation failed");
        Assert.True(Directory.Exists(outputDir), "Output directory was not created");
    }
}
