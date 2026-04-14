using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Dialogue;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.NPC;
using Oravey2.Core.World;

namespace Oravey2.Tests.Dialogue;

public class TradeActionTests
{
    private static (DialogueProcessor proc, DialogueContext ctx, EventBus bus) Setup()
    {
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);
        var inv = new InventoryComponent(stats);
        var world = new WorldStateService();
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        var ctx = new DialogueContext(skills, inv, world, level, bus);
        var proc = new DialogueProcessor(bus);
        return (proc, ctx, bus);
    }

    // --- BuyItemAction ---

    [Fact]
    public void BuyItemAction_DeductsCaps_AddsItem()
    {
        var (_, ctx, _) = Setup();
        ctx.Inventory.Caps = 50;

        var action = new BuyItemAction("medkit", 10);
        action.Execute(ctx);

        Assert.Equal(40, ctx.Inventory.Caps);
        Assert.True(ctx.Inventory.Contains("medkit"));
    }

    [Fact]
    public void BuyItemAction_InsufficientCaps_NoChange()
    {
        var (_, ctx, _) = Setup();
        ctx.Inventory.Caps = 5;

        var action = new BuyItemAction("medkit", 10);
        action.Execute(ctx);

        Assert.Equal(5, ctx.Inventory.Caps);
        Assert.False(ctx.Inventory.Contains("medkit"));
    }

    [Fact]
    public void BuyItemAction_ExactCaps_Works()
    {
        var (_, ctx, _) = Setup();
        ctx.Inventory.Caps = 10;

        var action = new BuyItemAction("medkit", 10);
        action.Execute(ctx);

        Assert.Equal(0, ctx.Inventory.Caps);
        Assert.True(ctx.Inventory.Contains("medkit"));
    }

    [Fact]
    public void BuyItemAction_ZeroCaps_NoNegative()
    {
        var (_, ctx, _) = Setup();
        ctx.Inventory.Caps = 0;

        var action = new BuyItemAction("medkit", 10);
        action.Execute(ctx);

        Assert.Equal(0, ctx.Inventory.Caps);
        Assert.False(ctx.Inventory.Contains("medkit"));
    }

    // --- SellItemAction ---

    [Fact]
    public void SellItemAction_RemovesItem_AddsCaps()
    {
        var (_, ctx, _) = Setup();
        ctx.Inventory.Caps = 10;
        ctx.Inventory.Add(new ItemInstance(M0Items.ScrapMetal()));

        var action = new SellItemAction("scrap_metal", 5);
        action.Execute(ctx);

        Assert.Equal(15, ctx.Inventory.Caps);
        Assert.False(ctx.Inventory.Contains("scrap_metal"));
    }

    [Fact]
    public void SellItemAction_NoItem_NoChange()
    {
        var (_, ctx, _) = Setup();
        ctx.Inventory.Caps = 10;

        var action = new SellItemAction("scrap_metal", 5);
        action.Execute(ctx);

        Assert.Equal(10, ctx.Inventory.Caps);
    }

    [Fact]
    public void SellItemAction_StackOf3_SellOne_StackOf2()
    {
        var (_, ctx, _) = Setup();
        ctx.Inventory.Caps = 10;
        ctx.Inventory.Add(new ItemInstance(M0Items.ScrapMetal(), 3));

        var action = new SellItemAction("scrap_metal", 5);
        action.Execute(ctx);

        Assert.Equal(15, ctx.Inventory.Caps);
        Assert.True(ctx.Inventory.Contains("scrap_metal", 2));
        Assert.False(ctx.Inventory.Contains("scrap_metal", 3));
    }

    // --- GiveItemAction ---

    [Fact]
    public void GiveItemAction_AddsItem()
    {
        var (_, ctx, _) = Setup();

        var action = new GiveItemAction("medkit", 2);
        action.Execute(ctx);

        Assert.True(ctx.Inventory.Contains("medkit", 2));
    }

    [Fact]
    public void GiveItemAction_StacksWithExisting()
    {
        var (_, ctx, _) = Setup();
        ctx.Inventory.Add(new ItemInstance(M0Items.Medkit(), 1));

        var action = new GiveItemAction("medkit", 2);
        action.Execute(ctx);

        Assert.True(ctx.Inventory.Contains("medkit", 3));
    }

    // --- Via DialogueProcessor ---

    [Fact]
    public void BuyItemAction_ViaDialogueProcessor()
    {
        var (proc, ctx, _) = Setup();
        ctx.Inventory.Caps = 50;

        var tree = TownDialogueTrees.MerchantDialogue();
        proc.StartDialogue(tree);

        // Choice 0 = "Buy Medkit (10 caps)"
        proc.SelectChoice(0, ctx);

        Assert.Equal(40, ctx.Inventory.Caps);
        Assert.True(ctx.Inventory.Contains("medkit"));
    }

    [Fact]
    public void SellItemAction_ViaDialogueProcessor()
    {
        var (proc, ctx, _) = Setup();
        ctx.Inventory.Caps = 50;
        ctx.Inventory.Add(new ItemInstance(M0Items.ScrapMetal()));

        var tree = TownDialogueTrees.MerchantDialogue();
        proc.StartDialogue(tree);

        // Choice 2 = "Sell Scrap Metal (5 caps)"
        proc.SelectChoice(2, ctx);

        Assert.Equal(55, ctx.Inventory.Caps);
        Assert.False(ctx.Inventory.Contains("scrap_metal"));
    }

    [Fact]
    public void GiveItemAction_ViaDialogueProcessor()
    {
        var (proc, ctx, _) = Setup();

        // Use a custom tree with GiveItemAction as consequence
        var nodes = new Dictionary<string, DialogueNode>
        {
            ["start"] = new DialogueNode { Id = "start", Speaker = "Questgiver", Text = "Here's your reward!", Choices =
            [
                new DialogueChoice { Text = "Take reward", Consequences = [new GiveItemAction("medkit", 2)] },
            ] },
        };
        var tree = new DialogueTree { Id = "reward_tree", StartNodeId = "start", Nodes = nodes };
        proc.StartDialogue(tree);

        proc.SelectChoice(0, ctx);

        Assert.True(ctx.Inventory.Contains("medkit", 2));
    }
}
