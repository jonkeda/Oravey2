using Brinell.Stride.Communication;
using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests.Generation;

public class WorldGenerationTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Skip = "Requires pre-generated world.db and 'generated' scenario wiring — blocked on Step 09 full integration")]
    public void NewGame_GeneratesWorld_TerrainVisible()
    {
        // Once world generation is wired end-to-end:
        // 1. Trigger "generated" scenario via automation
        // 2. Wait for terrain to appear
        // 3. Verify scene has terrain entities and player is on a valid tile

        var diag = GameQueryHelpers.GetSceneDiagnostics(_fixture.Context);
        Assert.True(diag.TotalEntities > 0, "Scene should have entities after world generation");

        var tile = GameQueryHelpers.GetTileAtWorldPos(_fixture.Context, 0, 0);
        Assert.NotEqual("Empty", tile.TileType);
    }
}
