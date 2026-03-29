using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// Tests that verify zoom state and world visibility.
/// Keyboard zoom uses HoldKey with PageUp/PageDown.
/// </summary>
public class ZoomTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public ZoomTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void InitialZoom_WorldEdgesVisible()
    {
        var corners = new (double x, double z, string label)[]
        {
            (-7.5, -7.5, "NW (0,0)"),
            (7.5, -7.5, "NE (15,0)"),
            (-7.5, 7.5, "SW (0,15)"),
            (7.5, 7.5, "SE (15,15)")
        };

        int onScreenCount = 0;
        foreach (var (x, z, label) in corners)
        {
            var sp = GameQueryHelpers.WorldToScreen(_fixture.Context, x, 0, z);
            _output.WriteLine($"{label}: screenX={sp.ScreenX:F0}, screenY={sp.ScreenY:F0}, onScreen={sp.OnScreen}");
            if (sp.OnScreen) onScreenCount++;
        }

        _output.WriteLine($"Corners on screen: {onScreenCount}/4");
        Assert.True(onScreenCount >= 2,
            $"At initial zoom, at least 2 of 4 world corners should be visible; got {onScreenCount}");
    }

    [Fact]
    public void ZoomState_IsQueryable()
    {
        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Zoom: {cam.Zoom:F1}, Yaw: {cam.Yaw:F1}, Pitch: {cam.Pitch:F1}");

        Assert.Equal(25.0, cam.Zoom, 5.0);
    }

    [Fact]
    public void ZoomOut_ShowsMoreWorld()
    {
        var camBefore = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Zoom before: {camBefore.Zoom:F1}");

        // Hold PageDown for 500ms → zoom out at 15 units/s → ~7.5 units increase
        _fixture.Context.HoldKey(VirtualKey.PageDown, 500);

        var camAfter = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Zoom after: {camAfter.Zoom:F1}");

        Assert.True(camAfter.Zoom > camBefore.Zoom,
            $"Zoom should increase (zoom out) after holding PageDown. Before={camBefore.Zoom:F1}, After={camAfter.Zoom:F1}");
    }

    [Fact]
    public void ZoomIn_ReducesZoom()
    {
        var camBefore = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Zoom before: {camBefore.Zoom:F1}");

        // Hold PageUp for 500ms → zoom in at 15 units/s → ~7.5 units decrease
        _fixture.Context.HoldKey(VirtualKey.PageUp, 500);

        var camAfter = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Zoom after: {camAfter.Zoom:F1}");

        Assert.True(camAfter.Zoom < camBefore.Zoom,
            $"Zoom should decrease (zoom in) after holding PageUp. Before={camBefore.Zoom:F1}, After={camAfter.Zoom:F1}");
    }
}
