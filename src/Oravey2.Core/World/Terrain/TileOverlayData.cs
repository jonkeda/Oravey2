using System.Numerics;

namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Output of the tile overlay pipeline for a Hybrid chunk.
/// Contains floor quads and structure entries snapped to the heightmap surface.
/// </summary>
public sealed class TileOverlayData
{
    /// <summary>Floor decal quads projected onto the heightmap surface.</summary>
    public VertexData[] FloorVertices { get; }

    /// <summary>Triangle indices for the floor quads.</summary>
    public int[] FloorIndices { get; }

    /// <summary>Structure entries (walls, doors, props) placed on the heightmap.</summary>
    public IReadOnlyList<StructureEntry> Structures { get; }

    public TileOverlayData(
        VertexData[] floorVertices,
        int[] floorIndices,
        IReadOnlyList<StructureEntry> structures)
    {
        FloorVertices = floorVertices;
        FloorIndices = floorIndices;
        Structures = structures;
    }
}

/// <summary>
/// A placed structure instance (wall segment, door, or prop) within a Hybrid chunk.
/// </summary>
public readonly record struct StructureEntry(
    int StructureId,
    StructurePlacement Placement,
    Vector3 Position,
    float RotationY);

/// <summary>
/// Describes the placement type of a structure within a tile.
/// </summary>
public enum StructurePlacement : byte
{
    Prop = 0,
    WallNorth = 1,
    WallEast = 2,
    WallSouth = 3,
    WallWest = 4,
    DoorNorth = 5,
    DoorEast = 6,
    DoorSouth = 7,
    DoorWest = 8,
}
