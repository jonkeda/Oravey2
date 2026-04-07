using System.Numerics;
using Oravey2.Core.World;
using Oravey2.Core.World.Terrain;
using Oravey2.Core.World.Vegetation;

var mapData = TerrainTestData.CreateTestMap();
var worldMap = TerrainTestData.CreateTestWorldMap(mapData);

// Build terrain for chunk (0,0) - the one with forested tiles
var chunk = worldMap.GetChunk(0, 0);
var terrainMesh = ChunkTerrainBuilder.Build(chunk);

// Get tree spawns
var spawns = TreePlacementHelper.GenerateForChunk(chunk.Tiles);
Console.WriteLine($"Tree spawns: {spawns.Count}");

// Check first few trees
foreach (var s in spawns.Take(5))
{
    float surfaceH = HeightmapMeshGenerator.GetSurfaceHeight(s.Position, terrainMesh.Heights, terrainMesh.VertsPerSide, terrainMesh.ChunkWorldSize);
    
    // Find nearest terrain vertex
    float cellSize = terrainMesh.ChunkWorldSize / (terrainMesh.VertsPerSide - 1);
    int vx = (int)Math.Round(s.Position.X / cellSize);
    int vy = (int)Math.Round(s.Position.Y / cellSize);
    vx = Math.Clamp(vx, 0, terrainMesh.VertsPerSide - 1);
    vy = Math.Clamp(vy, 0, terrainMesh.VertsPerSide - 1);
    float vertexH = terrainMesh.Heights[vx, vy];
    float terrainVertexY = terrainMesh.Vertices[vy * terrainMesh.VertsPerSide + vx].Position.Y;
    
    Console.WriteLine($"Tree at ({s.Position.X:F2}, {s.Position.Y:F2}): surfaceH={surfaceH:F4}, nearestVertexH={vertexH:F4}, terrainVertexY={terrainVertexY:F4}");
}
