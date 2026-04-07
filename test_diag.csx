using Oravey2.Core.World;
using Oravey2.Core.World.Terrain;
using Oravey2.Core.World.Rendering;

var tiles = new TileData[16, 16];
for (int x = 0; x < 16; x++)
    for (int y = 0; y < 16; y++)
        tiles[x, y] = new TileData(SurfaceType.Dirt, 1, 0, 0, TileFlags.Walkable, 0);

var (verts, indices) = HeightmapMeshGenerator.Generate(tiles, null, QualityPreset.Medium);

Console.WriteLine($"Vertices: {verts.Length}");
Console.WriteLine($"Indices: {indices.Length}");
Console.WriteLine($"First 3 verts:");
for (int i = 0; i < 3; i++)
    Console.WriteLine($"  [{i}] pos=({verts[i].Position.X:F2},{verts[i].Position.Y:F2},{verts[i].Position.Z:F2}) n=({verts[i].Normal.X:F2},{verts[i].Normal.Y:F2},{verts[i].Normal.Z:F2})");
Console.WriteLine($"First triangle: {indices[0]}, {indices[1]}, {indices[2]}");
Console.WriteLine($"Second triangle: {indices[3]}, {indices[4]}, {indices[5]}");
Console.WriteLine($"Last vert: pos=({verts[^1].Position.X:F2},{verts[^1].Position.Y:F2},{verts[^1].Position.Z:F2})");
