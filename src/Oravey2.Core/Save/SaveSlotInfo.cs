namespace Oravey2.Core.Save;

/// <summary>
/// Summary for the load screen UI. One per save slot.
/// </summary>
public sealed record SaveSlotInfo(
    SaveSlot Slot,
    bool IsEmpty,
    SaveHeader? Header
);
