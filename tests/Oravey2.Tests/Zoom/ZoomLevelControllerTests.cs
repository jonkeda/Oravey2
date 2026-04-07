using Oravey2.Core.Camera;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Tests.Zoom;

public class ZoomLevelControllerTests
{
    private static ZoomLevelController Create() => new(new EventBus());

    [Fact]
    public void Altitude10_IsLevel1()
    {
        var ctrl = Create();
        ctrl.Update(10f);

        Assert.Equal(ZoomLevel.Local, ctrl.CurrentLevel);
        Assert.Equal(0f, ctrl.TransitionAlpha);
    }

    [Fact]
    public void Altitude40_IsTransition_L1L2()
    {
        var ctrl = Create();
        ctrl.Update(40f);

        Assert.Equal(ZoomLevel.Local, ctrl.CurrentLevel);
        Assert.True(ctrl.TransitionAlpha > 0f && ctrl.TransitionAlpha < 1f,
            $"Expected alpha between 0 and 1 at 40m, got {ctrl.TransitionAlpha}");
        // 40 is halfway between 30..50 → alpha ≈ 0.5
        Assert.Equal(0.5f, ctrl.TransitionAlpha, 0.01f);
    }

    [Fact]
    public void Altitude100_IsLevel2()
    {
        var ctrl = Create();
        ctrl.Update(100f);

        Assert.Equal(ZoomLevel.Regional, ctrl.CurrentLevel);
        Assert.Equal(0f, ctrl.TransitionAlpha);
    }

    [Fact]
    public void Altitude500_IsTransition_L2L3()
    {
        var ctrl = Create();
        ctrl.Update(500f);

        Assert.Equal(ZoomLevel.Regional, ctrl.CurrentLevel);
        Assert.True(ctrl.TransitionAlpha > 0f && ctrl.TransitionAlpha < 1f,
            $"Expected alpha between 0 and 1 at 500m, got {ctrl.TransitionAlpha}");
        // 500 is halfway between 400..600 → alpha = 0.5
        Assert.Equal(0.5f, ctrl.TransitionAlpha, 0.01f);
    }

    [Fact]
    public void Altitude1000_IsLevel3()
    {
        var ctrl = Create();
        ctrl.Update(1000f);

        Assert.Equal(ZoomLevel.Continental, ctrl.CurrentLevel);
        Assert.Equal(0f, ctrl.TransitionAlpha);
    }

    [Fact]
    public void TransitionFromL1ToL2_PublishesEvent()
    {
        var bus = new EventBus();
        var ctrl = new ZoomLevelController(bus);
        ZoomLevelChangedEvent? captured = null;
        bus.Subscribe<ZoomLevelChangedEvent>(e => captured = e);

        ctrl.Update(10f);  // L1
        Assert.Null(captured);

        ctrl.Update(100f); // Jump to L2
        Assert.NotNull(captured);
        Assert.Equal(ZoomLevel.Local, captured.Value.OldLevel);
        Assert.Equal(ZoomLevel.Regional, captured.Value.NewLevel);
    }

    [Fact]
    public void AtExactBoundary30_StillL1()
    {
        var ctrl = Create();
        ctrl.Update(30f);

        // At exactly 30m, alpha = 0 → still fully L1 (start of transition zone)
        Assert.Equal(ZoomLevel.Local, ctrl.CurrentLevel);
        Assert.Equal(0f, ctrl.TransitionAlpha, 0.001f);
    }

    [Fact]
    public void AtExactBoundary50_IsL2()
    {
        var ctrl = Create();
        ctrl.Update(50f);

        Assert.Equal(ZoomLevel.Regional, ctrl.CurrentLevel);
        Assert.Equal(0f, ctrl.TransitionAlpha);
    }
}
