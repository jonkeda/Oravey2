using Brinell.Stride.Context;
using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Terrain;

public class TreeRenderingTests : IAsyncLifetime
{
    private readonly TerrainTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public TreeRenderingTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void ForestedArea_ShowsTrees()
    {
        // Wait for terrain to build
        _fixture.Context.HoldKey(VirtualKey.Space, 500);

        // Teleport near the forested area (top-left of map, chunk 0,0)
        GameQueryHelpers.TeleportPlayer(_fixture.Context, -40, 2, -40);
        _fixture.Context.HoldKey(VirtualKey.Space, 500);

        var path = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot (forested area): {path}");
        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        _output.WriteLine($"Size: {bytes.Length} bytes");
        Assert.True(bytes.Length > 1000, "Screenshot too small — trees may not be rendering");

        // Copy for inspection
        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(TreeRenderingTests).Assembly.Location)!,
            "screenshots");
        Directory.CreateDirectory(outputDir);
        File.Copy(path, Path.Combine(outputDir, "forested_area.png"), true);
        _output.WriteLine($"Copied to: {Path.Combine(outputDir, "forested_area.png")}");
    }

    [Fact]
    public void DistantTrees_AreBillboards()
    {
        // Wait for terrain to build
        _fixture.Context.HoldKey(VirtualKey.Space, 500);

        // Move camera far from the forest by zooming out significantly
        _fixture.Context.HoldKey(VirtualKey.PageDown, 3000);

        var path = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot (distant trees): {path}");
        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        _output.WriteLine($"Size: {bytes.Length} bytes");
        Assert.True(bytes.Length > 1000,
            "Screenshot too small — distant billboard trees may not be rendering");

        // Copy for inspection
        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(TreeRenderingTests).Assembly.Location)!,
            "screenshots");
        Directory.CreateDirectory(outputDir);
        File.Copy(path, Path.Combine(outputDir, "distant_trees_billboards.png"), true);
        _output.WriteLine($"Copied to: {Path.Combine(outputDir, "distant_trees_billboards.png")}");
    }
}
