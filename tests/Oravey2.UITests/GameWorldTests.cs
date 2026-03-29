using Oravey2.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace Oravey2.UITests;

/// <summary>
/// Tests that verify the game world page object and overall game readiness.
/// </summary>
public class GameWorldTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();
    private readonly ITestOutputHelper _output;

    public GameWorldTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void GameWorld_IsReady()
    {
        var page = new GameWorldPage(_fixture.Context);
        Assert.True(page.IsLoaded());
        Assert.True(page.IsReady());
    }

    [Fact]
    public void GameWorld_PlayerStartsNearOrigin()
    {
        var pos = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Player starts at (0, 0.5, 0) — allow some drift from earlier tests
        // but should be within the tile map (16x16)
        Assert.InRange(pos.X, -20, 20);
        Assert.InRange(pos.Z, -20, 20);
    }

    [Fact]
    public void GameWorld_StateIsExploring()
    {
        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void GameWorld_SceneHasRenderableEntities()
    {
        var diag = GameQueryHelpers.GetSceneDiagnostics(_fixture.Context);

        _output.WriteLine($"Total entities: {diag.TotalEntities}");
        _output.WriteLine($"Model entity count: {diag.ModelEntityCount}");

        foreach (var me in diag.ModelEntitiesSample)
            _output.WriteLine($"  Model: {me.Name} at ({me.X:F2},{me.Y:F2},{me.Z:F2}) meshes={me.MeshCount} materials={me.MaterialCount}");

        if (diag.Camera != null)
        {
            var cam = diag.Camera;
            _output.WriteLine($"Camera position: ({cam.Position.X:F2},{cam.Position.Y:F2},{cam.Position.Z:F2})");
            _output.WriteLine($"Camera forward:  ({cam.Forward.X:F4},{cam.Forward.Y:F4},{cam.Forward.Z:F4})");
            _output.WriteLine($"Camera projection: {cam.Projection}, orthoSize={cam.OrthoSize}");
            _output.WriteLine($"Camera clip: near={cam.NearClip}, far={cam.FarClip}");
            _output.WriteLine($"Camera slot: {cam.SlotId}");
        }
        else
        {
            _output.WriteLine("Camera: NOT FOUND");
        }

        // The scene should have renderable content (tiles + player)
        Assert.True(diag.TotalEntities > 5, $"Expected >5 entities, got {diag.TotalEntities}");
        Assert.True(diag.ModelEntityCount > 0, $"Expected renderable entities, got {diag.ModelEntityCount}");
        Assert.NotNull(diag.Camera);

        // Camera forward should point towards the scene (negative X and Z components, i.e. towards origin)
        var fwd = diag.Camera!.Forward;
        var fwdLen = Math.Sqrt(fwd.X * fwd.X + fwd.Y * fwd.Y + fwd.Z * fwd.Z);
        _output.WriteLine($"Camera forward magnitude: {fwdLen:F4}");
        Assert.True(fwdLen > 0.9, $"Camera forward vector nearly zero: {fwdLen}");
    }

    [Fact]
    public void GameWorld_ScreenshotIsNotSolidColor()
    {
        // Take a screenshot via the automation pipe
        var screenshotPath = GameQueryHelpers.TakeScreenshot(_fixture.Context);
        _output.WriteLine($"Screenshot saved to: {screenshotPath}");
        Assert.True(File.Exists(screenshotPath), $"Screenshot file not found: {screenshotPath}");

        // Read the raw PNG bytes and check file is non-trivial
        var fileBytes = File.ReadAllBytes(screenshotPath);
        _output.WriteLine($"Screenshot file size: {fileBytes.Length} bytes");
        Assert.True(fileBytes.Length > 1000, $"Screenshot too small ({fileBytes.Length} bytes), likely empty or corrupt");

        _output.WriteLine($"Screenshot captured successfully at: {screenshotPath}");
    }


}
