using System.Numerics;

namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Builds tile-resolution floor quads and structure entries for Hybrid-mode chunks.
/// Floor quads are projected onto the heightmap surface. Structure meshes are
/// snapped to the terrain via <see cref="HeightmapMeshGenerator.GetSurfaceHeight"/>.
/// </summary>
public static class TileOverlayBuilder
{
    /// <summary>
    /// Small Y offset above terrain to prevent z-fighting between overlay and heightmap.
    /// </summary>
    public const float OverlayOffset = 0.02f;

    /// <summary>
    /// Number of tiles at each Heightmap↔Hybrid boundary that fade from full overlay to none.
    /// </summary>
    public const int TransitionMargin = 2;

    /// <summary>
    /// Builds overlay data for a Hybrid chunk.
    /// Returns null for non-Hybrid chunks.
    /// </summary>
    public static TileOverlayData? Build(
        ChunkData chunk,
        float[,] heights,
        int vertsPerSide,
        float chunkWorldSize,
        IChunkNeighborProvider? neighbors = null)
    {
        if (chunk.Mode != ChunkMode.Hybrid)
            return null;

        var tiles = chunk.Tiles.TileDataGrid;
        int tilesPerSide = ChunkData.Size;
        float tileWorld = HeightmapMeshGenerator.TileWorldSize;

        var floorVerts = new List<VertexData>();
        var floorIndices = new List<int>();
        var structures = new List<StructureEntry>();

        for (int ty = 0; ty < tilesPerSide; ty++)
        {
            for (int tx = 0; tx < tilesPerSide; tx++)
            {
                var tile = tiles[tx, ty];

                // Skip pure natural terrain with no structures
                bool hasStructure = tile.StructureId != 0;
                bool isArtificialSurface = tile.Surface is SurfaceType.Asphalt
                    or SurfaceType.Concrete or SurfaceType.Metal;

                if (!hasStructure && !isArtificialSurface)
                    continue;

                // Compute transition opacity (1.0 = full, 0.0 = invisible)
                float opacity = ComputeTransitionOpacity(tx, ty, tilesPerSide, chunk, neighbors);
                if (opacity <= 0f)
                    continue;

                // Floor quad corners in chunk-local world space
                float x0 = tx * tileWorld;
                float z0 = ty * tileWorld;
                float x1 = x0 + tileWorld;
                float z1 = z0 + tileWorld;

                // Sample heightmap at quad corners and snap to surface
                float y00 = HeightmapMeshGenerator.GetSurfaceHeight(new Vector2(x0, z0), heights, vertsPerSide, chunkWorldSize) + OverlayOffset;
                float y10 = HeightmapMeshGenerator.GetSurfaceHeight(new Vector2(x1, z0), heights, vertsPerSide, chunkWorldSize) + OverlayOffset;
                float y01 = HeightmapMeshGenerator.GetSurfaceHeight(new Vector2(x0, z1), heights, vertsPerSide, chunkWorldSize) + OverlayOffset;
                float y11 = HeightmapMeshGenerator.GetSurfaceHeight(new Vector2(x1, z1), heights, vertsPerSide, chunkWorldSize) + OverlayOffset;

                // Normal: average of triangle normals (good enough for a flat-ish decal)
                var normal = Vector3.Normalize(new Vector3(0, 1, 0));

                // UV encodes opacity in the V channel for transition fading
                int baseIdx = floorVerts.Count;
                floorVerts.Add(new VertexData(new Vector3(x0, y00, z0), normal, new Vector2(0, opacity)));
                floorVerts.Add(new VertexData(new Vector3(x1, y10, z0), normal, new Vector2(1, opacity)));
                floorVerts.Add(new VertexData(new Vector3(x0, y01, z1), normal, new Vector2(0, opacity)));
                floorVerts.Add(new VertexData(new Vector3(x1, y11, z1), normal, new Vector2(1, opacity)));

                // Two triangles (CW winding matching terrain pipeline)
                floorIndices.Add(baseIdx);
                floorIndices.Add(baseIdx + 2);
                floorIndices.Add(baseIdx + 1);
                floorIndices.Add(baseIdx + 1);
                floorIndices.Add(baseIdx + 2);
                floorIndices.Add(baseIdx + 3);

                // Structure placement
                if (hasStructure)
                {
                    float cx = x0 + tileWorld * 0.5f;
                    float cz = z0 + tileWorld * 0.5f;
                    float cy = HeightmapMeshGenerator.GetSurfaceHeight(new Vector2(cx, cz), heights, vertsPerSide, chunkWorldSize) + OverlayOffset;

                    // Place walls at tile edges where cover exists, otherwise place as prop at centre.
                    // All Y values include OverlayOffset so structures sit on the visible overlay floor.
                    bool placedWall = false;

                    if (tile.FullCover.HasFlag(CoverEdges.North) || tile.HalfCover.HasFlag(CoverEdges.North))
                    {
                        float ey = HeightmapMeshGenerator.GetSurfaceHeight(new Vector2(cx, z0), heights, vertsPerSide, chunkWorldSize) + OverlayOffset;
                        structures.Add(new StructureEntry(tile.StructureId, StructurePlacement.WallNorth, new Vector3(cx, ey, z0), 0f));
                        placedWall = true;
                    }
                    if (tile.FullCover.HasFlag(CoverEdges.East) || tile.HalfCover.HasFlag(CoverEdges.East))
                    {
                        float ey = HeightmapMeshGenerator.GetSurfaceHeight(new Vector2(x1, cz), heights, vertsPerSide, chunkWorldSize) + OverlayOffset;
                        structures.Add(new StructureEntry(tile.StructureId, StructurePlacement.WallEast, new Vector3(x1, ey, cz), MathF.PI * 0.5f));
                        placedWall = true;
                    }
                    if (tile.FullCover.HasFlag(CoverEdges.South) || tile.HalfCover.HasFlag(CoverEdges.South))
                    {
                        float ey = HeightmapMeshGenerator.GetSurfaceHeight(new Vector2(cx, z1), heights, vertsPerSide, chunkWorldSize) + OverlayOffset;
                        structures.Add(new StructureEntry(tile.StructureId, StructurePlacement.WallSouth, new Vector3(cx, ey, z1), MathF.PI));
                        placedWall = true;
                    }
                    if (tile.FullCover.HasFlag(CoverEdges.West) || tile.HalfCover.HasFlag(CoverEdges.West))
                    {
                        float ey = HeightmapMeshGenerator.GetSurfaceHeight(new Vector2(x0, cz), heights, vertsPerSide, chunkWorldSize) + OverlayOffset;
                        structures.Add(new StructureEntry(tile.StructureId, StructurePlacement.WallWest, new Vector3(x0, ey, cz), MathF.PI * 1.5f));
                        placedWall = true;
                    }

                    // If no wall edges, place as a centred prop
                    if (!placedWall)
                    {
                        structures.Add(new StructureEntry(tile.StructureId, StructurePlacement.Prop, new Vector3(cx, cy, cz), 0f));
                    }
                }
            }
        }

        return new TileOverlayData(
            floorVerts.ToArray(),
            floorIndices.ToArray(),
            structures);
    }

    /// <summary>
    /// Computes the overlay opacity for a tile based on its distance from a Heightmap-mode boundary.
    /// Tiles at the chunk edge adjacent to a Heightmap chunk fade from 100% to 0% over TransitionMargin tiles.
    /// </summary>
    private static float ComputeTransitionOpacity(
        int tx, int ty, int tilesPerSide,
        ChunkData chunk,
        IChunkNeighborProvider? neighbors)
    {
        // If no neighbor data, assume full opacity (no transition needed)
        if (neighbors == null)
            return 1f;

        float minOpacity = 1f;

        // Check each edge: if the adjacent chunk is Heightmap, fade near that edge
        // North edge (ty == 0)
        if (ty < TransitionMargin)
        {
            var neighborTile = neighbors.GetTileAt(tx, -1);
            // If neighbor is default (height 0, no flags), it may be Heightmap — apply fade
            if (neighborTile.Equals(TileData.Empty))
            {
                float t = (float)(ty + 1) / (TransitionMargin + 1);
                minOpacity = MathF.Min(minOpacity, t);
            }
        }

        // South edge
        if (ty >= tilesPerSide - TransitionMargin)
        {
            var neighborTile = neighbors.GetTileAt(tx, tilesPerSide);
            if (neighborTile.Equals(TileData.Empty))
            {
                float t = (float)(tilesPerSide - ty) / (TransitionMargin + 1);
                minOpacity = MathF.Min(minOpacity, t);
            }
        }

        // West edge (tx == 0)
        if (tx < TransitionMargin)
        {
            var neighborTile = neighbors.GetTileAt(-1, ty);
            if (neighborTile.Equals(TileData.Empty))
            {
                float t = (float)(tx + 1) / (TransitionMargin + 1);
                minOpacity = MathF.Min(minOpacity, t);
            }
        }

        // East edge
        if (tx >= tilesPerSide - TransitionMargin)
        {
            var neighborTile = neighbors.GetTileAt(tilesPerSide, ty);
            if (neighborTile.Equals(TileData.Empty))
            {
                float t = (float)(tilesPerSide - tx) / (TransitionMargin + 1);
                minOpacity = MathF.Min(minOpacity, t);
            }
        }

        return minOpacity;
    }
}
