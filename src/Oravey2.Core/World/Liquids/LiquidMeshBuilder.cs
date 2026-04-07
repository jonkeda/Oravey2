using System.Numerics;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Core.World.Liquids;

/// <summary>
/// Mesh data for a rendered liquid region (flat surface + optional waterfall cascades).
/// </summary>
public sealed class LiquidMesh
{
    public VertexData[] Vertices { get; }
    public int[] Indices { get; }
    public LiquidType Type { get; }
    public bool Emissive { get; }

    public LiquidMesh(VertexData[] vertices, int[] indices, LiquidType type, bool emissive)
    {
        Vertices = vertices;
        Indices = indices;
        Type = type;
        Emissive = emissive;
    }
}

/// <summary>
/// Builds flat liquid surface meshes and vertical waterfall cascade meshes.
/// </summary>
public static class LiquidMeshBuilder
{
    /// <summary>Y offset above liquid surface to prevent z-fighting with terrain.</summary>
    public const float LiquidOffset = 0.03f;

    /// <summary>
    /// Builds a flat surface mesh for a liquid region. Each tile becomes one quad.
    /// </summary>
    public static LiquidMesh BuildSurface(LiquidRegion region, float tileWorldSize)
    {
        var props = LiquidProperties.Get(region.Type);
        int tileCount = region.Tiles.Count;
        var vertices = new VertexData[tileCount * 4];
        var indices = new int[tileCount * 6];
        var normal = new Vector3(0, 1, 0);
        float y = region.SurfaceY + LiquidOffset;

        for (int i = 0; i < tileCount; i++)
        {
            var (tx, ty) = region.Tiles[i];
            float x0 = tx * tileWorldSize;
            float z0 = ty * tileWorldSize;
            float x1 = x0 + tileWorldSize;
            float z1 = z0 + tileWorldSize;

            int vi = i * 4;
            vertices[vi + 0] = new VertexData(new Vector3(x0, y, z0), normal, new Vector2(0, 0));
            vertices[vi + 1] = new VertexData(new Vector3(x1, y, z0), normal, new Vector2(1, 0));
            vertices[vi + 2] = new VertexData(new Vector3(x0, y, z1), normal, new Vector2(0, 1));
            vertices[vi + 3] = new VertexData(new Vector3(x1, y, z1), normal, new Vector2(1, 1));

            int ii = i * 6;
            indices[ii + 0] = vi;
            indices[ii + 1] = vi + 2;
            indices[ii + 2] = vi + 1;
            indices[ii + 3] = vi + 1;
            indices[ii + 4] = vi + 2;
            indices[ii + 5] = vi + 3;
        }

        return new LiquidMesh(vertices, indices, region.Type, props.Emissive);
    }

    /// <summary>
    /// Detects waterfall edges and builds vertical cascade meshes.
    /// A waterfall exists where a liquid tile has an adjacent tile with a height delta ≥ CliffThreshold.
    /// </summary>
    public static List<LiquidMesh> BuildWaterfalls(
        LiquidRegion region,
        TileMapData tiles,
        float tileWorldSize)
    {
        var cascades = new List<LiquidMesh>();
        var props = LiquidProperties.Get(region.Type);

        foreach (var (tx, ty) in region.Tiles)
        {
            var tile = tiles.GetTileData(tx, ty);

            // Check 4 cardinal neighbors for cliff-edge height difference
            CheckWaterfallEdge(tx, ty, tx, ty - 1, tile, tiles, tileWorldSize, region, props, cascades); // North
            CheckWaterfallEdge(tx, ty, tx + 1, ty, tile, tiles, tileWorldSize, region, props, cascades); // East
            CheckWaterfallEdge(tx, ty, tx, ty + 1, tile, tiles, tileWorldSize, region, props, cascades); // South
            CheckWaterfallEdge(tx, ty, tx - 1, ty, tile, tiles, tileWorldSize, region, props, cascades); // West
        }

        return cascades;
    }

    private static void CheckWaterfallEdge(
        int fromX, int fromY, int toX, int toY,
        TileData fromTile, TileMapData tiles,
        float tileWorldSize, LiquidRegion region,
        LiquidPropertySet props, List<LiquidMesh> cascades)
    {
        if (toX < 0 || toX >= tiles.Width || toY < 0 || toY >= tiles.Height)
            return;

        var toTile = tiles.GetTileData(toX, toY);
        int delta = fromTile.HeightLevel - toTile.HeightLevel;

        if (delta < HeightHelper.CliffThreshold)
            return;

        // Build a vertical ribbon from upper water surface down to lower terrain + water level
        float upperY = region.SurfaceY;
        float lowerY = toTile.HasWater
            ? WaterHelper.GetWaterSurfaceY(toTile)
            : toTile.HeightLevel * HeightHelper.HeightStep;

        if (lowerY >= upperY) return;

        // Determine edge position and orientation
        float cx = fromX * tileWorldSize;
        float cz = fromY * tileWorldSize;

        Vector3 left, right;
        int dx = toX - fromX;
        int dy = toY - fromY;

        if (dx == 0 && dy == -1) // North edge
        {
            left = new Vector3(cx, 0, cz);
            right = new Vector3(cx + tileWorldSize, 0, cz);
        }
        else if (dx == 1 && dy == 0) // East edge
        {
            left = new Vector3(cx + tileWorldSize, 0, cz);
            right = new Vector3(cx + tileWorldSize, 0, cz + tileWorldSize);
        }
        else if (dx == 0 && dy == 1) // South edge
        {
            left = new Vector3(cx + tileWorldSize, 0, cz + tileWorldSize);
            right = new Vector3(cx, 0, cz + tileWorldSize);
        }
        else // West edge
        {
            left = new Vector3(cx, 0, cz + tileWorldSize);
            right = new Vector3(cx, 0, cz);
        }

        // Build a vertical quad (2 triangles)
        var normal = Vector3.Normalize(new Vector3(dx, 0, dy));
        var vertices = new VertexData[4];
        vertices[0] = new VertexData(new Vector3(left.X, upperY, left.Z), normal, new Vector2(0, 0));
        vertices[1] = new VertexData(new Vector3(right.X, upperY, right.Z), normal, new Vector2(1, 0));
        vertices[2] = new VertexData(new Vector3(left.X, lowerY, left.Z), normal, new Vector2(0, 1));
        vertices[3] = new VertexData(new Vector3(right.X, lowerY, right.Z), normal, new Vector2(1, 1));

        var indices = new int[] { 0, 2, 1, 1, 2, 3 };

        cascades.Add(new LiquidMesh(vertices, indices, region.Type, props.Emissive));
    }
}
