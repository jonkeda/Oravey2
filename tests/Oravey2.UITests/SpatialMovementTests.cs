using Brinell.Stride.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// Tests that verify WASD movement produces correct spatial outcomes.
/// Each test moves the player then verifies world position, tile, and screen position.
/// </summary>
public class SpatialMovementTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public SpatialMovementTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    [Trait("Category", "Smoke")]
    public void MoveW_PlayerMovesToExpectedTile()
    {
        var before = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _output.WriteLine($"Before: ({before.X:F2}, {before.Z:F2})");

        _fixture.Context.HoldKey(VirtualKey.W, 1000);

        var after = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _output.WriteLine($"After: ({after.X:F2}, {after.Z:F2})");

        // At yaw=45°, W moves player in the +X/+Z direction (screen-up)
        var dx = after.X - before.X;
        var dz = after.Z - before.Z;
        var distance = Math.Sqrt(dx * dx + dz * dz);
        _output.WriteLine($"Delta: dx={dx:F2}, dz={dz:F2}, dist={distance:F2}");

        Assert.True(distance > 1.0,
            $"W should move player >1 unit, moved {distance:F2}");

        // Verify player is on a valid tile (not Wall)
        var tile = GameQueryHelpers.GetTileAtWorldPos(_fixture.Context, after.X, after.Z);
        _output.WriteLine($"Tile at player: ({tile.TileX},{tile.TileZ}) type={tile.TileType}");
        Assert.NotEqual("Wall", tile.TileType);
    }

    [Fact]
    public void MoveS_PlayerMovesOpposite()
    {
        var origin = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        _fixture.Context.HoldKey(VirtualKey.W, 500);
        var afterW = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        _fixture.Context.HoldKey(VirtualKey.S, 500);
        var afterS = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        _output.WriteLine($"Origin: ({origin.X:F2},{origin.Z:F2})");
        _output.WriteLine($"After W: ({afterW.X:F2},{afterW.Z:F2})");
        _output.WriteLine($"After S: ({afterS.X:F2},{afterS.Z:F2})");

        // S should move player back toward origin
        var distFromOriginAfterW = Math.Sqrt(
            Math.Pow(afterW.X - origin.X, 2) + Math.Pow(afterW.Z - origin.Z, 2));
        var distFromOriginAfterS = Math.Sqrt(
            Math.Pow(afterS.X - origin.X, 2) + Math.Pow(afterS.Z - origin.Z, 2));

        Assert.True(distFromOriginAfterS < distFromOriginAfterW,
            $"S should reverse W movement. Dist after W: {distFromOriginAfterW:F2}, after S: {distFromOriginAfterS:F2}");

        // Player should still be on a valid tile
        var tile = GameQueryHelpers.GetTileAtWorldPos(_fixture.Context, afterS.X, afterS.Z);
        _output.WriteLine($"Tile after S: ({tile.TileX},{tile.TileZ}) type={tile.TileType}");
        Assert.NotEqual("Wall", tile.TileType);
    }

    [Fact]
    public void MoveA_PlayerMovesLeftOnScreen()
    {
        // Project a fixed world reference point before and after movement.
        // After A, the camera follows the player left, so the fixed point shifts right.
        var refPoint = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var screenBefore = GameQueryHelpers.WorldToScreen(
            _fixture.Context, refPoint.X, refPoint.Y, refPoint.Z);
        _output.WriteLine($"Before screen: X={screenBefore.ScreenX:F1}, Y={screenBefore.ScreenY:F1}");

        _fixture.Context.HoldKey(VirtualKey.A, 500);

        // Project the SAME fixed point after movement — it should shift right
        // (because camera moved left with the player)
        var screenAfter = GameQueryHelpers.WorldToScreen(
            _fixture.Context, refPoint.X, refPoint.Y, refPoint.Z);
        _output.WriteLine($"After screen: X={screenAfter.ScreenX:F1}, Y={screenAfter.ScreenY:F1}");

        // The reference point should have moved right on screen (player went left)
        Assert.True(screenAfter.ScreenX > screenBefore.ScreenX,
            $"A should move player left (reference drifts right). Screen X went from {screenBefore.ScreenX:F1} to {screenAfter.ScreenX:F1}");

        // Player still on valid tile
        var posAfter = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var tile = GameQueryHelpers.GetTileAtWorldPos(_fixture.Context, posAfter.X, posAfter.Z);
        Assert.NotEqual("Wall", tile.TileType);
    }

    [Fact]
    public void MoveD_PlayerMovesRightOnScreen()
    {
        // Project a fixed world reference point before and after movement.
        var refPoint = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var screenBefore = GameQueryHelpers.WorldToScreen(
            _fixture.Context, refPoint.X, refPoint.Y, refPoint.Z);
        _output.WriteLine($"Before screen: X={screenBefore.ScreenX:F1}, Y={screenBefore.ScreenY:F1}");

        _fixture.Context.HoldKey(VirtualKey.D, 500);

        // Project the SAME fixed point after movement — it should shift left
        // (because camera moved right with the player)
        var screenAfter = GameQueryHelpers.WorldToScreen(
            _fixture.Context, refPoint.X, refPoint.Y, refPoint.Z);
        _output.WriteLine($"After screen: X={screenAfter.ScreenX:F1}, Y={screenAfter.ScreenY:F1}");

        // The reference point should have moved left on screen (player went right)
        Assert.True(screenAfter.ScreenX < screenBefore.ScreenX,
            $"D should move player right (reference drifts left). Screen X went from {screenBefore.ScreenX:F1} to {screenAfter.ScreenX:F1}");

        // Player still on valid tile
        var posAfter = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var tile = GameQueryHelpers.GetTileAtWorldPos(_fixture.Context, posAfter.X, posAfter.Z);
        Assert.NotEqual("Wall", tile.TileType);
    }

    [Fact]
    public void MoveW_PlayerStaysOnMap()
    {
        // Use a shorter hold time to stay within the 16x16 map
        // (no wall collisions exist yet, so the player can walk off)
        _fixture.Context.HoldKey(VirtualKey.W, 1000);

        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _output.WriteLine($"Position after 1s W: ({pos.X:F2}, {pos.Z:F2})");

        // Player should still be within the tile map bounds
        var tile = GameQueryHelpers.GetTileAtWorldPos(_fixture.Context, pos.X, pos.Z);
        _output.WriteLine($"Tile: ({tile.TileX},{tile.TileZ}) type={tile.TileType}");

        Assert.InRange(tile.TileX, 0, 15);
        Assert.InRange(tile.TileZ, 0, 15);
        Assert.NotEqual("Empty", tile.TileType);
    }

    [Fact]
    public void WASD_AreOrthogonal()
    {
        // Record W delta
        var beforeW = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.W, 500);
        var afterW = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var wDx = afterW.X - beforeW.X;
        var wDz = afterW.Z - beforeW.Z;

        // Move back
        _fixture.Context.HoldKey(VirtualKey.S, 500);

        // Record A delta
        var beforeA = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.A, 500);
        var afterA = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var aDx = afterA.X - beforeA.X;
        var aDz = afterA.Z - beforeA.Z;

        _output.WriteLine($"W delta: ({wDx:F2}, {wDz:F2})");
        _output.WriteLine($"A delta: ({aDx:F2}, {aDz:F2})");

        // Normalize and compute dot product — should be ≈ 0 for perpendicular
        var wLen = Math.Sqrt(wDx * wDx + wDz * wDz);
        var aLen = Math.Sqrt(aDx * aDx + aDz * aDz);
        Assert.True(wLen > 0.3, $"W didn't move enough: {wLen:F2}");
        Assert.True(aLen > 0.3, $"A didn't move enough: {aLen:F2}");

        var dot = (wDx / wLen) * (aDx / aLen) + (wDz / wLen) * (aDz / aLen);
        _output.WriteLine($"Dot product (normalized): {dot:F3}");

        Assert.InRange(dot, -0.3, 0.3);
    }
}
