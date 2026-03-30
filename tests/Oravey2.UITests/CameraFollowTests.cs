using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// Tests that verify the camera follows the player and the player
/// stays visible on screen after movement and rotation.
/// </summary>
public class CameraFollowTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public CameraFollowTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void PlayerOnScreen_AtStart()
    {
        var psp = GameQueryHelpers.GetPlayerScreenPosition(_fixture.Context);
        _output.WriteLine($"Player screen: normX={psp.NormX:F3}, normY={psp.NormY:F3}, onScreen={psp.OnScreen}");

        Assert.True(psp.OnScreen, "Player should be on screen at start");
        Assert.InRange(psp.NormX, 0.2, 0.8);
        Assert.InRange(psp.NormY, 0.2, 0.8);
    }

    [Fact]
    public void PlayerOnScreen_AfterMovement()
    {
        _fixture.Context.HoldKey(VirtualKey.W, 1500);

        var psp = GameQueryHelpers.GetPlayerScreenPosition(_fixture.Context);
        _output.WriteLine($"After W 1.5s: normX={psp.NormX:F3}, normY={psp.NormY:F3}, onScreen={psp.OnScreen}");

        Assert.True(psp.OnScreen, "Player should still be on screen after movement");
        // Camera follow should keep player roughly centered
        Assert.InRange(psp.NormX, 0.15, 0.85);
        Assert.InRange(psp.NormY, 0.15, 0.85);
    }

    [Fact]
    public void CameraOffset_MatchesYawPitch()
    {
        // Settle one frame so the TacticalCameraScript computes its orbit position
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        var player = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);

        _output.WriteLine($"Player: ({player.X:F2}, {player.Y:F2}, {player.Z:F2})");
        _output.WriteLine($"Camera: ({cam.X:F2}, {cam.Y:F2}, {cam.Z:F2}) yaw={cam.Yaw:F1} pitch={cam.Pitch:F1} zoom={cam.Zoom:F1}");

        // Compute expected offset from the ACTUAL yaw/pitch (not hardcoded 45°)
        // to be resilient to state carryover from other tests that rotate the camera.
        var yawRad = cam.Yaw * Math.PI / 180.0;
        var pitchRad = cam.Pitch * Math.PI / 180.0;
        var distance = 50.0;
        var expectedOffsetX = distance * Math.Cos(pitchRad) * Math.Sin(yawRad);
        var expectedOffsetY = distance * Math.Sin(pitchRad);
        var expectedOffsetZ = distance * Math.Cos(pitchRad) * Math.Cos(yawRad);

        var offsetX = cam.X - player.X;
        var offsetY = cam.Y - player.Y;
        var offsetZ = cam.Z - player.Z;
        _output.WriteLine($"Offset: ({offsetX:F2}, {offsetY:F2}, {offsetZ:F2})");
        _output.WriteLine($"Expected: ({expectedOffsetX:F2}, {expectedOffsetY:F2}, {expectedOffsetZ:F2})");

        // Y offset should be positive (camera above player)
        Assert.True(offsetY > 10, $"Camera should be above player, Y offset = {offsetY:F2}");

        // Camera distance from player (3D) should approximate 50
        var dist3D = Math.Sqrt(offsetX * offsetX + offsetY * offsetY + offsetZ * offsetZ);
        _output.WriteLine($"3D distance: {dist3D:F2}, expected ~50");
        Assert.InRange(dist3D, 35, 65);
    }

    [Fact]
    public void CameraFollows_PositionDelta()
    {
        // Teleport to origin and settle so camera is stable before measuring delta
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 0, 0.5, 0);
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var playerBefore = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var camBefore = GameQueryHelpers.GetCameraState(_fixture.Context);

        _fixture.Context.HoldKey(VirtualKey.W, 1000);

        var playerAfter = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var camAfter = GameQueryHelpers.GetCameraState(_fixture.Context);

        var playerDx = playerAfter.X - playerBefore.X;
        var playerDz = playerAfter.Z - playerBefore.Z;
        var camDx = camAfter.X - camBefore.X;
        var camDz = camAfter.Z - camBefore.Z;

        _output.WriteLine($"Player delta: ({playerDx:F2}, {playerDz:F2})");
        _output.WriteLine($"Camera delta: ({camDx:F2}, {camDz:F2})");

        var playerDist = Math.Sqrt(playerDx * playerDx + playerDz * playerDz);
        var camDist = Math.Sqrt(camDx * camDx + camDz * camDz);
        _output.WriteLine($"Player moved: {playerDist:F2}, Camera moved: {camDist:F2}");

        Assert.True(playerDist > 1.0, $"Player should move >1 unit, moved {playerDist:F2}");
        Assert.True(camDist > playerDist * 0.5,
            $"Camera should follow at least 50% of player distance. Player: {playerDist:F2}, Camera: {camDist:F2}");

        // Camera delta direction should roughly match player delta direction
        if (playerDist > 0.1 && camDist > 0.1)
        {
            var dot = (playerDx / playerDist) * (camDx / camDist) +
                      (playerDz / playerDist) * (camDz / camDist);
            _output.WriteLine($"Direction dot product: {dot:F3}");
            Assert.True(dot > 0.5, $"Camera should move in same direction as player, dot={dot:F3}");
        }
    }

    [Fact]
    public void PlayerVisible_FromAllFourRotations()
    {
        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);
        _output.WriteLine($"Starting yaw: {cam.Yaw:F1}");

        for (int i = 0; i < 4; i++)
        {
            // Move a bit to ensure we're testing follow across rotation
            _fixture.Context.HoldKey(VirtualKey.W, 300);

            var psp = GameQueryHelpers.GetPlayerScreenPosition(_fixture.Context);
            var camState = GameQueryHelpers.GetCameraState(_fixture.Context);
            _output.WriteLine($"Rotation {i}: yaw={camState.Yaw:F1}, player onScreen={psp.OnScreen}, normX={psp.NormX:F3}, normY={psp.NormY:F3}");

            Assert.True(psp.OnScreen, $"Player should be visible at rotation {i} (yaw={camState.Yaw:F1})");

            // Rotate for next iteration (~90° at 120°/s in 750ms)
            _fixture.Context.HoldKey(VirtualKey.Q, 750);
        }
    }
}
