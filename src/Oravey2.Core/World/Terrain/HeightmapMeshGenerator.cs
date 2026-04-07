using System.Numerics;
using Oravey2.Core.World.Rendering;

namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Generates a heightmap mesh from a 16×16 tile grid.
/// The mesh has (N+1)×(N+1) vertices where N is the tile count at the chosen quality level.
/// At Low quality, 1 quad per tile → 17×17. Medium → 33×33. High → 65×65.
/// </summary>
public static class HeightmapMeshGenerator
{
    /// <summary>
    /// Tile size in world units. Each tile is 2m × 2m.
    /// </summary>
    public const float TileWorldSize = 2f;

    /// <summary>
    /// Height scale: each HeightLevel unit maps to this many world Y units.
    /// </summary>
    public const float HeightStep = 0.25f;

    /// <summary>
    /// Generates a heightmap mesh for a single chunk.
    /// </summary>
    /// <param name="tiles">16×16 tile data array from TileMapData.TileDataGrid.</param>
    /// <param name="neighbors">Provider for sampling tiles from adjacent chunks (for edge stitching).</param>
    /// <param name="quality">Rendering quality preset.</param>
    /// <returns>Vertex and index arrays ready for mesh creation.</returns>
    public static (VertexData[] Vertices, int[] Indices) Generate(
        TileData[,] tiles,
        IChunkNeighborProvider? neighbors,
        QualityPreset quality)
    {
        int tilesPerSide = ChunkData.Size; // 16
        int subdivision = quality switch
        {
            QualityPreset.Low => 1,
            QualityPreset.Medium => 2,
            QualityPreset.High => 4,
            _ => 1
        };

        int quadsPerSide = tilesPerSide * subdivision; // 16, 32, or 64
        int vertsPerSide = quadsPerSide + 1;           // 17, 33, or 65

        // Phase 1: compute heights
        var heights = SampleHeights(tiles, neighbors, vertsPerSide, tilesPerSide, subdivision);

        // Phase 2: build vertices with normals
        var vertices = BuildVertices(heights, vertsPerSide, tilesPerSide);

        // Phase 3: build index buffer
        var indices = BuildIndices(vertsPerSide);

        return (vertices, indices);
    }

    /// <summary>
    /// Samples heights for the vertex grid, averaging adjacent tile heights at vertex positions.
    /// </summary>
    internal static float[,] SampleHeights(
        TileData[,] tiles,
        IChunkNeighborProvider? neighbors,
        int vertsPerSide,
        int tilesPerSide,
        int subdivision)
    {
        var heights = new float[vertsPerSide, vertsPerSide];

        for (int vy = 0; vy < vertsPerSide; vy++)
        {
            for (int vx = 0; vx < vertsPerSide; vx++)
            {
                // Vertex position in tile-space (fractional)
                float tileX = (float)vx / subdivision;
                float tileY = (float)vy / subdivision;

                heights[vx, vy] = SampleHeightAt(tiles, neighbors, tileX, tileY, tilesPerSide);
            }
        }

        return heights;
    }

    /// <summary>
    /// Samples a height at a fractional tile coordinate by bilinear interpolation
    /// of the four nearest tile centres.
    /// </summary>
    private static float SampleHeightAt(
        TileData[,] tiles,
        IChunkNeighborProvider? neighbors,
        float tileX,
        float tileY,
        int tilesPerSide)
    {
        // Tile centres are at (0.5, 0.5), (1.5, 0.5), etc.
        // Shift so we interpolate between centres
        float sx = tileX - 0.5f;
        float sy = tileY - 0.5f;

        int x0 = (int)MathF.Floor(sx);
        int y0 = (int)MathF.Floor(sy);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float fx = sx - x0;
        float fy = sy - y0;

        float h00 = GetHeightAt(tiles, neighbors, x0, y0, tilesPerSide);
        float h10 = GetHeightAt(tiles, neighbors, x1, y0, tilesPerSide);
        float h01 = GetHeightAt(tiles, neighbors, x0, y1, tilesPerSide);
        float h11 = GetHeightAt(tiles, neighbors, x1, y1, tilesPerSide);

        // Bilinear interpolation
        float h = h00 * (1 - fx) * (1 - fy)
                + h10 * fx * (1 - fy)
                + h01 * (1 - fx) * fy
                + h11 * fx * fy;

        return h;
    }

    private static float GetHeightAt(
        TileData[,] tiles,
        IChunkNeighborProvider? neighbors,
        int x, int y,
        int tilesPerSide)
    {
        if (x >= 0 && x < tilesPerSide && y >= 0 && y < tilesPerSide)
            return tiles[x, y].HeightLevel * HeightStep;

        if (neighbors != null)
            return neighbors.GetTileAt(x, y).HeightLevel * HeightStep;

        // No neighbor data — clamp to edge tile
        int cx = Math.Clamp(x, 0, tilesPerSide - 1);
        int cy = Math.Clamp(y, 0, tilesPerSide - 1);
        return tiles[cx, cy].HeightLevel * HeightStep;
    }

    /// <summary>
    /// Builds vertices with positions, normals, and UVs from the height grid.
    /// </summary>
    internal static VertexData[] BuildVertices(float[,] heights, int vertsPerSide, int tilesPerSide)
    {
        float chunkWorldSize = tilesPerSide * TileWorldSize;
        var vertices = new VertexData[vertsPerSide * vertsPerSide];

        for (int vy = 0; vy < vertsPerSide; vy++)
        {
            for (int vx = 0; vx < vertsPerSide; vx++)
            {
                float u = (float)vx / (vertsPerSide - 1);
                float v = (float)vy / (vertsPerSide - 1);

                float worldX = u * chunkWorldSize;
                float worldZ = v * chunkWorldSize;
                float worldY = heights[vx, vy];

                var normal = ComputeNormal(heights, vx, vy, vertsPerSide, chunkWorldSize);

                vertices[vy * vertsPerSide + vx] = new VertexData(
                    new Vector3(worldX, worldY, worldZ),
                    normal,
                    new Vector2(u, v));
            }
        }

        return vertices;
    }

    /// <summary>
    /// Computes a per-vertex normal by averaging cross products of adjacent triangle edges.
    /// </summary>
    private static Vector3 ComputeNormal(float[,] heights, int vx, int vy, int vertsPerSide, float chunkWorldSize)
    {
        float step = chunkWorldSize / (vertsPerSide - 1);
        float h = heights[vx, vy];

        // Sample left/right/up/down with clamping
        float hL = heights[Math.Max(0, vx - 1), vy];
        float hR = heights[Math.Min(vertsPerSide - 1, vx + 1), vy];
        float hD = heights[vx, Math.Max(0, vy - 1)];
        float hU = heights[vx, Math.Min(vertsPerSide - 1, vy + 1)];

        // Use central differences for slope
        float dx;
        float dy;

        if (vx > 0 && vx < vertsPerSide - 1)
            dx = (hR - hL) / (2 * step);
        else if (vx == 0)
            dx = (hR - h) / step;
        else
            dx = (h - hL) / step;

        if (vy > 0 && vy < vertsPerSide - 1)
            dy = (hU - hD) / (2 * step);
        else if (vy == 0)
            dy = (hU - h) / step;
        else
            dy = (h - hD) / step;

        // Normal from slope: n = normalize(-dx, 1, -dy)
        var normal = Vector3.Normalize(new Vector3(-dx, 1f, -dy));
        return normal;
    }

    /// <summary>
    /// Gets the interpolated surface height at an arbitrary XZ world position within a chunk,
    /// using barycentric interpolation on the heightmap mesh triangles.
    /// </summary>
    /// <param name="worldXZ">XZ position in chunk-local space (0..chunkWorldSize).</param>
    /// <param name="heights">Height grid from terrain pipeline.</param>
    /// <param name="vertsPerSide">Vertex grid resolution.</param>
    /// <param name="chunkWorldSize">World-space size of the chunk.</param>
    /// <returns>Interpolated Y height at the given position.</returns>
    public static float GetSurfaceHeight(
        Vector2 worldXZ,
        float[,] heights,
        int vertsPerSide,
        float chunkWorldSize)
    {
        float cellSize = chunkWorldSize / (vertsPerSide - 1);

        // Convert world XZ to grid-cell coordinates
        float gx = worldXZ.X / cellSize;
        float gz = worldXZ.Y / cellSize;

        // Clamp to valid grid range
        gx = Math.Clamp(gx, 0f, vertsPerSide - 1f);
        gz = Math.Clamp(gz, 0f, vertsPerSide - 1f);

        int ix = (int)MathF.Floor(gx);
        int iz = (int)MathF.Floor(gz);

        // Clamp cell indices to avoid overflow
        ix = Math.Min(ix, vertsPerSide - 2);
        iz = Math.Min(iz, vertsPerSide - 2);

        float fx = gx - ix;
        float fz = gz - iz;

        float hTL = heights[ix, iz];
        float hTR = heights[ix + 1, iz];
        float hBL = heights[ix, iz + 1];
        float hBR = heights[ix + 1, iz + 1];

        // Determine which triangle the point falls in and do barycentric interpolation.
        // The quad is split along the TL→BR diagonal (matches BuildIndices winding):
        //   Triangle 1: TL, BL, TR  (fx + fz <= 1)
        //   Triangle 2: TR, BL, BR  (fx + fz > 1)
        if (fx + fz <= 1f)
        {
            // Upper-left triangle: TL(0,0), BL(0,1), TR(1,0)
            return hTL + fx * (hTR - hTL) + fz * (hBL - hTL);
        }
        else
        {
            // Lower-right triangle: BR(1,1), TR(1,0), BL(0,1)
            return hBR + (1f - fx) * (hBL - hBR) + (1f - fz) * (hTR - hBR);
        }
    }

    /// <summary>
    /// Builds a triangle-list index buffer for a quad grid.
    /// </summary>
    internal static int[] BuildIndices(int vertsPerSide)
    {
        int quads = (vertsPerSide - 1) * (vertsPerSide - 1);
        var indices = new int[quads * 6];
        int idx = 0;

        for (int vy = 0; vy < vertsPerSide - 1; vy++)
        {
            for (int vx = 0; vx < vertsPerSide - 1; vx++)
            {
                int tl = vy * vertsPerSide + vx;
                int tr = tl + 1;
                int bl = tl + vertsPerSide;
                int br = bl + 1;

                // Two triangles per quad (CW winding for Stride's default front-face)
                indices[idx++] = tl;
                indices[idx++] = bl;
                indices[idx++] = tr;

                indices[idx++] = tr;
                indices[idx++] = bl;
                indices[idx++] = br;
            }
        }

        return indices;
    }
}
