namespace Oravey2.Core.Save;

/// <summary>
/// Summary for the load screen UI. One per save slot.
/// </summary>
public sealed class SaveSlotInfo
{
    public SaveSlot Slot { get; set; }
    public bool IsEmpty { get; set; }
    public SaveHeader? Header { get; set; }
}
