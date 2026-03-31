using Oravey2.Core.Inventory.Items;

namespace Oravey2.Core.Dialogue;

/// <summary>
/// Adds an item to the player's inventory (e.g. quest reward).
/// </summary>
public sealed class GiveItemAction : IConsequenceAction
{
    public string ItemId { get; }
    public int Count { get; }

    public GiveItemAction(string itemId, int count = 1)
    {
        ItemId = itemId;
        Count = count;
    }

    public void Execute(DialogueContext context)
    {
        context.Inventory.Add(new ItemInstance(ItemResolver.Resolve(ItemId), Count));
    }
}
