using Oravey2.Core.Character.Stats;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Dialogue;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.World;

namespace Oravey2.Tests.Dialogue;

public class ConsequenceActionTests
{
    private static (DialogueContext ctx, EventBus bus) MakeContext()
    {
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);
        var inv = new InventoryComponent(stats);
        var world = new WorldStateService();
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        var ctx = new DialogueContext(skills, inv, world, level, bus);
        return (ctx, bus);
    }

    [Fact]
    public void SetFlag_SetsWorldFlag()
    {
        var (ctx, _) = MakeContext();
        var action = new SetFlagAction("test_flag", true);
        action.Execute(ctx);
        Assert.True(ctx.WorldState.GetFlag("test_flag"));
    }

    [Fact]
    public void SetFlag_CanSetFalse()
    {
        var (ctx, _) = MakeContext();
        ctx.WorldState.SetFlag("test_flag", true);
        var action = new SetFlagAction("test_flag", false);
        action.Execute(ctx);
        Assert.False(ctx.WorldState.GetFlag("test_flag"));
    }

    [Fact]
    public void GiveXP_AddsXPToLevel()
    {
        var (ctx, _) = MakeContext();
        var before = ctx.Level.CurrentXP;
        var action = new GiveXPAction(100);
        action.Execute(ctx);
        Assert.Equal(before + 100, ctx.Level.CurrentXP);
    }

    [Fact]
    public void StartQuest_PublishesQuestStartRequestedEvent()
    {
        var (ctx, bus) = MakeContext();
        QuestStartRequestedEvent? received = null;
        bus.Subscribe<QuestStartRequestedEvent>(e => received = e);

        var action = new StartQuestAction("supply_run");
        action.Execute(ctx);

        Assert.NotNull(received);
        Assert.Equal("supply_run", received.Value.QuestId);
    }
}
