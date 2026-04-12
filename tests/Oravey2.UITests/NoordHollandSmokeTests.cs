using Brinell.Stride.Context;
using Brinell.Stride.Infrastructure;
using Brinell.Stride.Testing;
using Oravey2.Core.Data;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Test fixture that imports the Noord-Holland content pack into world.db,
/// then launches Oravey2 with --scenario Noord-Holland.
/// </summary>
public class NoordHollandTestFixture : StrideTestFixtureBase
{
    protected override string GetDefaultAppPath()
    {
        var solutionDir = FindSolutionDirectory();
        return Path.Combine(solutionDir,
            "src", "Oravey2.Windows", "bin", "Debug", "net10.0", "Oravey2.Windows.exe");
    }

    protected override StrideTestContextOptions CreateOptions()
    {
        EnsureWorldDb();

        var options = base.CreateOptions();
        options.GameArguments = ["--automation", "--scenario", "Noord-Holland"];
        options.StartupTimeoutMs = 30000;
        options.ConnectionTimeoutMs = 15000;
        options.DefaultTimeoutMs = 5000;
        return options;
    }

    /// <summary>
    /// Imports the Noord-Holland content pack into the user's world.db,
    /// using ORAVEY2_WORLD_DB to redirect to a test-specific location.
    /// </summary>
    private void EnsureWorldDb()
    {
        var solutionDir = FindSolutionDirectory();
        var contentPackDir = Path.Combine(solutionDir,
            "src", "Oravey2.Windows", "bin", "Debug", "net10.0",
            "ContentPacks", "Oravey2.Apocalyptic.NL.NH");

        if (!Directory.Exists(contentPackDir))
            throw new InvalidOperationException(
                $"Content pack not found at '{contentPackDir}'. Build Oravey2.Windows first.");

        // Use a test-specific world.db path via env var
        var testDbDir = Path.Combine(Path.GetTempPath(), "Oravey2-UITests");
        Directory.CreateDirectory(testDbDir);
        var worldDbPath = Path.Combine(testDbDir, "world.db");
        Environment.SetEnvironmentVariable("ORAVEY2_WORLD_DB", worldDbPath);

        // Check if region already imported
        if (File.Exists(worldDbPath))
        {
            using var checkStore = new WorldMapStore(worldDbPath);
            var existing = checkStore.GetRegionByName("Noord-Holland");
            if (existing != null)
                return;
        }

        // Try fast path: copy the pre-built world.db from the content pack
        var packDbPath = Path.Combine(contentPackDir, "world.db");
        if (File.Exists(packDbPath))
        {
            File.Copy(packDbPath, worldDbPath, overwrite: true);
            return;
        }

        // Fallback: import from content pack source data
        using var store = new WorldMapStore(worldDbPath);
        var importer = new ContentPackImporter(store);
        var result = importer.Import(contentPackDir);

        if (result.ChunksWritten == 0)
            throw new InvalidOperationException(
                $"Content pack import produced 0 chunks. Warnings: {string.Join("; ", result.Warnings)}");
    }
}

/// <summary>
/// Smoke tests for the Noord-Holland pipeline-to-game integration.
/// Verifies that the content pack loads in-game and basic gameplay works.
/// </summary>
public class NoordHollandSmokeTests : IAsyncLifetime
{
    private readonly NoordHollandTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    [Trait("Category", "Smoke")]
    public void NoordHolland_LoadsSuccessfully()
    {
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void NoordHolland_PlayerSpawns()
    {
        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        Assert.True(pos.Y >= 0, $"Player Y should be >= 0 (ground level), was {pos.Y}");
    }

    [Fact]
    public void NoordHolland_PlayerIsOnScreen()
    {
        var screen = GameQueryHelpers.GetPlayerScreenPosition(_fixture.Context);
        Assert.True(screen.OnScreen, "Player should be visible on screen");
    }

    [Fact]
    public void NoordHolland_SceneHasEntities()
    {
        var diag = GameQueryHelpers.GetSceneDiagnostics(_fixture.Context);
        // At minimum: Player, TileMap, NotificationFeed, HUD, InventoryOverlay, GameOverOverlay + buildings/props
        Assert.True(diag.TotalEntities >= 6,
            $"Expected at least 6 entities (player + terrain + HUD + overlays), got {diag.TotalEntities}");
    }

    [Fact]
    public void NoordHolland_TerrainExists()
    {
        // Tile at player origin — should be a valid tile type, not default/empty
        var tile = GameQueryHelpers.GetTileAtWorldPos(_fixture.Context, 0, 0);
        Assert.True(tile.TileX >= 0 && tile.TileZ >= 0,
            $"Tile coordinates should be valid, got ({tile.TileX}, {tile.TileZ})");
    }

    [Fact]
    public void NoordHolland_PlayerCanMove()
    {
        var before = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.W, 500);
        var after = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        var dx = after.X - before.X;
        var dz = after.Z - before.Z;
        var distance = Math.Sqrt(dx * dx + dz * dz);

        Assert.True(distance > 0.1, $"Player should have moved, distance was {distance:F3}");
    }

    [Fact]
    public void NoordHolland_CameraFollowsPlayer()
    {
        var cam = GameQueryHelpers.GetCameraState(_fixture.Context);
        var player = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Camera should be roughly centered on player (within isometric offset tolerance)
        Assert.True(Math.Abs(cam.X - player.X) < 50,
            $"Camera X ({cam.X:F1}) should be near player X ({player.X:F1})");
    }

    [Fact]
    public void NoordHolland_HudIsVisible()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal("Exploring", hud.GameState);
        Assert.True(hud.MaxHp > 0, "Player should have max HP > 0");
        Assert.True(hud.Hp > 0, "Player should have current HP > 0");
    }

    [Fact]
    public void NoordHolland_ScreenshotIsNotBlack()
    {
        var path = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        Assert.True(File.Exists(path), $"Screenshot should exist at {path}");

        var fileInfo = new FileInfo(path);
        Assert.True(fileInfo.Length > 1024,
            $"Screenshot should be > 1KB (not a blank/corrupt image), was {fileInfo.Length} bytes");
    }
}
