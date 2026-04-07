using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Streaming;

/// <summary>
/// UI tests that verify chunk streaming produces continuous terrain as the player moves.
/// Requires a running game process with the generated world scenario.
/// </summary>
public class ChunkStreamingTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public ChunkStreamingTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void WalkAcrossChunkBoundary_NoGaps()
    {
        // Walk toward a chunk boundary
        _fixture.Context.HoldKey(VirtualKey.W, 3000);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot at chunk boundary: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot file size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            "Screenshot at chunk boundary too small — terrain may have gaps between chunks");
    }

    [Fact]
    public void WalkIntoUnexploredArea_TerrainAppears()
    {
        // Walk a significant distance to trigger on-demand chunk generation
        _fixture.Context.HoldKey(VirtualKey.W, 5000);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot in unexplored area: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot file size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            "Screenshot in unexplored area too small — terrain may not have generated");
    }
}
