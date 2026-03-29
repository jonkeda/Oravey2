using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Tests for fullscreen toggle functionality.
/// Skipped until a GetWindowState automation query is available.
/// </summary>
public class FullscreenTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Skip = "Requires GetWindowState automation query for fullscreen detection")]
    public void F11_TogglesFullscreen()
    {
        // Would press F11 and verify window state changed via GetWindowState query
        // or detect resolution change via screenshot dimensions.
    }
}
