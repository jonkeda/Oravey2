using Oravey2.MapGen.Pipeline;

namespace Oravey2.Tests.Pipeline;

public class PipelineStateTests
{
    [Fact]
    public void PipelineState_DefaultValues_Step1Unlocked()
    {
        var state = new PipelineState();

        Assert.Equal(1, state.CurrentStep);
        Assert.True(state.IsStepUnlocked(1));
        Assert.False(state.IsStepUnlocked(2));
        Assert.Equal(string.Empty, state.RegionName);
        Assert.Equal(string.Empty, state.ContentPackPath);
    }

    [Fact]
    public void PipelineState_IsStepCompleted_ReturnsFalseForAllByDefault()
    {
        var state = new PipelineState();

        for (var step = 1; step <= 8; step++)
            Assert.False(state.IsStepCompleted(step));
    }

    [Fact]
    public void PipelineState_IsStepCompleted_ReturnsFalseForInvalidStep()
    {
        var state = new PipelineState();

        Assert.False(state.IsStepCompleted(0));
        Assert.False(state.IsStepCompleted(9));
        Assert.False(state.IsStepCompleted(-1));
    }

    [Fact]
    public void PipelineState_IsStepUnlocked_Step1AlwaysUnlocked()
    {
        var state = new PipelineState();

        Assert.True(state.IsStepUnlocked(1));
        Assert.True(state.IsStepUnlocked(0));
        Assert.True(state.IsStepUnlocked(-1));
    }

    [Fact]
    public void PipelineState_IsStepUnlocked_RequiresPreviousCompleted()
    {
        var state = new PipelineState();
        state.Region.Completed = true;

        Assert.True(state.IsStepUnlocked(2));
        Assert.False(state.IsStepUnlocked(3));
    }

    [Fact]
    public void PipelineState_TryAdvance_SucceedsWhenCurrentCompleted()
    {
        var state = new PipelineState { CurrentStep = 1 };
        state.Region.Completed = true;

        var result = state.TryAdvance();

        Assert.True(result);
        Assert.Equal(2, state.CurrentStep);
    }

    [Fact]
    public void PipelineState_TryAdvance_FailsWhenCurrentNotCompleted()
    {
        var state = new PipelineState { CurrentStep = 1 };

        var result = state.TryAdvance();

        Assert.False(result);
        Assert.Equal(1, state.CurrentStep);
    }

    [Fact]
    public void PipelineState_TryAdvance_FailsAtStep8()
    {
        var state = new PipelineState { CurrentStep = 8 };
        state.Assembly.Completed = true;

        var result = state.TryAdvance();

        Assert.False(result);
        Assert.Equal(8, state.CurrentStep);
    }

    [Fact]
    public void PipelineState_MultiStepAdvancement_UnlocksChain()
    {
        var state = new PipelineState { CurrentStep = 1 };

        state.Region.Completed = true;
        Assert.True(state.TryAdvance());
        Assert.Equal(2, state.CurrentStep);

        state.Download.Completed = true;
        Assert.True(state.TryAdvance());
        Assert.Equal(3, state.CurrentStep);

        state.Parse.Completed = true;
        Assert.True(state.TryAdvance());
        Assert.Equal(4, state.CurrentStep);

        Assert.True(state.IsStepUnlocked(4));
        Assert.False(state.IsStepUnlocked(5));
    }

    [Fact]
    public void DownloadStepState_DefaultValues()
    {
        var state = new DownloadStepState();

        Assert.False(state.Completed);
        Assert.False(state.SrtmDownloaded);
        Assert.False(state.OsmDownloaded);
    }

    [Fact]
    public void TownDesignStepState_DefaultValues_EmptyDesignedList()
    {
        var state = new TownDesignStepState();

        Assert.False(state.Completed);
        Assert.Empty(state.Designed);
        Assert.Equal(0, state.Remaining);
    }

    [Fact]
    public void AssetsStepState_DefaultValues()
    {
        var state = new AssetsStepState();

        Assert.False(state.Completed);
        Assert.Equal(0, state.Ready);
        Assert.Equal(0, state.Pending);
        Assert.Equal(0, state.Failed);
    }
}
