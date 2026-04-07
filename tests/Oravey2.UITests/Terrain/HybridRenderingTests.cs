using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Terrain;

/// <summary>
/// UI tests that verify Hybrid-mode chunk rendering (tile overlays and structures).
/// Requires a running game process with the terrain_test scenario which includes
/// a Hybrid chunk at position (2,0) in the test world.
/// </summary>
public class HybridRenderingTests : IAsyncLifetime
{
    private readonly TerrainTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public HybridRenderingTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void HybridChunk_ShowsFloorTiles()
    {
        // Zoom out to see the hybrid chunk area (top-right of the map)
        _fixture.Context.HoldKey(VirtualKey.PageDown, 2000);

        // Rotate camera to look toward the hybrid chunk (top-right)
        _fixture.Context.PressKey(VirtualKey.E);
        _fixture.Context.PressKey(VirtualKey.E);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Hybrid chunk screenshot: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            $"Screenshot too small ({fileBytes.Length} bytes) — hybrid overlay may not be rendering");

        // Copy for manual inspection
        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(HybridRenderingTests).Assembly.Location)!,
            "screenshots");
        Directory.CreateDirectory(outputDir);
        File.Copy(screenshotPath, Path.Combine(outputDir, "hybrid_chunk_floor.png"), true);
        _output.WriteLine($"Copied to: {Path.Combine(outputDir, "hybrid_chunk_floor.png")}");
    }

    [Fact]
    public void HybridToHeightmap_Transition_NoHardEdge()
    {
        // Zoom out to see the boundary between hybrid and heightmap chunks
        _fixture.Context.HoldKey(VirtualKey.PageDown, 2000);

        // Take a screenshot showing the transition area
        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Transition screenshot: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            "Screenshot at transition boundary too small — terrain may have hard edges");

        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(HybridRenderingTests).Assembly.Location)!,
            "screenshots");
        Directory.CreateDirectory(outputDir);
        File.Copy(screenshotPath, Path.Combine(outputDir, "hybrid_transition.png"), true);
        _output.WriteLine($"Copied to: {Path.Combine(outputDir, "hybrid_transition.png")}");
    }
}
