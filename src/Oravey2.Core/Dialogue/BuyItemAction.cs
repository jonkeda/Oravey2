using Oravey2.Core.Inventory.Items;

namespace Oravey2.Core.Dialogue;

/// <summary>
/// Deducts caps and adds an item to the player's inventory.
/// No-op if the player can't afford it.
/// </summary>
public sealed class BuyItemAction : IConsequenceAction
{
    public string ItemId { get; }
    public int Cost { get; }

    public BuyItemAction(string itemId, int cost)
    {
        ItemId = itemId;
        Cost = cost;
    }

    public void Execute(DialogueContext context)
    {
        if (context.Inventory.Caps < Cost) return;

        context.Inventory.Caps -= Cost;
        context.Inventory.Add(new ItemInstance(ItemResolver.Resolve(ItemId)));
    }
}
