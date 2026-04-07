using System.Numerics;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Core.World.Vegetation;

/// <summary>
/// Generates tree mesh vertices and indices for a set of tree spawns in a chunk.
/// Produces separate trunk and canopy meshes (different materials).
/// Trunk = cylinder, Canopy = sphere. Dead trees have trunk only.
/// Billboard meshes are generated separately for LOD switching.
/// </summary>
public static class TreeRenderer
{
    /// <summary>Distance (in world units) at which trees switch from mesh to billboard.</summary>
    public const float BillboardDistance = 50f;

    /// <summary>Transition range over which mesh/billboard crossfade.</summary>
    public const float TransitionRange = 5f;

    private const int CylinderSegments = 8;
    private const float BaseTrunkRadius = 0.15f;
    private const float BaseTrunkHeight = 1.2f;
    private const float BaseCanopyRadius = 0.6f;
    private const float BillboardWidth = 1.5f;
    private const float BillboardHeight = 2.0f;

    /// <summary>
    /// Builds trunk and canopy mesh data for all trees in a chunk.
    /// </summary>
    public static TreeChunkMeshData BuildMeshes(
        IReadOnlyList<TreeSpawn> spawns,
        float[,] heights,
        int vertsPerSide,
        float chunkWorldSize)
    {
        var trunkVerts = new List<VertexData>();
        var trunkIndices = new List<int>();
        var canopyVerts = new List<VertexData>();
        var canopyIndices = new List<int>();

        foreach (var spawn in spawns)
        {
            float surfaceY = HeightmapMeshGenerator.GetSurfaceHeight(
                spawn.Position, heights, vertsPerSide, chunkWorldSize);
            float scale = spawn.GrowthStage / 255f;
            scale = Math.Max(scale, 0.15f); // minimum visible size for saplings

            // Trunk (always rendered)
            float trunkR = BaseTrunkRadius * scale;
            float trunkH = BaseTrunkHeight * scale;
            AddCylinder(trunkVerts, trunkIndices, trunkVerts.Count,
                new Vector3(spawn.Position.X, surfaceY, spawn.Position.Y),
                trunkR, trunkH, CylinderSegments);

            // Canopy (only if alive)
            if (!spawn.IsDead)
            {
                float canopyR = BaseCanopyRadius * scale;
                float canopyY = surfaceY + trunkH;
                AddSphere(canopyVerts, canopyIndices, canopyVerts.Count,
                    new Vector3(spawn.Position.X, canopyY + canopyR, spawn.Position.Y),
                    canopyR, 6, 6);
            }
        }

        return new TreeChunkMeshData(
            new TreeMeshData(trunkVerts.ToArray(), trunkIndices.ToArray()),
            new TreeMeshData(canopyVerts.ToArray(), canopyIndices.ToArray()));
    }

    /// <summary>
    /// Builds billboard quads for trees that are beyond the LOD distance.
    /// Each billboard is a camera-facing quad centered at the tree position.
    /// </summary>
    public static TreeMeshData BuildBillboards(
        IReadOnlyList<TreeSpawn> spawns,
        float[,] heights,
        int vertsPerSide,
        float chunkWorldSize)
    {
        var vertices = new List<VertexData>();
        var indices = new List<int>();

        foreach (var spawn in spawns)
        {
            float surfaceY = HeightmapMeshGenerator.GetSurfaceHeight(
                spawn.Position, heights, vertsPerSide, chunkWorldSize);
            float scale = spawn.GrowthStage / 255f;
            scale = Math.Max(scale, 0.15f);

            int baseIndex = vertices.Count;
            float w = BillboardWidth * scale * 0.5f;
            float h = BillboardHeight * scale;
            float cx = spawn.Position.X;
            float cz = spawn.Position.Y;

            // Billboard quad in XZ plane (will be rotated to face camera at render time)
            var normal = new Vector3(0, 0, 1);
            vertices.Add(new VertexData(new Vector3(cx - w, surfaceY, cz), normal, new Vector2(0, 1)));
            vertices.Add(new VertexData(new Vector3(cx + w, surfaceY, cz), normal, new Vector2(1, 1)));
            vertices.Add(new VertexData(new Vector3(cx + w, surfaceY + h, cz), normal, new Vector2(1, 0)));
            vertices.Add(new VertexData(new Vector3(cx - w, surfaceY + h, cz), normal, new Vector2(0, 0)));

            indices.Add(baseIndex);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }

        return new TreeMeshData(vertices.ToArray(), indices.ToArray());
    }

    /// <summary>How far the trunk base is embedded below the terrain surface so
    /// the side faces block light in the shadow map all the way to ground level.</summary>
    public const float TrunkEmbedDepth = 0.3f;

    private static void AddCylinder(
        List<VertexData> vertices, List<int> indices, int baseIndex,
        Vector3 baseCenter, float radius, float height, int segments)
    {
        // Bottom ring + top ring
        for (int ring = 0; ring < 2; ring++)
        {
            // Embed bottom ring well below terrain so side faces occlude light
            // all the way past the surface — no cap needed.
            float y = baseCenter.Y + ring * height - (ring == 0 ? TrunkEmbedDepth : 0f);
            for (int i = 0; i < segments; i++)
            {
                float angle = MathF.PI * 2f * i / segments;
                float nx = MathF.Cos(angle);
                float nz = MathF.Sin(angle);
                float px = baseCenter.X + nx * radius;
                float pz = baseCenter.Z + nz * radius;
                vertices.Add(new VertexData(
                    new Vector3(px, y, pz),
                    new Vector3(nx, 0, nz),
                    new Vector2((float)i / segments, ring)));
            }
        }

        // Side faces
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int bot = baseIndex + i;
            int botNext = baseIndex + next;
            int top = baseIndex + segments + i;
            int topNext = baseIndex + segments + next;

            indices.Add(bot);
            indices.Add(botNext);
            indices.Add(top);

            indices.Add(top);
            indices.Add(botNext);
            indices.Add(topNext);
        }
    }

    private static void AddSphere(
        List<VertexData> vertices, List<int> indices, int baseIndex,
        Vector3 center, float radius, int rings, int segments)
    {
        // Generate vertices
        for (int ring = 0; ring <= rings; ring++)
        {
            float phi = MathF.PI * ring / rings;
            float y = MathF.Cos(phi) * radius;
            float r = MathF.Sin(phi) * radius;

            for (int seg = 0; seg <= segments; seg++)
            {
                float theta = MathF.PI * 2f * seg / segments;
                float x = r * MathF.Cos(theta);
                float z = r * MathF.Sin(theta);
                var normal = Vector3.Normalize(new Vector3(x, y, z));
                vertices.Add(new VertexData(
                    center + new Vector3(x, y, z),
                    normal,
                    new Vector2((float)seg / segments, (float)ring / rings)));
            }
        }

        // Generate indices
        int cols = segments + 1;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int tl = baseIndex + ring * cols + seg;
                int tr = tl + 1;
                int bl = tl + cols;
                int br = bl + 1;

                indices.Add(tl);
                indices.Add(bl);
                indices.Add(tr);

                indices.Add(tr);
                indices.Add(bl);
                indices.Add(br);
            }
        }
    }
}

/// <summary>
/// Combined vertex and index data for a single mesh layer (trunk or canopy).
/// </summary>
public sealed class TreeMeshData
{
    public VertexData[] Vertices { get; }
    public int[] Indices { get; }

    public TreeMeshData(VertexData[] vertices, int[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }
}

/// <summary>
/// Contains separate trunk and canopy mesh data for batched rendering with different materials.
/// </summary>
public sealed class TreeChunkMeshData
{
    public TreeMeshData Trunks { get; }
    public TreeMeshData Canopies { get; }

    public TreeChunkMeshData(TreeMeshData trunks, TreeMeshData canopies)
    {
        Trunks = trunks;
        Canopies = canopies;
    }
}
