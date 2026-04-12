using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Oravey2.MapGen.Generation;

namespace Oravey2.MapGen.Assets;

/// <summary>
/// Generates minimal valid glTF 2.0 binary (.glb) files for primitive shapes.
/// </summary>
public static class PrimitiveMeshWriter
{
    private const uint GlbMagic = 0x46546C67; // "glTF"
    private const uint GlbVersion = 2;
    private const uint ChunkTypeJson = 0x4E4F534A; // "JSON"
    private const uint ChunkTypeBin = 0x004E4942;  // "BIN\0"

    public static readonly string PyramidPath = "assets/meshes/primitives/pyramid.glb";
    public static readonly string CubePath = "assets/meshes/primitives/cube.glb";
    public static readonly string SpherePath = "assets/meshes/primitives/sphere.glb";

    /// <summary>
    /// Ensures all three primitive .glb files exist under <paramref name="contentRoot"/>/assets/.
    /// </summary>
    public static void EnsurePrimitiveMeshes(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "assets", "meshes", "primitives");
        Directory.CreateDirectory(dir);

        WriteIfMissing(Path.Combine(dir, "pyramid.glb"), BuildPyramid);
        WriteIfMissing(Path.Combine(dir, "cube.glb"), BuildCube);
        WriteIfMissing(Path.Combine(dir, "sphere.glb"), () => BuildSphere());

        WriteMetaIfMissing(dir, "pyramid", "Pyramid placeholder for landmark buildings");
        WriteMetaIfMissing(dir, "cube", "Cube placeholder for buildings");
        WriteMetaIfMissing(dir, "sphere", "Sphere placeholder for props");
    }

    private static void WriteMetaIfMissing(string dir, string name, string description)
    {
        var metaPath = Path.Combine(dir, $"{name}.meta.json");
        if (File.Exists(metaPath)) return;
        AssetFiles.SaveMeta(new AssetMeta
        {
            AssetId = name,
            Prompt = description,
            GeneratedAt = DateTime.UtcNow,
            Status = "accepted",
            SourceType = "primitive",
            SizeCategory = "medium",
        }, metaPath);
    }

    private static void WriteIfMissing(string path, Func<byte[]> builder)
    {
        if (!File.Exists(path))
            File.WriteAllBytes(path, builder());
    }

    // ── Pyramid ──────────────────────────────────────────────────────────

    internal static byte[] BuildPyramid()
    {
        // 5 unique vertices, 6 triangles (4 side faces + 2 for base quad)
        float[] positions =
        [
            0f, 1f, 0f,       // 0: apex
            -0.5f, 0f, -0.5f, // 1: base front-left
            0.5f, 0f, -0.5f,  // 2: base front-right
            0.5f, 0f, 0.5f,   // 3: base back-right
            -0.5f, 0f, 0.5f,  // 4: base back-left
        ];

        ushort[] indices =
        [
            0, 1, 2, // front
            0, 2, 3, // right
            0, 3, 4, // back
            0, 4, 1, // left
            1, 3, 2, // base tri 1
            1, 4, 3, // base tri 2
        ];

        var normals = ComputeFlatNormals(positions, indices);
        return WriteGlb(positions, normals, indices, "Pyramid");
    }

    // ── Cube ─────────────────────────────────────────────────────────────

    internal static byte[] BuildCube()
    {
        // 24 vertices (4 per face for correct normals), 36 indices
        float[] positions =
        [
            // Front face (z = -0.5)
            -0.5f, -0.5f, -0.5f,  0.5f, -0.5f, -0.5f,  0.5f, 0.5f, -0.5f,  -0.5f, 0.5f, -0.5f,
            // Back face (z = 0.5)
            0.5f, -0.5f, 0.5f,  -0.5f, -0.5f, 0.5f,  -0.5f, 0.5f, 0.5f,  0.5f, 0.5f, 0.5f,
            // Top face (y = 0.5)
            -0.5f, 0.5f, -0.5f,  0.5f, 0.5f, -0.5f,  0.5f, 0.5f, 0.5f,  -0.5f, 0.5f, 0.5f,
            // Bottom face (y = -0.5)
            -0.5f, -0.5f, 0.5f,  0.5f, -0.5f, 0.5f,  0.5f, -0.5f, -0.5f,  -0.5f, -0.5f, -0.5f,
            // Right face (x = 0.5)
            0.5f, -0.5f, -0.5f,  0.5f, -0.5f, 0.5f,  0.5f, 0.5f, 0.5f,  0.5f, 0.5f, -0.5f,
            // Left face (x = -0.5)
            -0.5f, -0.5f, 0.5f,  -0.5f, -0.5f, -0.5f,  -0.5f, 0.5f, -0.5f,  -0.5f, 0.5f, 0.5f,
        ];

        float[] normals =
        [
            // Front
            0, 0, -1,  0, 0, -1,  0, 0, -1,  0, 0, -1,
            // Back
            0, 0, 1,  0, 0, 1,  0, 0, 1,  0, 0, 1,
            // Top
            0, 1, 0,  0, 1, 0,  0, 1, 0,  0, 1, 0,
            // Bottom
            0, -1, 0,  0, -1, 0,  0, -1, 0,  0, -1, 0,
            // Right
            1, 0, 0,  1, 0, 0,  1, 0, 0,  1, 0, 0,
            // Left
            -1, 0, 0,  -1, 0, 0,  -1, 0, 0,  -1, 0, 0,
        ];

        ushort[] indices =
        [
            0, 1, 2,  0, 2, 3,     // front
            4, 5, 6,  4, 6, 7,     // back
            8, 9, 10, 8, 10, 11,   // top
            12, 13, 14, 12, 14, 15, // bottom
            16, 17, 18, 16, 18, 19, // right
            20, 21, 22, 20, 22, 23, // left
        ];

        return WriteGlb(positions, normals, indices, "Cube");
    }

    // ── Sphere ───────────────────────────────────────────────────────────

    internal static byte[] BuildSphere(int slices = 16, int stacks = 8)
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var indices = new List<ushort>();

        // Generate vertices
        for (var stack = 0; stack <= stacks; stack++)
        {
            var phi = MathF.PI * stack / stacks;
            var sinPhi = MathF.Sin(phi);
            var cosPhi = MathF.Cos(phi);

            for (var slice = 0; slice <= slices; slice++)
            {
                var theta = 2f * MathF.PI * slice / slices;
                var x = sinPhi * MathF.Cos(theta);
                var y = cosPhi;
                var z = sinPhi * MathF.Sin(theta);

                positions.AddRange([x * 0.5f, y * 0.5f, z * 0.5f]);
                normals.AddRange([x, y, z]);
            }
        }

        // Generate indices
        for (var stack = 0; stack < stacks; stack++)
        {
            for (var slice = 0; slice < slices; slice++)
            {
                var first = (ushort)(stack * (slices + 1) + slice);
                var second = (ushort)(first + slices + 1);

                indices.AddRange([first, second, (ushort)(first + 1)]);
                indices.AddRange([(ushort)(first + 1), second, (ushort)(second + 1)]);
            }
        }

        return WriteGlb(
            [.. positions],
            [.. normals],
            [.. indices],
            "Sphere");
    }

    // ── GLB writer ───────────────────────────────────────────────────────

    internal static byte[] WriteGlb(float[] positions, float[] normals, ushort[] indices, string meshName)
    {
        var vertexCount = positions.Length / 3;
        var posBytes = positions.Length * sizeof(float);
        var normBytes = normals.Length * sizeof(float);
        var idxBytes = indices.Length * sizeof(ushort);

        // Pad index buffer to 4-byte alignment
        var idxBytesPadded = (idxBytes + 3) & ~3;
        var totalBinSize = posBytes + normBytes + idxBytesPadded;

        // Compute bounding box for positions
        var minX = float.MaxValue; var minY = float.MaxValue; var minZ = float.MaxValue;
        var maxX = float.MinValue; var maxY = float.MinValue; var maxZ = float.MinValue;
        for (var i = 0; i < positions.Length; i += 3)
        {
            minX = MathF.Min(minX, positions[i]);     maxX = MathF.Max(maxX, positions[i]);
            minY = MathF.Min(minY, positions[i + 1]); maxY = MathF.Max(maxY, positions[i + 1]);
            minZ = MathF.Min(minZ, positions[i + 2]); maxZ = MathF.Max(maxZ, positions[i + 2]);
        }

        // Build glTF JSON
        var gltf = new
        {
            asset = new { version = "2.0", generator = "Oravey2.PrimitiveMeshWriter" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes = new[] { new { mesh = 0, name = meshName } },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new { POSITION = 0, NORMAL = 1 },
                            indices = 2,
                        }
                    }
                }
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    componentType = 5126, // FLOAT
                    count = vertexCount,
                    type = "VEC3",
                    min = new[] { minX, minY, minZ },
                    max = new[] { maxX, maxY, maxZ },
                },
                new
                {
                    bufferView = 1,
                    componentType = 5126,
                    count = vertexCount,
                    type = "VEC3",
                },
                new
                {
                    bufferView = 2,
                    componentType = 5123, // UNSIGNED_SHORT
                    count = indices.Length,
                    type = "SCALAR",
                },
            },
            bufferViews = new[]
            {
                new { buffer = 0, byteOffset = 0, byteLength = posBytes },
                new { buffer = 0, byteOffset = posBytes, byteLength = normBytes },
                new { buffer = 0, byteOffset = posBytes + normBytes, byteLength = idxBytes },
            },
            buffers = new[] { new { byteLength = totalBinSize } },
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(gltf, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        });

        // Pad JSON to 4-byte alignment with spaces (per spec)
        var jsonPadded = ((jsonBytes.Length + 3) / 4) * 4;

        var totalSize = 12 + 8 + jsonPadded + 8 + totalBinSize;
        var glb = new byte[totalSize];
        var span = glb.AsSpan();
        var offset = 0;

        // GLB header
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], GlbMagic); offset += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], GlbVersion); offset += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)totalSize); offset += 4;

        // JSON chunk
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)jsonPadded); offset += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], ChunkTypeJson); offset += 4;
        jsonBytes.CopyTo(span[offset..]);
        for (var i = jsonBytes.Length; i < jsonPadded; i++)
            span[offset + i] = 0x20; // space padding
        offset += jsonPadded;

        // Binary chunk
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)totalBinSize); offset += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], ChunkTypeBin); offset += 4;

        // Write positions
        foreach (var f in positions)
        {
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], f);
            offset += 4;
        }

        // Write normals
        foreach (var f in normals)
        {
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], f);
            offset += 4;
        }

        // Write indices
        foreach (var idx in indices)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], idx);
            offset += 2;
        }

        return glb;
    }

    private static float[] ComputeFlatNormals(float[] positions, ushort[] indices)
    {
        // For simple shapes, approximate per-vertex normals from adjacent faces
        var normals = new float[positions.Length];
        for (var i = 0; i < indices.Length; i += 3)
        {
            var i0 = indices[i] * 3;
            var i1 = indices[i + 1] * 3;
            var i2 = indices[i + 2] * 3;

            var ax = positions[i1] - positions[i0];
            var ay = positions[i1 + 1] - positions[i0 + 1];
            var az = positions[i1 + 2] - positions[i0 + 2];
            var bx = positions[i2] - positions[i0];
            var by = positions[i2 + 1] - positions[i0 + 1];
            var bz = positions[i2 + 2] - positions[i0 + 2];

            var nx = ay * bz - az * by;
            var ny = az * bx - ax * bz;
            var nz = ax * by - ay * bx;

            // Accumulate (unnormalized — we normalize at the end)
            normals[i0] += nx; normals[i0 + 1] += ny; normals[i0 + 2] += nz;
            normals[i1] += nx; normals[i1 + 1] += ny; normals[i1 + 2] += nz;
            normals[i2] += nx; normals[i2 + 1] += ny; normals[i2 + 2] += nz;
        }

        // Normalize
        for (var i = 0; i < normals.Length; i += 3)
        {
            var len = MathF.Sqrt(normals[i] * normals[i] + normals[i + 1] * normals[i + 1] + normals[i + 2] * normals[i + 2]);
            if (len > 0)
            {
                normals[i] /= len;
                normals[i + 1] /= len;
                normals[i + 2] /= len;
            }
        }

        return normals;
    }
}
