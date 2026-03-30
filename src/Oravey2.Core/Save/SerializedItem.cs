namespace Oravey2.Core.Save;

/// <summary>
/// Lightweight representation of an inventory item for serialized save data.
/// </summary>
public sealed record SerializedItem(
    string ItemId,
    int StackCount,
    int? CurrentDurability
);
