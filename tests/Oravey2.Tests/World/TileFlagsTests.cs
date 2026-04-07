using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class TileFlagsTests
{
    [Fact]
    public void TileFlags_Combine_Correctly()
    {
        var flags = TileFlags.Walkable | TileFlags.Irradiated;
        Assert.True(flags.HasFlag(TileFlags.Walkable));
        Assert.True(flags.HasFlag(TileFlags.Irradiated));
        Assert.False(flags.HasFlag(TileFlags.Burnable));
    }

    [Fact]
    public void TileFlags_HasFlag_Works()
    {
        var flags = TileFlags.Walkable | TileFlags.Destructible;
        Assert.True(flags.HasFlag(TileFlags.Walkable));
        Assert.True(flags.HasFlag(TileFlags.Destructible));
        Assert.False(flags.HasFlag(TileFlags.Irradiated));
        Assert.False(flags.HasFlag(TileFlags.Burnable));
    }

    [Fact]
    public void TileFlags_None_HasNoFlags()
    {
        Assert.False(TileFlags.None.HasFlag(TileFlags.Walkable));
        Assert.False(TileFlags.None.HasFlag(TileFlags.Irradiated));
    }

    [Fact]
    public void TileFlags_NewFlags_HaveCorrectValues()
    {
        Assert.Equal((TileFlags)16, TileFlags.Forested);
        Assert.Equal((TileFlags)32, TileFlags.Interior);
        Assert.Equal((TileFlags)64, TileFlags.FastTravel);
        Assert.Equal((TileFlags)128, TileFlags.Searchable);
    }

    [Fact]
    public void TileFlags_CombinedFlags_WorkWithBitwiseOr()
    {
        var flags = TileFlags.Walkable | TileFlags.Forested | TileFlags.Searchable;
        Assert.True(flags.HasFlag(TileFlags.Walkable));
        Assert.True(flags.HasFlag(TileFlags.Forested));
        Assert.True(flags.HasFlag(TileFlags.Searchable));
        Assert.False(flags.HasFlag(TileFlags.Interior));
        Assert.False(flags.HasFlag(TileFlags.Irradiated));
    }
}
