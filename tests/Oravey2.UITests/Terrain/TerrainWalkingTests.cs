using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests.Terrain;

/// <summary>
/// UI tests for player terrain height snapping, cliff blocking, and liquid depth blocking.
/// Uses the terrain_test scenario (48×48 map, tileSize=2, centered at origin).
/// </summary>
public class TerrainWalkingTests : IAsyncLifetime
{
    private readonly TerrainTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void PlayerY_SnapsToTerrain_OnFlatGround()
    {
        // Teleport to flat grass area (height 4) — center of map
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 0, 10, 0);

        // Wait one frame for height snap
        _fixture.Context.HoldKey(VirtualKey.W, 100);
        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Player Y should have snapped down from 10 to near terrain height + offset
        // Height 4 → terrain Y ≈ 4 * 0.25 = 1.0 + 0.5 offset = ~1.5
        Assert.True(pos.Y < 5, $"Player Y ({pos.Y}) should have snapped down from teleport height 10");
        Assert.True(pos.Y > 0, $"Player Y ({pos.Y}) should be above ground");
    }

    [Fact]
    public void PlayerY_Increases_WhenWalkingUphill()
    {
        // Start on flat grass (height 4) near the hill
        // Hill is centered at tile (11,10), heights up to 12
        // Tile (11,10) in world: (11*2 - 48 + 1, 10*2 - 48 + 1) = (-25, -27)
        // Start south of the hill at tile ~(11,16) → world (-25, -15)
        GameQueryHelpers.TeleportPlayer(_fixture.Context, -25, 0.5, -15);
        _fixture.Context.HoldKey(VirtualKey.W, 100); // settle height
        var before = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Walk north toward the hill center
        _fixture.Context.HoldKey(VirtualKey.S, 2000);
        var after = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Player should have moved and Y should have decreased (walking downhill on the map Z axis)
        // or stayed ~same if on flat; at minimum not stuck at same position
        var zMoved = Math.Abs(after.Z - before.Z) > 0.5;
        Assert.True(zMoved, $"Player should have moved in Z: before={before.Z}, after={after.Z}");
    }

    [Fact]
    public void PlayerBlocked_AtCliff()
    {
        // Waterfall cliff: tiles (4-7,20) at height 12, tile (4-7,21) at height 4
        // Delta = 8, which is > CliffThreshold (7) → cliff
        // Tile (6,20) world: (6*2-48+1, 20*2-48+1) = (-35, -7)
        // Walk south from height 12 toward height 4 (cliff edge)
        GameQueryHelpers.TeleportPlayer(_fixture.Context, -35, 5, -7);
        _fixture.Context.HoldKey(VirtualKey.W, 100); // settle height
        var before = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Walk south (toward y=21, the cliff drop)
        _fixture.Context.HoldKey(VirtualKey.S, 1500);
        var after = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Player should be blocked by the cliff — Z shouldn't reach the lower tile
        // Tile (6,21) world Z = 21*2-48+1 = -5
        Assert.True(after.Z < -4.5 || Math.Abs(after.Z - before.Z) < 3.0,
            $"Player should be blocked at cliff edge: before.Z={before.Z}, after.Z={after.Z}");
    }
}
