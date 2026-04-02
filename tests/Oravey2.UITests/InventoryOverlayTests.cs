using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Verifies Tab toggles the inventory overlay and displays correct content.
/// </summary>
public class InventoryOverlayTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void Overlay_NotVisible_AtStart()
    {
        var visible = GameQueryHelpers.GetInventoryOverlayVisible(_fixture.Context);
        Assert.False(visible);
    }

    [Fact]
    public void TabPress_OpensOverlay()
    {
        _fixture.Context.PressKey(VirtualKey.Tab);
        var visible = GameQueryHelpers.GetInventoryOverlayVisible(_fixture.Context);
        Assert.True(visible);
    }

    [Fact]
    public void TabPress_ClosesOverlay()
    {
        _fixture.Context.PressKey(VirtualKey.Tab);
        _fixture.Context.PressKey(VirtualKey.Tab);
        var visible = GameQueryHelpers.GetInventoryOverlayVisible(_fixture.Context);
        Assert.False(visible);
    }


}
