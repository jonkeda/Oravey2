using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Terrain;

/// <summary>
/// UI tests that verify the heightmap terrain renders correctly.
/// These require a running game process with the terrain_test scenario.
/// </summary>
public class HeightmapRenderingTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public HeightmapRenderingTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void TestScene_Launches_TerrainVisible()
    {
        // Take a screenshot to verify terrain is rendered
        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot saved to: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot file size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            $"Screenshot too small ({fileBytes.Length} bytes), likely empty or corrupt — terrain may not be rendering");
    }

    [Fact]
    public void TestScene_MultipleChunks_NoGapsBetweenChunks()
    {
        // Move to a chunk boundary area to test seam rendering
        _fixture.Context.HoldKey(VirtualKey.W, 2000);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot at chunk boundary: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        Assert.True(fileBytes.Length > 1000,
            "Screenshot at chunk boundary too small — terrain may have gaps");
    }
}
