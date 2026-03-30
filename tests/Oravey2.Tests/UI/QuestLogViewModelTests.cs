using Oravey2.Core.Quests;
using Oravey2.Core.UI.ViewModels;

namespace Oravey2.Tests.UI;

public class QuestLogViewModelTests
{
    [Fact]
    public void Create_SeparatesActive()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "s1");
        log.StartQuest("q2", "s1");
        var vm = QuestLogViewModel.Create(log);
        Assert.Equal(2, vm.Active.Count);
    }

    [Fact]
    public void Create_SeparatesCompleted()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "s1");
        log.CompleteQuest("q1");
        var vm = QuestLogViewModel.Create(log);
        Assert.Single(vm.Completed);
    }

    [Fact]
    public void Create_SeparatesFailed()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "s1");
        log.FailQuest("q1");
        var vm = QuestLogViewModel.Create(log);
        Assert.Single(vm.Failed);
    }

    [Fact]
    public void Create_IncludesCurrentStage()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q1", "s1");
        var vm = QuestLogViewModel.Create(log);
        Assert.Equal("s1", vm.Active[0].CurrentStageId);
    }

    [Fact]
    public void Create_EmptyLog_AllEmpty()
    {
        var log = new QuestLogComponent();
        var vm = QuestLogViewModel.Create(log);
        Assert.Empty(vm.Active);
        Assert.Empty(vm.Completed);
        Assert.Empty(vm.Failed);
    }
}
