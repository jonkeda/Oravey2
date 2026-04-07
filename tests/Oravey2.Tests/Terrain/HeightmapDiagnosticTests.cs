using Oravey2.Core.World;
using Oravey2.Core.World.Rendering;
using Oravey2.Core.World.Terrain;
using Xunit.Abstractions;

namespace Oravey2.Tests.Terrain;

public class HeightmapDiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public HeightmapDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DumpMeshData_ForDebugging()
    {
        var tiles = new TileData[16, 16];
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                tiles[x, y] = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0);

        var (verts, indices) = HeightmapMeshGenerator.Generate(tiles, null, QualityPreset.Medium);

        _output.WriteLine($"Vertices: {verts.Length}");
        _output.WriteLine($"Indices: {indices.Length}");
        _output.WriteLine($"Triangles: {indices.Length / 3}");

        // First few vertices
        for (int i = 0; i < Math.Min(5, verts.Length); i++)
            _output.WriteLine($"  v[{i}] pos=({verts[i].Position.X:F3},{verts[i].Position.Y:F3},{verts[i].Position.Z:F3}) n=({verts[i].Normal.X:F3},{verts[i].Normal.Y:F3},{verts[i].Normal.Z:F3})");

        // Last vertex
        _output.WriteLine($"  v[{verts.Length - 1}] pos=({verts[^1].Position.X:F3},{verts[^1].Position.Y:F3},{verts[^1].Position.Z:F3})");

        // First 2 triangles
        _output.WriteLine($"Tri 0: {indices[0]}, {indices[1]}, {indices[2]}");
        _output.WriteLine($"Tri 1: {indices[3]}, {indices[4]}, {indices[5]}");

        // Check winding: first triangle
        var v0 = verts[indices[0]].Position;
        var v1 = verts[indices[1]].Position;
        var v2 = verts[indices[2]].Position;
        var e1 = v1 - v0;
        var e2 = v2 - v0;
        var cross = System.Numerics.Vector3.Cross(e1, e2);
        _output.WriteLine($"Tri 0 cross product (face normal): ({cross.X:F3},{cross.Y:F3},{cross.Z:F3})");
        _output.WriteLine($"  If Y > 0 → face points UP (visible from above)");
        _output.WriteLine($"  If Y < 0 → face points DOWN (backface-culled from above)");

        // Bounding box
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in verts)
        {
            minX = Math.Min(minX, v.Position.X); maxX = Math.Max(maxX, v.Position.X);
            minY = Math.Min(minY, v.Position.Y); maxY = Math.Max(maxY, v.Position.Y);
            minZ = Math.Min(minZ, v.Position.Z); maxZ = Math.Max(maxZ, v.Position.Z);
        }
        _output.WriteLine($"BBox: ({minX:F2},{minY:F2},{minZ:F2}) → ({maxX:F2},{maxY:F2},{maxZ:F2})");
    }
}
