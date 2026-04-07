namespace Oravey2.Core.World;

[Flags]
public enum TileFlags : ushort
{
    None         = 0,
    Walkable     = 1 << 0,
    Irradiated   = 1 << 1,
    Burnable     = 1 << 2,
    Destructible = 1 << 3,
    Forested     = 1 << 4,
    Interior     = 1 << 5,
    FastTravel   = 1 << 6,
    Searchable   = 1 << 7,
}
