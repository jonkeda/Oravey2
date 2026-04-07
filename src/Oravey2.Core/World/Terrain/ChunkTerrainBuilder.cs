using Oravey2.Core.World.LinearFeatures;
using Oravey2.Core.World.Rendering;

namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Orchestrates the terrain build pipeline for a single chunk:
/// height sampling → modifier application → normal calculation → splat build → linear features.
/// </summary>
public static class ChunkTerrainBuilder
{
    /// <summary>
    /// Builds a complete terrain mesh from chunk data.
    /// </summary>
    public static ChunkTerrainMesh Build(
        ChunkData chunk,
        IChunkNeighborProvider? neighbors = null,
        QualityPreset quality = QualityPreset.Medium)
    {
        var tiles = chunk.Tiles.TileDataGrid;
        int tilesPerSide = ChunkData.Size;
        int subdivision = quality switch
        {
            QualityPreset.Low => 1,
            QualityPreset.Medium => 2,
            QualityPreset.High => 4,
            _ => 1
        };
        int vertsPerSide = tilesPerSide * subdivision + 1;
        float chunkWorldSize = tilesPerSide * HeightmapMeshGenerator.TileWorldSize;

        // Phase 1: sample base heights
        var heights = HeightmapMeshGenerator.SampleHeights(
            tiles, neighbors, vertsPerSide, tilesPerSide, subdivision);

        // Phase 2: apply terrain modifiers
        if (chunk.TerrainModifiers.Count > 0)
        {
            TerrainModifierApplicator.Apply(
                heights, vertsPerSide, chunkWorldSize, chunk.TerrainModifiers);
        }

        // Phase 3: build vertices (positions + normals + UVs)
        var vertices = HeightmapMeshGenerator.BuildVertices(heights, vertsPerSide, tilesPerSide);

        // Phase 4: build index buffer
        var indices = HeightmapMeshGenerator.BuildIndices(vertsPerSide);

        // Phase 5: build splat maps
        var (splat0, splat1) = TerrainSplatBuilder.Build(tiles);

        // Phase 6: build linear feature ribbon meshes
        var ribbonMeshes = BuildLinearFeatures(
            chunk.LinearFeatures, heights, vertsPerSide, chunkWorldSize);

        // Phase 7: build tile overlay for Hybrid chunks (floor quads + structures)
        TileOverlayData? overlay = null;
        if (chunk.Mode == ChunkMode.Hybrid)
        {
            overlay = TileOverlayBuilder.Build(chunk, heights, vertsPerSide, chunkWorldSize, neighbors);
        }

        return new ChunkTerrainMesh(vertices, indices, splat0, splat1, chunk.TerrainModifiers, ribbonMeshes, overlay);
    }

    private static List<RibbonMesh> BuildLinearFeatures(
        IReadOnlyList<LinearFeature> features,
        float[,] heights,
        int vertsPerSide,
        float chunkWorldSize)
    {
        if (features.Count == 0)
            return [];

        var meshes = new List<RibbonMesh>(features.Count);

        foreach (var feature in features)
        {
            // Clip feature to chunk bounds
            var clipped = SplineClipper.Clip(feature, 0, 0, chunkWorldSize, chunkWorldSize);
            if (clipped == null) continue;

            var ribbon = RibbonMeshBuilder.Build(clipped, heights, vertsPerSide, chunkWorldSize);
            if (ribbon.Vertices.Length > 0)
                meshes.Add(ribbon);
        }

        return meshes;
    }
}
