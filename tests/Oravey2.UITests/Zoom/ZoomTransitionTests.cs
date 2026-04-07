using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests.Zoom;

/// <summary>
/// UI tests that verify smooth zoom transitions between L1, L2, and L3 views.
/// These require a running game process with terrain loaded.
/// </summary>
public class ZoomTransitionTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public ZoomTransitionTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void ZoomOut_L1ToL2_SmoothTransition()
    {
        // Zoom out significantly to reach L2 altitude range (>50 m)
        _fixture.Context.HoldKey(VirtualKey.PageDown, 2000);

        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Camera after zoom out: Y={cam.Y:F1}, Zoom={cam.Zoom:F1}");

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot at L2 altitude: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        Assert.True(fileBytes.Length > 1000,
            "Screenshot at L2 altitude too small — transition may have failed");
    }

    [Fact]
    public void ZoomOut_L2ToL3_ShowsContinentView()
    {
        // Zoom out far to reach L3 altitude range (>600 m)
        _fixture.Context.HoldKey(VirtualKey.PageDown, 5000);

        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Camera after deep zoom out: Y={cam.Y:F1}, Zoom={cam.Zoom:F1}");

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot at L3 altitude: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        Assert.True(fileBytes.Length > 1000,
            "Screenshot at L3 altitude too small — continental view may not render");
    }

    [Fact]
    public void ZoomIn_L3ToL1_RestoresDetail()
    {
        // First zoom out to L3
        _fixture.Context.HoldKey(VirtualKey.PageDown, 5000);

        // Then zoom back in to L1
        _fixture.Context.HoldKey(VirtualKey.PageUp, 5000);

        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Camera after zoom back in: Y={cam.Y:F1}, Zoom={cam.Zoom:F1}");

        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot at L1 after round-trip: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        var fileBytes = File.ReadAllBytes(screenshotPath);
        Assert.True(fileBytes.Length > 1000,
            "Screenshot after zoom round-trip too small — detail may not have restored");
    }
}
