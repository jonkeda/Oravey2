using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Crafting;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;

namespace Oravey2.Tests.Crafting;

public class CraftingProcessorTests
{
    private static ItemDefinition MakeItem(string id, bool stackable = true, int maxStack = 99)
        => new(id, id, "", ItemCategory.CraftingMaterial, 0.5f, stackable, 5, MaxStack: maxStack);

    private static (CraftingProcessor proc, InventoryComponent inv, SkillsComponent skills, EventBus bus) Setup()
    {
        var stats = new StatsComponent();
        var inv = new InventoryComponent(stats);
        var skills = new SkillsComponent(stats);
        var bus = new EventBus();
        var proc = new CraftingProcessor(bus);
        return (proc, inv, skills, bus);
    }

    private static RecipeDefinition SimpleRecipe(
        SkillType? skill = null, int skillThreshold = 0,
        StationType station = StationType.Workbench)
    {
        return new RecipeDefinition(
            "craft_stimpak", "Craft Stimpak", "stimpak", 1,
            new Dictionary<string, int> { { "antiseptic", 1 }, { "syringe", 1 } },
            station, skill, skillThreshold);
    }

    private ItemInstance CreateItem(string id, int count) =>
        new(MakeItem(id), stackCount: count);

    [Fact]
    public void CanCraft_AllIngredients_SkillMet_True()
    {
        var (proc, inv, skills, _) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        inv.Add(new ItemInstance(MakeItem("syringe")));

        var recipe = SimpleRecipe(SkillType.Science, 10);
        Assert.True(proc.CanCraft(inv, skills, recipe, StationType.Workbench));
    }

    [Fact]
    public void CanCraft_MissingIngredient_False()
    {
        var (proc, inv, skills, _) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        // missing syringe

        Assert.False(proc.CanCraft(inv, skills, SimpleRecipe(), StationType.Workbench));
    }

    [Fact]
    public void CanCraft_InsufficientIngredientCount_False()
    {
        var (proc, inv, skills, _) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        inv.Add(new ItemInstance(MakeItem("syringe")));

        var recipe = new RecipeDefinition(
            "r", "R", "out", 1,
            new Dictionary<string, int> { { "antiseptic", 5 } },
            StationType.Workbench);

        Assert.False(proc.CanCraft(inv, skills, recipe, StationType.Workbench));
    }

    [Fact]
    public void CanCraft_SkillBelowThreshold_False()
    {
        var (proc, inv, skills, _) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        inv.Add(new ItemInstance(MakeItem("syringe")));

        // Default Intelligence=5 → Science base = 10 + 5*2 = 20
        var recipe = SimpleRecipe(SkillType.Science, 50);
        Assert.False(proc.CanCraft(inv, skills, recipe, StationType.Workbench));
    }

    [Fact]
    public void CanCraft_NoSkillRequired_SkipsCheck()
    {
        var (proc, inv, skills, _) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        inv.Add(new ItemInstance(MakeItem("syringe")));

        var recipe = SimpleRecipe(skill: null);
        Assert.True(proc.CanCraft(inv, skills, recipe, StationType.Workbench));
    }

    [Fact]
    public void CanCraft_WrongStation_False()
    {
        var (proc, inv, skills, _) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        inv.Add(new ItemInstance(MakeItem("syringe")));

        var recipe = SimpleRecipe(station: StationType.CookingFire);
        Assert.False(proc.CanCraft(inv, skills, recipe, StationType.Workbench));
    }

    [Fact]
    public void Craft_ConsumesIngredients()
    {
        var (proc, inv, skills, _) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        inv.Add(new ItemInstance(MakeItem("syringe")));

        proc.Craft(inv, skills, SimpleRecipe(), StationType.Workbench, CreateItem);

        Assert.False(inv.Contains("antiseptic"));
        Assert.False(inv.Contains("syringe"));
    }

    [Fact]
    public void Craft_AddsOutput()
    {
        var (proc, inv, skills, _) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        inv.Add(new ItemInstance(MakeItem("syringe")));

        proc.Craft(inv, skills, SimpleRecipe(), StationType.Workbench, CreateItem);

        Assert.True(inv.Contains("stimpak"));
    }

    [Fact]
    public void Craft_PublishesItemCraftedEvent()
    {
        var (proc, inv, skills, bus) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        inv.Add(new ItemInstance(MakeItem("syringe")));

        ItemCraftedEvent? received = null;
        bus.Subscribe<ItemCraftedEvent>(e => received = e);

        proc.Craft(inv, skills, SimpleRecipe(), StationType.Workbench, CreateItem);

        Assert.NotNull(received);
        Assert.Equal("craft_stimpak", received.Value.RecipeId);
        Assert.Equal("stimpak", received.Value.OutputItemId);
    }

    [Fact]
    public void Craft_ReturnsFalse_WhenCannotCraft()
    {
        var (proc, inv, skills, _) = Setup();
        // no ingredients
        Assert.False(proc.Craft(inv, skills, SimpleRecipe(), StationType.Workbench, CreateItem));
    }

    [Fact]
    public void Craft_MultipleOutput()
    {
        var (proc, inv, skills, bus) = Setup();
        inv.Add(new ItemInstance(MakeItem("antiseptic")));
        inv.Add(new ItemInstance(MakeItem("syringe")));

        var recipe = new RecipeDefinition(
            "r", "R", "ammo_9mm", 10,
            new Dictionary<string, int> { { "antiseptic", 1 }, { "syringe", 1 } },
            StationType.Workbench);

        ItemCraftedEvent? received = null;
        bus.Subscribe<ItemCraftedEvent>(e => received = e);

        proc.Craft(inv, skills, recipe, StationType.Workbench, CreateItem);

        Assert.Equal(10, received!.Value.Count);
    }
}
