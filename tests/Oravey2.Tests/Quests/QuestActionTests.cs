using Oravey2.Core.Character.Stats;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Quests;
using Oravey2.Core.World;

namespace Oravey2.Tests.Quests;

public class QuestActionTests
{
    private static (QuestContext ctx, EventBus bus) MakeContext()
    {
        var stats = new StatsComponent();
        var inv = new InventoryComponent(stats);
        var world = new WorldStateService();
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        var log = new QuestLogComponent();
        var ctx = new QuestContext(inv, world, level, log, bus);
        return (ctx, bus);
    }

    [Fact]
    public void SetFlag_SetsWorldFlag()
    {
        var (ctx, _) = MakeContext();
        var action = new QuestSetFlagAction("flag", true);
        action.Execute(ctx);
        Assert.True(ctx.WorldState.GetFlag("flag"));
    }

    [Fact]
    public void SetFlag_CanSetFalse()
    {
        var (ctx, _) = MakeContext();
        ctx.WorldState.SetFlag("flag", true);
        var action = new QuestSetFlagAction("flag", false);
        action.Execute(ctx);
        Assert.False(ctx.WorldState.GetFlag("flag"));
    }

    [Fact]
    public void GiveXP_AddsXP()
    {
        var (ctx, _) = MakeContext();
        var before = ctx.Level.CurrentXP;
        var action = new QuestGiveXPAction(200);
        action.Execute(ctx);
        Assert.Equal(before + 200, ctx.Level.CurrentXP);
    }

    [Fact]
    public void UpdateJournal_PublishesJournalUpdatedEvent()
    {
        var (ctx, bus) = MakeContext();
        JournalUpdatedEvent? received = null;
        bus.Subscribe<JournalUpdatedEvent>(e => received = e);

        var action = new UpdateJournalAction("supply_run", "Delivered the goods.");
        action.Execute(ctx);

        Assert.NotNull(received);
        Assert.Equal("supply_run", received.Value.QuestId);
        Assert.Equal("Delivered the goods.", received.Value.Text);
    }
}
