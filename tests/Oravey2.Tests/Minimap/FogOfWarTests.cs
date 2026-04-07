using Oravey2.Core.UI;

namespace Oravey2.Tests.Minimap;

public class FogOfWarTests
{
    [Fact]
    public void NewRegion_FullyDark()
    {
        var mask = new FogOfWarMask(32, 32);
        Assert.Equal(32 * 32, mask.CountCells(FogState.Unknown));
        Assert.Equal(0, mask.CountCells(FogState.Visible));
        Assert.Equal(0, mask.CountCells(FogState.Visited));
    }

    [Fact]
    public void RevealRadius_MarksExplored()
    {
        var mask = new FogOfWarMask(32, 32);
        mask.Reveal(16, 16, 5);

        int visible = mask.CountCells(FogState.Visible);
        Assert.True(visible > 0, "Reveal should make some cells Visible");
        Assert.Equal(FogState.Visible, mask[16, 16]); // centre is visible
    }

    [Fact]
    public void VisitedButNotVisible_IsDimmed()
    {
        var mask = new FogOfWarMask(32, 32);

        // First reveal at (10, 10)
        mask.Reveal(10, 10, 3);
        Assert.Equal(FogState.Visible, mask[10, 10]);

        // Reveal at a different location — old area should become Visited
        mask.Reveal(25, 25, 3);
        Assert.Equal(FogState.Visited, mask[10, 10]);
        Assert.Equal(FogState.Visible, mask[25, 25]);
    }

    [Fact]
    public void FogMask_SerializesToBitmap()
    {
        var original = new FogOfWarMask(16, 16);
        original.Reveal(8, 8, 4);

        byte[] bytes = original.ToBytes();
        var restored = FogOfWarMask.FromBytes(bytes);

        Assert.Equal(original.Width, restored.Width);
        Assert.Equal(original.Height, restored.Height);

        // Compare all cells
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                Assert.Equal(original[x, y], restored[x, y]);
    }

    [Fact]
    public void FogMask_Merge_CombinesTwoRegions()
    {
        var maskA = new FogOfWarMask(16, 16);
        maskA.Reveal(4, 4, 2);

        var maskB = new FogOfWarMask(16, 16);
        maskB.Reveal(12, 12, 2);

        // Both should have different visible areas
        Assert.Equal(FogState.Unknown, maskA[12, 12]);
        Assert.Equal(FogState.Unknown, maskB[4, 4]);

        maskA.Merge(maskB);

        // After merge, both areas should be revealed
        Assert.True(maskA[4, 4] >= FogState.Visited);
        Assert.True(maskA[12, 12] >= FogState.Visited);
    }

    [Fact]
    public void OutOfBounds_ReturnsUnknown()
    {
        var mask = new FogOfWarMask(8, 8);
        Assert.Equal(FogState.Unknown, mask[-1, 0]);
        Assert.Equal(FogState.Unknown, mask[0, -1]);
        Assert.Equal(FogState.Unknown, mask[8, 0]);
        Assert.Equal(FogState.Unknown, mask[0, 8]);
    }

    [Fact]
    public void RevealAtEdge_DoesNotThrow()
    {
        var mask = new FogOfWarMask(16, 16);
        // Reveal near corner — radius extends outside bounds
        mask.Reveal(0, 0, 5);
        Assert.Equal(FogState.Visible, mask[0, 0]);
        Assert.Equal(FogState.Visible, mask[3, 3]);
    }
}
