namespace Oravey2.Core.Dialogue;

/// <summary>
/// Removes an item from the player's inventory and adds caps.
/// No-op if the player doesn't have the item.
/// </summary>
public sealed class SellItemAction : IConsequenceAction
{
    public string ItemId { get; }
    public int Price { get; }

    public SellItemAction(string itemId, int price)
    {
        ItemId = itemId;
        Price = price;
    }

    public void Execute(DialogueContext context)
    {
        if (!context.Inventory.Contains(ItemId)) return;

        if (context.Inventory.Remove(ItemId))
            context.Inventory.Caps += Price;
    }
}
