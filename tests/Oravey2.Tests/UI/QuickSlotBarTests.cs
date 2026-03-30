using Oravey2.Core.UI;

namespace Oravey2.Tests.UI;

public class QuickSlotBarTests
{
    [Fact]
    public void Default_AllSlotsNull()
    {
        var bar = new QuickSlotBar();
        for (int i = 0; i < QuickSlotBar.SlotCount; i++)
            Assert.Null(bar.GetSlot(i));
    }

    [Fact]
    public void SetSlot_GetSlot_RoundTrip()
    {
        var bar = new QuickSlotBar();
        bar.SetSlot(0, "stimpak");
        Assert.Equal("stimpak", bar.GetSlot(0));
    }

    [Fact]
    public void SetSlot_OutOfRange_Ignored()
    {
        var bar = new QuickSlotBar();
        bar.SetSlot(10, "x");
        Assert.Null(bar.GetSlot(10));
    }

    [Fact]
    public void SetSlot_Null_ClearsSlot()
    {
        var bar = new QuickSlotBar();
        bar.SetSlot(0, "stimpak");
        bar.SetSlot(0, null);
        Assert.Null(bar.GetSlot(0));
    }

    [Fact]
    public void ClearAll_ResetsAllSlots()
    {
        var bar = new QuickSlotBar();
        bar.SetSlot(0, "a");
        bar.SetSlot(1, "b");
        bar.SetSlot(2, "c");
        bar.ClearAll();
        for (int i = 0; i < QuickSlotBar.SlotCount; i++)
            Assert.Null(bar.GetSlot(i));
    }

    [Fact]
    public void FindSlot_Found()
    {
        var bar = new QuickSlotBar();
        bar.SetSlot(3, "stimpak");
        Assert.Equal(3, bar.FindSlot("stimpak"));
    }

    [Fact]
    public void FindSlot_NotFound_MinusOne()
    {
        var bar = new QuickSlotBar();
        Assert.Equal(-1, bar.FindSlot("nope"));
    }

    [Fact]
    public void Contains_TrueWhenAssigned()
    {
        var bar = new QuickSlotBar();
        bar.SetSlot(0, "x");
        Assert.True(bar.Contains("x"));
    }
}
