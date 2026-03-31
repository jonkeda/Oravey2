using Oravey2.Core.Inventory.Items;

namespace Oravey2.Core.Dialogue;

/// <summary>
/// Maps item IDs to ItemDefinitions. Decouples trade actions from static factory methods.
/// </summary>
public static class ItemResolver
{
    private static readonly Dictionary<string, Func<ItemDefinition>> Registry = new()
    {
        ["pipe_wrench"] = M0Items.PipeWrench,
        ["medkit"] = M0Items.Medkit,
        ["scrap_metal"] = M0Items.ScrapMetal,
        ["leather_jacket"] = M0Items.LeatherJacket,
        ["rusty_shiv"] = M0Items.RustyShiv,
    };

    public static ItemDefinition Resolve(string itemId)
    {
        if (Registry.TryGetValue(itemId, out var factory))
            return factory();

        throw new KeyNotFoundException($"Unknown item ID: {itemId}");
    }
}
