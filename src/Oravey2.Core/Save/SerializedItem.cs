namespace Oravey2.Core.Save;

/// <summary>
/// Lightweight representation of an inventory item for serialized save data.
/// </summary>
public sealed class SerializedItem
{
    public string ItemId { get; set; } = "";
    public int StackCount { get; set; }
    public int? CurrentDurability { get; set; }
}
