namespace Oravey2.Core.World;

[Flags]
public enum CoverEdges : byte
{
    None  = 0,
    North = 1,
    East  = 2,
    South = 4,
    West  = 8,
}
