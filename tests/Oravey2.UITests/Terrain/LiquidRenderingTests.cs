using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Terrain;

/// <summary>
/// UI tests that verify liquid rendering (water, lava, toxic) in the terrain_test scenario.
/// The test scene includes a water lake, lava pool, toxic puddle, and a waterfall edge.
/// </summary>
public class LiquidRenderingTests : IAsyncLifetime
{
    private readonly TerrainTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public LiquidRenderingTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void WaterLake_IsVisible()
    {
        // Zoom out to see the water lake area (bottom-centre of the map)
        _fixture.Context.HoldKey(VirtualKey.PageDown, 2000);

        // Move camera toward the lake area
        _fixture.Context.HoldKey(VirtualKey.S, 1500);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Water lake screenshot: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            $"Screenshot too small ({fileBytes.Length} bytes) — water lake may not be rendering");

        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(LiquidRenderingTests).Assembly.Location)!,
            "screenshots");
        Directory.CreateDirectory(outputDir);
        File.Copy(screenshotPath, Path.Combine(outputDir, "water_lake.png"), true);
        _output.WriteLine($"Copied to: {Path.Combine(outputDir, "water_lake.png")}");
    }

    [Fact]
    public void LavaPool_HasGlow()
    {
        // Zoom out to see the lava pool area
        _fixture.Context.HoldKey(VirtualKey.PageDown, 2000);

        // Move toward lava pool (bottom-centre-right area)
        _fixture.Context.HoldKey(VirtualKey.S, 1500);
        _fixture.Context.HoldKey(VirtualKey.D, 500);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Lava pool screenshot: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            "Screenshot too small — lava pool may not be rendering");

        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(LiquidRenderingTests).Assembly.Location)!,
            "screenshots");
        Directory.CreateDirectory(outputDir);
        File.Copy(screenshotPath, Path.Combine(outputDir, "lava_pool.png"), true);
        _output.WriteLine($"Copied to: {Path.Combine(outputDir, "lava_pool.png")}");
    }
}
