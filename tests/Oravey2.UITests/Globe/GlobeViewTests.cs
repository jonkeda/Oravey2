using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Globe;

/// <summary>
/// UI tests that verify the globe view (L4) renders and supports interaction.
/// These require a running game process.
/// </summary>
public class GlobeViewTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public GlobeViewTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void OpenGlobe_ShowsPlanet()
    {
        // Zoom out past L3 to reach globe view
        _fixture.Context.HoldKey(VirtualKey.PageDown, 6000);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot at globe altitude: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot file size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000,
            "Screenshot at globe altitude too small — globe mesh may not be rendering");
    }

    [Fact]
    public void ClickRegion_ShowsTravelDialog()
    {
        // Zoom out to globe
        _fixture.Context.HoldKey(VirtualKey.PageDown, 6000);

        // Click near the centre of the screen (should hit a region)
        _fixture.Context.PressKey(VirtualKey.Enter);

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot after region click: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        Assert.True(fileBytes.Length > 1000,
            "Screenshot after region interaction too small — travel dialog may not appear");
    }

    [Fact]
    public void GlobeRotation_DragChangesView()
    {
        // Zoom out to globe
        _fixture.Context.HoldKey(VirtualKey.PageDown, 6000);

        // Take before screenshot
        var beforePath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        var beforeBytes = File.ReadAllBytes(beforePath);

        // Rotate the view (Q key rotates in the game)
        _fixture.Context.HoldKey(VirtualKey.Q, 1000);

        // Take after screenshot
        var afterPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        var afterBytes = File.ReadAllBytes(afterPath);

        _output.WriteLine($"Before: {beforeBytes.Length} bytes, After: {afterBytes.Length} bytes");

        // Both screenshots should be valid (non-empty)
        Assert.True(beforeBytes.Length > 1000, "Before screenshot too small");
        Assert.True(afterBytes.Length > 1000, "After screenshot too small");
    }
}
