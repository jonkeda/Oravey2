namespace Oravey2.Core.UI;

/// <summary>
/// Manages 6 quick-use item slots. Each slot stores an item ID that the player
/// can activate. The Stride HUD renders these; this is the data/logic layer.
/// </summary>
public sealed class QuickSlotBar
{
    public const int SlotCount = 6;

    private readonly string?[] _slots = new string?[SlotCount];

    /// <summary>Gets the item ID assigned to a slot (0–5), or null if empty.</summary>
    public string? GetSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return null;
        return _slots[index];
    }

    /// <summary>Assigns an item ID to a slot. Pass null to clear.</summary>
    public void SetSlot(int index, string? itemId)
    {
        if (index < 0 || index >= SlotCount) return;
        _slots[index] = itemId;
    }

    /// <summary>Clears all slots.</summary>
    public void ClearAll()
    {
        for (int i = 0; i < SlotCount; i++)
            _slots[i] = null;
    }

    /// <summary>Finds the first slot containing the given item ID, or -1.</summary>
    public int FindSlot(string itemId)
    {
        for (int i = 0; i < SlotCount; i++)
            if (_slots[i] == itemId)
                return i;
        return -1;
    }

    /// <summary>Checks if any slot contains the given item ID.</summary>
    public bool Contains(string itemId)
        => FindSlot(itemId) >= 0;

    /// <summary>Returns a snapshot of all slots.</summary>
    public IReadOnlyList<string?> GetAllSlots()
        => _slots.ToArray();
}
