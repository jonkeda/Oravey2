using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Items;

namespace Oravey2.Core.UI.ViewModels;

/// <summary>
/// Snapshot of inventory state for the inventory screen.
/// </summary>
public sealed record InventoryItemView(
    string ItemId,
    string Name,
    string Description,
    ItemCategory Category,
    int StackCount,
    float Weight,
    int? CurrentDurability,
    int? MaxDurability
);

public sealed record InventoryViewModel(
    IReadOnlyList<InventoryItemView> Items,
    float CurrentWeight,
    float MaxCarryWeight,
    bool IsOverweight
)
{
    public static InventoryViewModel Create(InventoryComponent inventory)
    {
        var items = inventory.Items.Select(item => new InventoryItemView(
            ItemId: item.Definition.Id,
            Name: item.Definition.Name,
            Description: item.Definition.Description,
            Category: item.Definition.Category,
            StackCount: item.StackCount,
            Weight: item.TotalWeight,
            CurrentDurability: item.CurrentDurability,
            MaxDurability: item.Definition.Durability?.MaxDurability
        )).ToList();

        return new InventoryViewModel(
            Items: items,
            CurrentWeight: inventory.CurrentWeight,
            MaxCarryWeight: inventory.MaxCarryWeight,
            IsOverweight: inventory.IsOverweight
        );
    }
}
