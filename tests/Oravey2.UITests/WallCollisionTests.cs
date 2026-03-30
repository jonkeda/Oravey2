using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests that tile-based collision prevents the player from walking off the map or through walls.
/// Map is 32x32, TileSize=1.0. Walkable tiles 1-30, tile edge at ±15.0, wall tile center at ±15.5.
/// </summary>
public class WallCollisionTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void PlayerCannot_WalkPastNorthWall()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 0, 0.5, 13.5);
        _fixture.Context.HoldKey(VirtualKey.W, 2000);

        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        Assert.True(pos.Z < 15.5, $"Player Z ({pos.Z}) should not reach wall tile (< 15.5)");
    }

    [Fact]
    public void PlayerCannot_WalkPastSouthWall()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 0, 0.5, -13.5);
        _fixture.Context.HoldKey(VirtualKey.S, 2000);

        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        Assert.True(pos.Z > -15.5, $"Player Z ({pos.Z}) should not reach wall tile (> -15.5)");
    }

    [Fact]
    public void PlayerCannot_WalkPastEastWall()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 13.5, 0.5, 0);
        _fixture.Context.HoldKey(VirtualKey.D, 2000);

        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        Assert.True(pos.X < 15.5, $"Player X ({pos.X}) should not reach wall tile (< 15.5)");
    }

    [Fact]
    public void PlayerCannot_WalkPastWestWall()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, -13.5, 0.5, 0);
        _fixture.Context.HoldKey(VirtualKey.A, 2000);

        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        Assert.True(pos.X > -15.5, $"Player X ({pos.X}) should not reach wall tile (> -15.5)");
    }

    [Fact]
    public void PlayerSlides_AlongWallDiagonal()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 13.5, 0.5, 13.5);
        var startPos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        _fixture.Context.HoldKey(VirtualKey.W, 1000);

        var endPos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Player should have moved (not stuck) but stayed inside bounds
        var moved = Math.Abs(endPos.X - startPos.X) > 0.01 || Math.Abs(endPos.Z - startPos.Z) > 0.01;
        Assert.True(moved, "Player should not be stuck at teleport point");
        Assert.True(endPos.X < 15.5, $"Player X ({endPos.X}) should not reach wall tile");
        Assert.True(endPos.Z < 15.5, $"Player Z ({endPos.Z}) should not reach wall tile");
    }

    [Fact]
    public void PlayerOnWalkableTile_AfterCollision()
    {
        // Teleport near north wall and try to walk through it
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 0, 0.5, 13.5);
        _fixture.Context.HoldKey(VirtualKey.W, 2000);

        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        var tile = GameQueryHelpers.GetTileAtWorldPos(_fixture.Context, pos.X, pos.Z);

        // After collision, player should be on a walkable tile type
        var walkableTypes = new[] { "Ground", "Road", "Rubble" };
        Assert.Contains(tile.TileType, walkableTypes);
    }
}
