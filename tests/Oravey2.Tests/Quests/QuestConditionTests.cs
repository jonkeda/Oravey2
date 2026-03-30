using Oravey2.Core.Character.Stats;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Quests;
using Oravey2.Core.World;

namespace Oravey2.Tests.Quests;

public class QuestConditionTests
{
    private static QuestContext MakeContext(
        Action<WorldStateService>? setupFlags = null,
        Action<InventoryComponent>? setupInventory = null,
        Action<QuestLogComponent>? setupLog = null)
    {
        var stats = new StatsComponent();
        var inv = new InventoryComponent(stats);
        var world = new WorldStateService();
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        var log = new QuestLogComponent();

        setupFlags?.Invoke(world);
        setupInventory?.Invoke(inv);
        setupLog?.Invoke(log);

        return new QuestContext(inv, world, level, log, bus);
    }

    private static ItemDefinition MakeItem(string id, bool stackable = false, int maxStack = 1)
        => new(id, id, "", ItemCategory.Junk, 1f, stackable, 10, MaxStack: maxStack);

    // --- HasItemCondition ---

    [Fact]
    public void HasItem_Present_Passes()
    {
        var ctx = MakeContext(setupInventory: inv =>
            inv.Add(new ItemInstance(MakeItem("supply_crate"))));
        var cond = new HasItemCondition("supply_crate");
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void HasItem_Absent_Fails()
    {
        var ctx = MakeContext();
        var cond = new HasItemCondition("supply_crate");
        Assert.False(cond.Evaluate(ctx));
    }

    [Fact]
    public void HasItem_InsufficientCount_Fails()
    {
        var ctx = MakeContext(setupInventory: inv =>
            inv.Add(new ItemInstance(MakeItem("supply_crate", stackable: true, maxStack: 10), stackCount: 1)));
        var cond = new HasItemCondition("supply_crate", 3);
        Assert.False(cond.Evaluate(ctx));
    }

    // --- QuestFlagCondition ---

    [Fact]
    public void QuestFlag_Set_Passes()
    {
        var ctx = MakeContext(setupFlags: w => w.SetFlag("outpost_reached", true));
        var cond = new QuestFlagCondition("outpost_reached");
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void QuestFlag_NotSet_Fails()
    {
        var ctx = MakeContext();
        var cond = new QuestFlagCondition("outpost_reached");
        Assert.False(cond.Evaluate(ctx));
    }

    [Fact]
    public void QuestFlag_ExpectedFalse_Passes()
    {
        var ctx = MakeContext();
        var cond = new QuestFlagCondition("outpost_reached", expected: false);
        Assert.True(cond.Evaluate(ctx));
    }

    // --- QuestLevelCondition ---

    [Fact]
    public void QuestLevel_Above_Passes()
    {
        var ctx = MakeContext();
        ctx.Level.GainXP(50000);
        var cond = new QuestLevelCondition(5);
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void QuestLevel_Below_Fails()
    {
        var ctx = MakeContext();
        var cond = new QuestLevelCondition(5);
        Assert.False(cond.Evaluate(ctx));
    }

    [Fact]
    public void QuestLevel_Exact_Passes()
    {
        var ctx = MakeContext();
        var cond = new QuestLevelCondition(1);
        Assert.True(cond.Evaluate(ctx));
    }

    // --- QuestCompleteCondition ---

    [Fact]
    public void QuestComplete_Completed_Passes()
    {
        var ctx = MakeContext(setupLog: log =>
        {
            log.StartQuest("prereq", "s1");
            log.CompleteQuest("prereq");
        });
        var cond = new QuestCompleteCondition("prereq");
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void QuestComplete_Active_Fails()
    {
        var ctx = MakeContext(setupLog: log => log.StartQuest("prereq", "s1"));
        var cond = new QuestCompleteCondition("prereq");
        Assert.False(cond.Evaluate(ctx));
    }

    [Fact]
    public void QuestComplete_NotStarted_Fails()
    {
        var ctx = MakeContext();
        var cond = new QuestCompleteCondition("prereq");
        Assert.False(cond.Evaluate(ctx));
    }
}
