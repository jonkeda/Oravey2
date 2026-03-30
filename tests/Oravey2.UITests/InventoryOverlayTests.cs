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

    [Fact]
    public void Overlay_ShowsStartingItems()
    {
        _fixture.Context.PressKey(VirtualKey.Tab);
        var inventory = GameQueryHelpers.GetInventoryState(_fixture.Context);
        var medkit = inventory.Items.FirstOrDefault(i => i.Id == "medkit");
        Assert.NotNull(medkit);
        Assert.Equal(2, medkit.Count);
    }

    [Fact]
    public void Overlay_ShowsCorrectWeight()
    {
        _fixture.Context.PressKey(VirtualKey.Tab);
        var inventory = GameQueryHelpers.GetInventoryState(_fixture.Context);
        // 2 Medkits × 0.5 = 1.0
        Assert.Equal(1.0, inventory.CurrentWeight, 0.1);
    }
}
