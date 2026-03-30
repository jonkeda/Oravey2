namespace Oravey2.Core.Crafting;

using Oravey2.Core.Character.Skills;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;

public sealed class CraftingProcessor
{
    private readonly IEventBus _eventBus;

    public CraftingProcessor(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public bool CanCraft(
        InventoryComponent inventory,
        SkillsComponent skills,
        RecipeDefinition recipe,
        StationType currentStation)
    {
        if (recipe.RequiredStation != currentStation)
            return false;

        if (recipe.RequiredSkill.HasValue &&
            skills.GetEffective(recipe.RequiredSkill.Value) < recipe.SkillThreshold)
            return false;

        foreach (var (itemId, count) in recipe.Ingredients)
        {
            if (!inventory.Contains(itemId, count))
                return false;
        }

        return true;
    }

    public bool Craft(
        InventoryComponent inventory,
        SkillsComponent skills,
        RecipeDefinition recipe,
        StationType currentStation,
        Func<string, int, ItemInstance> createItem)
    {
        if (!CanCraft(inventory, skills, recipe, currentStation))
            return false;

        foreach (var (itemId, count) in recipe.Ingredients)
            inventory.Remove(itemId, count);

        var output = createItem(recipe.OutputItemId, recipe.OutputCount);
        inventory.Add(output);

        _eventBus.Publish(new ItemCraftedEvent(recipe.Id, recipe.OutputItemId, recipe.OutputCount));

        return true;
    }
}
