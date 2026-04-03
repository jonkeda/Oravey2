namespace Oravey2.Core.World;

[Flags]
public enum TileFlags : byte
{
    None         = 0,
    Walkable     = 1 << 0,
    Irradiated   = 1 << 1,
    Burnable     = 1 << 2,
    Destructible = 1 << 3,
}
