using System.Numerics;
using Oravey2.Core.World.LinearFeatures;

namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Output of the terrain build pipeline for a single chunk.
/// Holds raw mesh data and splat maps ready for the Stride renderer to consume.
/// </summary>
public sealed class ChunkTerrainMesh : IDisposable
{
    /// <summary>Vertex positions, normals, and UVs for the heightmap mesh.</summary>
    public VertexData[] Vertices { get; }

    /// <summary>Triangle index buffer.</summary>
    public int[] Indices { get; }

    /// <summary>Splat map 0: R=Dirt, G=Asphalt, B=Concrete, A=Grass (32×32 RGBA).</summary>
    public byte[] SplatMap0 { get; }

    /// <summary>Splat map 1: R=Sand, G=Mud, B=Rock, A=Metal (32×32 RGBA).</summary>
    public byte[] SplatMap1 { get; }

    /// <summary>Terrain modifiers that were applied (kept for debugging / re-application).</summary>
    public IReadOnlyList<TerrainModifier> AppliedModifiers { get; }

    /// <summary>Ribbon meshes for linear features (roads, rails, rivers) in this chunk.</summary>
    public IReadOnlyList<RibbonMesh> LinearFeatureMeshes { get; }

    /// <summary>Tile overlay data for Hybrid chunks. Null for Heightmap-only chunks.</summary>
    public TileOverlayData? Overlay { get; }

    public ChunkTerrainMesh(
        VertexData[] vertices,
        int[] indices,
        byte[] splatMap0,
        byte[] splatMap1,
        IReadOnlyList<TerrainModifier>? appliedModifiers = null,
        IReadOnlyList<RibbonMesh>? linearFeatureMeshes = null,
        TileOverlayData? overlay = null)
    {
        Vertices = vertices;
        Indices = indices;
        SplatMap0 = splatMap0;
        SplatMap1 = splatMap1;
        AppliedModifiers = appliedModifiers ?? Array.Empty<TerrainModifier>();
        LinearFeatureMeshes = linearFeatureMeshes ?? Array.Empty<RibbonMesh>();
        Overlay = overlay;
    }

    public void Dispose()
    {
        // No unmanaged resources in the pure-data version.
        // Stride textures/meshes are created by the rendering layer and disposed there.
    }
}

/// <summary>
/// Per-vertex data produced by the heightmap mesh generator.
/// </summary>
public readonly record struct VertexData(Vector3 Position, Vector3 Normal, Vector2 TexCoord);
