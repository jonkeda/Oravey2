using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// Tests that verify Q/E rotation produces actual visual changes —
/// landmarks move on screen and screenshots differ.
/// Rotation is now continuous (120°/s) so we use HoldKey with duration.
/// </summary>
public class CameraRotationTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public CameraRotationTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    /// <summary>
    /// Wall corner at tile (0,0) = world (-7.5, 0, -7.5).
    /// </summary>
    private const double WallCornerX = -7.5;
    private const double WallCornerY = 0;
    private const double WallCornerZ = -7.5;

    [Fact]
    public void QHold_WorldRotatesOnScreen()
    {
        var before = GameQueryHelpers.WorldToScreen(
            _fixture.Context, WallCornerX, WallCornerY, WallCornerZ);
        _output.WriteLine($"Before Q: screenX={before.ScreenX:F1}, screenY={before.ScreenY:F1}, onScreen={before.OnScreen}");

        // Hold Q for 500ms → ~60° rotation at 120°/s
        _fixture.Context.HoldKey(VirtualKey.Q, 500);

        var after = GameQueryHelpers.WorldToScreen(
            _fixture.Context, WallCornerX, WallCornerY, WallCornerZ);
        _output.WriteLine($"After Q: screenX={after.ScreenX:F1}, screenY={after.ScreenY:F1}, onScreen={after.OnScreen}");

        var dx = Math.Abs(after.ScreenX - before.ScreenX);
        var dy = Math.Abs(after.ScreenY - before.ScreenY);
        var screenDist = Math.Sqrt(dx * dx + dy * dy);
        _output.WriteLine($"Screen movement: dx={dx:F1}, dy={dy:F1}, dist={screenDist:F1}");

        var threshold = before.ScreenWidth * 0.10;
        var moved = screenDist > threshold || before.OnScreen != after.OnScreen;
        Assert.True(moved,
            $"Wall corner should move significantly after Q hold. Moved {screenDist:F1}px (threshold: {threshold:F1}px)");
    }

    [Fact]
    public void EHold_WorldRotatesOpposite()
    {
        double lx = 5.0, ly = 0.0, lz = -3.0;

        var initial = GameQueryHelpers.WorldToScreen(_fixture.Context, lx, ly, lz);
        _output.WriteLine($"Initial: screenX={initial.ScreenX:F1}, screenY={initial.ScreenY:F1}");

        // Rotate with Q for 500ms
        _fixture.Context.HoldKey(VirtualKey.Q, 500);
        var afterQ = GameQueryHelpers.WorldToScreen(_fixture.Context, lx, ly, lz);
        _output.WriteLine($"After Q: screenX={afterQ.ScreenX:F1}, screenY={afterQ.ScreenY:F1}");

        // Rotate back with E for same duration
        _fixture.Context.HoldKey(VirtualKey.E, 500);
        var afterQE = GameQueryHelpers.WorldToScreen(_fixture.Context, lx, ly, lz);
        _output.WriteLine($"After Q+E: screenX={afterQE.ScreenX:F1}, screenY={afterQE.ScreenY:F1}");

        // Should return close to original position (not exact due to frame timing)
        var dx = Math.Abs(afterQE.ScreenX - initial.ScreenX);
        var dy = Math.Abs(afterQE.ScreenY - initial.ScreenY);
        _output.WriteLine($"Return error: dx={dx:F1}, dy={dy:F1}");

        // With continuous rotation, frame timing variance means ~10-15% drift is normal
        var screenWidth = initial.ScreenWidth > 0 ? initial.ScreenWidth : 1280;
        var maxDrift = screenWidth * 0.25; // 25% tolerance
        Assert.True(dx < maxDrift && dy < maxDrift,
            $"Q+E should return landmark near original position. Drift: ({dx:F1}, {dy:F1}), max: {maxDrift:F0}");
    }

    [Fact]
    public void FullRotation_Returns360()
    {
        var initialCam = GameQueryHelpers.GetCameraState(_fixture.Context);
        var shotBefore = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        var beforeBytes = File.ReadAllBytes(shotBefore);
        _output.WriteLine($"Initial yaw: {initialCam.Yaw:F1}");

        // Hold Q for 3000ms → roughly 360° at 120°/s
        _fixture.Context.HoldKey(VirtualKey.Q, 3000);

        var finalCam = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Final yaw: {finalCam.Yaw:F1}");

        // Yaw should be near the initial value (within tolerance for frame timing)
        var diff = Math.Abs(finalCam.Yaw - initialCam.Yaw);
        if (diff > 180) diff = 360 - diff;
        _output.WriteLine($"Yaw diff from start: {diff:F1}");

        Assert.True(diff < 30,
            $"3s of Q hold at 120°/s should return near initial yaw. Got diff={diff:F1}°");
    }

    [Fact]
    public void RotationChangesView_Visually()
    {
        var shotBefore = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        var beforeBytes = File.ReadAllBytes(shotBefore);
        Assert.True(beforeBytes.Length > 1000, "Screenshot before rotation too small");

        // Hold Q for 500ms → ~60° rotation
        _fixture.Context.HoldKey(VirtualKey.Q, 500);

        var shotAfter = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        var afterBytes = File.ReadAllBytes(shotAfter);
        Assert.True(afterBytes.Length > 1000, "Screenshot after rotation too small");

        _output.WriteLine($"Before: {beforeBytes.Length} bytes, After: {afterBytes.Length} bytes");

        Assert.False(beforeBytes.SequenceEqual(afterBytes),
            "Screenshots before and after Q rotation should differ");
    }

}
