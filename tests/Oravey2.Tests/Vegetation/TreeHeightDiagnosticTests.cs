using System.Numerics;
using Oravey2.Core.World;
using Oravey2.Core.World.Terrain;
using Oravey2.Core.World.Vegetation;
using Xunit.Abstractions;

namespace Oravey2.Tests.Vegetation;

public class TreeHeightDiagnosticTests
{
    private readonly ITestOutputHelper _output;
    public TreeHeightDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TreeSurfaceHeight_MatchesTerrainVertices_OnFlatGround()
    {
        var mapData = TerrainTestData.CreateTestMap();
        var worldMap = TerrainTestData.CreateTestWorldMap(mapData);

        var chunk = worldMap.GetChunk(0, 0)!;
        var terrainMesh = ChunkTerrainBuilder.Build(chunk);
        var spawns = TreePlacementHelper.GenerateForChunk(chunk.Tiles);

        Assert.True(spawns.Count > 0, "Expected tree spawns in forested chunk");

        // Build tree meshes the EXACT same way HTS does
        var treeMeshes = TreeRenderer.BuildMeshes(
            spawns, terrainMesh.Heights, terrainMesh.VertsPerSide, terrainMesh.ChunkWorldSize);

        // For each tree, find the lowest trunk vertex Y and compare to the
        // terrain surface height at the tree's XZ position.
        // The trunk bottom ring should have Y == surfaceHeight - TrunkEmbedDepth.
        float maxDelta = 0f;
        int trunkVertsPerTree = 8 * 2; // CylinderSegments * 2 rings

        for (int i = 0; i < spawns.Count; i++)
        {
            int baseVert = i * trunkVertsPerTree;
            if (baseVert >= treeMeshes.Trunks.Vertices.Length) break;

            // Bottom ring is the first 8 vertices
            float minTrunkY = float.MaxValue;
            float maxTrunkBottomX = float.MinValue;
            float minTrunkBottomX = float.MaxValue;
            for (int v = 0; v < 8 && (baseVert + v) < treeMeshes.Trunks.Vertices.Length; v++)
            {
                var vert = treeMeshes.Trunks.Vertices[baseVert + v];
                if (vert.Position.Y < minTrunkY) minTrunkY = vert.Position.Y;
                if (vert.Position.X > maxTrunkBottomX) maxTrunkBottomX = vert.Position.X;
                if (vert.Position.X < minTrunkBottomX) minTrunkBottomX = vert.Position.X;
            }

            float surfaceH = HeightmapMeshGenerator.GetSurfaceHeight(
                spawns[i].Position, terrainMesh.Heights,
                terrainMesh.VertsPerSide, terrainMesh.ChunkWorldSize);

            // The trunk base should be embedded TrunkEmbedDepth (0.05) below the surface
            float expectedDelta = TreeRenderer.TrunkEmbedDepth;
            float delta = Math.Abs(minTrunkY - (surfaceH - expectedDelta));
            if (delta > maxDelta) maxDelta = delta;

            if (i < 5)
            {
                _output.WriteLine($"Tree {i} at ({spawns[i].Position.X:F2},{spawns[i].Position.Y:F2}): " +
                    $"trunkBaseY={minTrunkY:F4}, surfaceH={surfaceH:F4}, delta={delta:F4}");

                // Also check a few terrain vertex heights near this tree
                float cellSize = terrainMesh.ChunkWorldSize / (terrainMesh.VertsPerSide - 1);
                int vx = (int)(spawns[i].Position.X / cellSize);
                int vy = (int)(spawns[i].Position.Y / cellSize);
                vx = Math.Clamp(vx, 0, terrainMesh.VertsPerSide - 2);
                vy = Math.Clamp(vy, 0, terrainMesh.VertsPerSide - 2);
                _output.WriteLine($"  Nearby vertex heights: [{vx},{vy}]={terrainMesh.Heights[vx,vy]:F4}, " +
                    $"[{vx+1},{vy}]={terrainMesh.Heights[vx+1,vy]:F4}, " +
                    $"[{vx},{vy+1}]={terrainMesh.Heights[vx,vy+1]:F4}");
            }
        }

        _output.WriteLine($"Max delta: {maxDelta:F6}");
        Assert.True(maxDelta < 0.01f,
            $"Tree trunk base Y differs from terrain surface by {maxDelta:F4}");
    }

    [Fact]
    public void TreeSurfaceHeight_ExpectedValue_FlatHeightLevel4()
    {
        var tiles = new TileMapData(16, 16);
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                tiles.SetTileData(x, y, new TileData(
                    SurfaceType.Grass, 4, 0, 0,
                    TileFlags.Walkable | TileFlags.Forested, 42));

        var chunk = new ChunkData(0, 0, tiles);
        var terrainMesh = ChunkTerrainBuilder.Build(chunk);

        float expectedY = 4 * HeightmapMeshGenerator.HeightStep; // 1.0

        float midX = 8 * HeightmapMeshGenerator.TileWorldSize + 1f;
        float midZ = 8 * HeightmapMeshGenerator.TileWorldSize + 1f;
        float surfaceH = HeightmapMeshGenerator.GetSurfaceHeight(
            new Vector2(midX, midZ), terrainMesh.Heights,
            terrainMesh.VertsPerSide, terrainMesh.ChunkWorldSize);

        float cellSize = terrainMesh.ChunkWorldSize / (terrainMesh.VertsPerSide - 1);
        int vx = (int)Math.Round(midX / cellSize);
        int vy = (int)Math.Round(midZ / cellSize);
        float vertexY = terrainMesh.Heights[vx, vy];

        Assert.Equal(expectedY, surfaceH, 0.01f);
        Assert.Equal(expectedY, vertexY, 0.01f);
    }

    [Fact]
    public void StructureHeight_MatchesTerrainSurface()
    {
        var mapData = TerrainTestData.CreateTestMap();
        var worldMap = TerrainTestData.CreateTestWorldMap(mapData);

        // Chunk (2,0) is the Hybrid town chunk with walls
        var chunk = worldMap.GetChunk(2, 0)!;
        var terrainMesh = ChunkTerrainBuilder.Build(chunk);

        Assert.NotNull(terrainMesh.Overlay);
        var structures = terrainMesh.Overlay!.Structures;
        Assert.True(structures.Count > 0, "Expected structures in Hybrid chunk");

        float maxDelta = 0f;
        foreach (var structure in structures)
        {
            // The structure.Position.Y is the terrain surface height at the wall position
            // Check it matches GetSurfaceHeight at the same XZ
            float surfaceH = HeightmapMeshGenerator.GetSurfaceHeight(
                new Vector2(structure.Position.X, structure.Position.Z),
                terrainMesh.Heights, terrainMesh.VertsPerSide, terrainMesh.ChunkWorldSize);

            // Also find the actual terrain vertex Y near this position
            float cellSize = terrainMesh.ChunkWorldSize / (terrainMesh.VertsPerSide - 1);
            int vx = (int)(structure.Position.X / cellSize);
            int vy = (int)(structure.Position.Z / cellSize);
            vx = Math.Clamp(vx, 0, terrainMesh.VertsPerSide - 2);
            vy = Math.Clamp(vy, 0, terrainMesh.VertsPerSide - 2);
            float nearestVertY = terrainMesh.Heights[vx, vy];

            float delta = Math.Abs(structure.Position.Y - nearestVertY);
            if (delta > maxDelta) maxDelta = delta;

            _output.WriteLine($"Structure at ({structure.Position.X:F2},{structure.Position.Z:F2}): " +
                $"structY={structure.Position.Y:F4}, surfaceH={surfaceH:F4}, nearestVertY={nearestVertY:F4}, " +
                $"delta={delta:F4}");
        }

        _output.WriteLine($"Max structure delta: {maxDelta:F6}");
        // If this fails, the structure Y (from GetSurfaceHeight) doesn't match terrain vertices
        Assert.True(maxDelta < 0.3f,
            $"Structure Y differs from nearest terrain vertex by {maxDelta:F4}");
    }
}
