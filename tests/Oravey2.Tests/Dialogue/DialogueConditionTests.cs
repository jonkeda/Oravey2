using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Dialogue;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Character.Level;
using Oravey2.Core.World;

namespace Oravey2.Tests.Dialogue;

public class DialogueConditionTests
{
    private static DialogueContext MakeContext(
        int speechBase = 20,
        int charisma = 5,
        int level = 1,
        Action<WorldStateService>? setupFlags = null,
        Action<InventoryComponent>? setupInventory = null)
    {
        var stats = new StatsComponent(new Dictionary<Stat, int> { { Stat.Charisma, charisma } });
        var skills = new SkillsComponent(stats);
        var inv = new InventoryComponent(stats);
        var world = new WorldStateService();
        var bus = new EventBus();
        var lev = new LevelComponent(stats, bus);

        setupFlags?.Invoke(world);
        setupInventory?.Invoke(inv);

        return new DialogueContext(skills, inv, world, lev, bus);
    }

    private static ItemDefinition MakeItem(string id, bool stackable = false, int maxStack = 1)
        => new(id, id, "", ItemCategory.Junk, 1f, stackable, 10, MaxStack: maxStack);

    // --- SkillCheckCondition ---

    [Fact]
    public void SkillCheck_AboveThreshold_Passes()
    {
        // Charisma=8 → Speech base = 10 + 8*2 = 26, effective = 26 (no modifiers)
        var ctx = MakeContext(charisma: 8);
        var cond = new SkillCheckCondition(SkillType.Speech, 20);
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void SkillCheck_BelowThreshold_Fails()
    {
        // Charisma=5 → Speech base = 10 + 5*2 = 20, effective = 20
        var ctx = MakeContext(charisma: 5);
        var cond = new SkillCheckCondition(SkillType.Speech, 40);
        Assert.False(cond.Evaluate(ctx));
    }

    [Fact]
    public void SkillCheck_ExactThreshold_Passes()
    {
        // Charisma=5 → Speech = 20
        var ctx = MakeContext(charisma: 5);
        var cond = new SkillCheckCondition(SkillType.Speech, 20);
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void SkillCheck_HiddenFlag_DoesNotAffectResult()
    {
        var ctx = MakeContext(charisma: 5);
        var visible = new SkillCheckCondition(SkillType.Speech, 20, hidden: false);
        var hidden = new SkillCheckCondition(SkillType.Speech, 20, hidden: true);
        Assert.Equal(visible.Evaluate(ctx), hidden.Evaluate(ctx));
    }

    // --- FlagCondition ---

    [Fact]
    public void FlagCondition_FlagSet_Passes()
    {
        var ctx = MakeContext(setupFlags: w => w.SetFlag("met_merchant", true));
        var cond = new FlagCondition("met_merchant");
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void FlagCondition_FlagNotSet_Fails()
    {
        var ctx = MakeContext();
        var cond = new FlagCondition("met_merchant");
        Assert.False(cond.Evaluate(ctx));
    }

    [Fact]
    public void FlagCondition_ExpectedFalse_PassesWhenNotSet()
    {
        var ctx = MakeContext();
        var cond = new FlagCondition("met_merchant", expected: false);
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void FlagCondition_ExpectedFalse_FailsWhenSet()
    {
        var ctx = MakeContext(setupFlags: w => w.SetFlag("met_merchant", true));
        var cond = new FlagCondition("met_merchant", expected: false);
        Assert.False(cond.Evaluate(ctx));
    }

    // --- ItemCondition ---

    [Fact]
    public void ItemCondition_HasEnough_Passes()
    {
        var ctx = MakeContext(setupInventory: inv =>
        {
            inv.Add(new ItemInstance(MakeItem("stimpak", stackable: true, maxStack: 10), stackCount: 3));
        });
        var cond = new ItemCondition("stimpak", 2);
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void ItemCondition_NotEnough_Fails()
    {
        var ctx = MakeContext(setupInventory: inv =>
        {
            inv.Add(new ItemInstance(MakeItem("stimpak", stackable: true, maxStack: 10), stackCount: 1));
        });
        var cond = new ItemCondition("stimpak", 2);
        Assert.False(cond.Evaluate(ctx));
    }

    [Fact]
    public void ItemCondition_ItemAbsent_Fails()
    {
        var ctx = MakeContext();
        var cond = new ItemCondition("stimpak");
        Assert.False(cond.Evaluate(ctx));
    }

    // --- LevelCondition ---

    [Fact]
    public void LevelCondition_AboveMin_Passes()
    {
        // GainXP enough to get to high level. XP for level 2 = 100*4 = 400
        var ctx = MakeContext();
        ctx.Level.GainXP(50000); // well above level 5
        var cond = new LevelCondition(5);
        Assert.True(cond.Evaluate(ctx));
    }

    [Fact]
    public void LevelCondition_BelowMin_Fails()
    {
        var ctx = MakeContext();
        var cond = new LevelCondition(5);
        Assert.False(cond.Evaluate(ctx));
    }

    [Fact]
    public void LevelCondition_ExactMin_Passes()
    {
        var ctx = MakeContext();
        // Level=1 (default)
        var cond = new LevelCondition(1);
        Assert.True(cond.Evaluate(ctx));
    }
}
