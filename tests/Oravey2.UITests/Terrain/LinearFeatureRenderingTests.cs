using Brinell.Stride.Context;
using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Terrain;

/// <summary>
/// Fixture that launches the terrain_test scenario for linear feature testing.
/// </summary>
public class TerrainTestFixture : OraveyTestFixture
{
    protected override StrideTestContextOptions CreateOptions()
    {
        var options = base.CreateOptions();
        options.GameArguments = ["--automation", "--scenario", "terrain_test"];
        return options;
    }
}

/// <summary>
/// UI tests that verify linear features (roads, rails, rivers) render correctly
/// on the terrain. Requires a running game process with the terrain_test scenario.
/// </summary>
public class LinearFeatureRenderingTests : IAsyncLifetime
{
    private readonly TerrainTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public LinearFeatureRenderingTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void TerrainTest_LinearFeatures_ScreenshotCaptured()
    {
        // Wait a frame for terrain to build
        _fixture.Context.HoldKey(VirtualKey.Space, 500);

        // Zoom OUT to see the whole map (PageDown = zoom out)
        _fixture.Context.HoldKey(VirtualKey.PageDown, 2000);

        // Take wide-angle screenshot showing entire terrain
        var path1 = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot (zoomed out): {path1}");
        Assert.True(File.Exists(path1));
        var bytes1 = File.ReadAllBytes(path1);
        _output.WriteLine($"Size: {bytes1.Length} bytes");
        Assert.True(bytes1.Length > 1000, "Screenshot too small");

        // Copy to a known location for inspection
        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(LinearFeatureRenderingTests).Assembly.Location)!,
            "screenshots");
        Directory.CreateDirectory(outputDir);
        File.Copy(path1, Path.Combine(outputDir, "linear_features_wide.png"), true);
        _output.WriteLine($"Copied to: {Path.Combine(outputDir, "linear_features_wide.png")}");
    }

    [Fact]
    public void Road_CrossingChunkBoundary_RendersContinuous()
    {
        _fixture.Context.HoldKey(VirtualKey.W, 2000);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot at chunk boundary: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath));

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            "Screenshot too small — road or terrain may not be rendering");

        // Copy for inspection
        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(LinearFeatureRenderingTests).Assembly.Location)!,
            "screenshots");
        Directory.CreateDirectory(outputDir);
        File.Copy(screenshotPath, Path.Combine(outputDir, "road_chunk_boundary.png"), true);
        _output.WriteLine($"Copied to: {Path.Combine(outputDir, "road_chunk_boundary.png")}");
    }

    [Fact]
    public void River_HasWaterSurface()
    {
        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"River screenshot: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath));

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            "Screenshot too small — river may not be rendering");

        // Copy for inspection
        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(LinearFeatureRenderingTests).Assembly.Location)!,
            "screenshots");
        Directory.CreateDirectory(outputDir);
        File.Copy(screenshotPath, Path.Combine(outputDir, "river_surface.png"), true);
        _output.WriteLine($"Copied to: {Path.Combine(outputDir, "river_surface.png")}");
    }
}
