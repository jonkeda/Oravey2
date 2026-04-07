using Oravey2.Core.World.Terrain;

namespace Oravey2.Core.World.Liquids;

/// <summary>
/// Output of the liquid rendering pipeline for a single chunk.
/// Contains surface meshes and waterfall cascade meshes for each liquid region.
/// </summary>
public sealed class ChunkLiquidData
{
    public IReadOnlyList<LiquidMesh> SurfaceMeshes { get; }
    public IReadOnlyList<LiquidMesh> WaterfallMeshes { get; }

    public ChunkLiquidData(
        IReadOnlyList<LiquidMesh> surfaceMeshes,
        IReadOnlyList<LiquidMesh> waterfallMeshes)
    {
        SurfaceMeshes = surfaceMeshes;
        WaterfallMeshes = waterfallMeshes;
    }
}

/// <summary>
/// Orchestrates liquid rendering for a chunk:
/// 1. Find connected liquid regions by type
/// 2. Build flat surface meshes per region
/// 3. Detect waterfalls at cliff edges
/// 4. Group shore tiles for edge effects
/// </summary>
public static class LiquidRenderer
{
    /// <summary>
    /// Builds all liquid meshes for a chunk. Returns null if the chunk has no liquid tiles.
    /// </summary>
    public static ChunkLiquidData? Build(ChunkData chunk)
    {
        var tiles = chunk.Tiles;
        float tileWorldSize = HeightmapMeshGenerator.TileWorldSize;

        // Phase 1: find all liquid regions
        var regions = LiquidRegionFinder.FindRegions(tiles);
        if (regions.Count == 0)
            return null;

        var surfaceMeshes = new List<LiquidMesh>();
        var waterfallMeshes = new List<LiquidMesh>();

        foreach (var region in regions)
        {
            // Phase 2: build flat surface mesh
            var surface = LiquidMeshBuilder.BuildSurface(region, tileWorldSize);
            if (surface.Vertices.Length > 0)
                surfaceMeshes.Add(surface);

            // Phase 3: detect and build waterfall cascades
            var cascades = LiquidMeshBuilder.BuildWaterfalls(region, tiles, tileWorldSize);
            waterfallMeshes.AddRange(cascades);
        }

        if (surfaceMeshes.Count == 0 && waterfallMeshes.Count == 0)
            return null;

        return new ChunkLiquidData(surfaceMeshes, waterfallMeshes);
    }
}
