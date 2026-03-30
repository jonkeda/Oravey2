using Oravey2.Core.Quests;

namespace Oravey2.Tests.Quests;

public class QuestLogComponentTests
{
    [Fact]
    public void StartQuest_SetsActive()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "stage_1");
        Assert.Equal(QuestStatus.Active, log.GetStatus("q1"));
    }

    [Fact]
    public void StartQuest_SetsFirstStage()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "stage_1");
        Assert.Equal("stage_1", log.GetCurrentStage("q1"));
    }

    [Fact]
    public void StartQuest_AlreadyActive_NoOp()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "stage_1");
        log.StartQuest("q1", "stage_other");
        Assert.Equal("stage_1", log.GetCurrentStage("q1"));
    }

    [Fact]
    public void AdvanceStage_UpdatesStage()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "stage_1");
        log.AdvanceStage("q1", "stage_2");
        Assert.Equal("stage_2", log.GetCurrentStage("q1"));
    }

    [Fact]
    public void AdvanceStage_NotActive_NoOp()
    {
        var log = new QuestLogComponent();
        log.AdvanceStage("q1", "stage_2");
        Assert.Null(log.GetCurrentStage("q1"));
    }

    [Fact]
    public void CompleteQuest_SetsCompleted()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "stage_1");
        log.CompleteQuest("q1");
        Assert.Equal(QuestStatus.Completed, log.GetStatus("q1"));
    }

    [Fact]
    public void CompleteQuest_RemovesCurrentStage()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "stage_1");
        log.CompleteQuest("q1");
        Assert.Null(log.GetCurrentStage("q1"));
    }

    [Fact]
    public void FailQuest_SetsFailed()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "stage_1");
        log.FailQuest("q1");
        Assert.Equal(QuestStatus.Failed, log.GetStatus("q1"));
    }

    [Fact]
    public void FailQuest_RemovesCurrentStage()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "stage_1");
        log.FailQuest("q1");
        Assert.Null(log.GetCurrentStage("q1"));
    }

    [Fact]
    public void GetStatus_Unknown_ReturnsNotStarted()
    {
        var log = new QuestLogComponent();
        Assert.Equal(QuestStatus.NotStarted, log.GetStatus("unknown"));
    }

    [Fact]
    public void GetCurrentStage_Unknown_ReturnsNull()
    {
        var log = new QuestLogComponent();
        Assert.Null(log.GetCurrentStage("unknown"));
    }
}
