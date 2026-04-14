using System.Numerics;

namespace Oravey2.Core.World;

/// <summary>
/// Creates hand-crafted TileMapData for the terrain test scene.
/// Produces a 48×48 tile grid (3×3 chunks of 16 tiles each) with varied surfaces and heights.
/// </summary>
public static class TerrainTestData
{
    public static TileMapData CreateTestMap()
    {
        const int size = 48; // 3 chunks × 16 tiles
        var map = new TileMapData(size, size);

        // Fill the entire map with walkable grass at base height
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Grass,
                    HeightLevel: 4,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: (byte)((x * 7 + y * 13) % 256)));
            }
        }

        // Road: horizontal asphalt strip through the middle
        for (int x = 0; x < size; x++)
        {
            for (int y = 22; y <= 25; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Asphalt,
                    HeightLevel: 4,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: (byte)(x * 3)));
            }
        }

        // Dirt area: bottom-left quadrant
        for (int x = 0; x < 20; x++)
        {
            for (int y = 30; y < size; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Dirt,
                    HeightLevel: 3,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: (byte)((x + y) % 256)));
            }
        }

        // Concrete pad: top-right area
        for (int x = 32; x < 44; x++)
        {
            for (int y = 4; y < 16; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Concrete,
                    HeightLevel: 5,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: 0));
            }
        }

        // Hills: rising terrain in the top-left area
        for (int x = 4; x < 18; x++)
        {
            for (int y = 4; y < 16; y++)
            {
                int cx = x - 11;
                int cy = y - 10;
                int dist = (int)MathF.Sqrt(cx * cx + cy * cy);
                byte height = (byte)Math.Clamp(8 - dist, 4, 12);

                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Grass,
                    HeightLevel: height,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: (byte)(x ^ y)));
            }
        }

        // Sand area: bottom-right
        for (int x = 32; x < size; x++)
        {
            for (int y = 32; y < size; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Sand,
                    HeightLevel: 3,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: (byte)(x * y)));
            }
        }

        // Water lake: bottom-centre area (depression in dirt with water)
        for (int x = 8; x < 16; x++)
        {
            for (int y = 34; y < 42; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Mud,
                    HeightLevel: 1,
                    WaterLevel: 3,
                    StructureId: 0,
                    Flags: TileFlags.None,
                    VariantSeed: (byte)((x + y) % 256),
                    Liquid: LiquidType.Water));
            }
        }

        // Lava pool: small 3×3 area near bottom-centre
        for (int x = 22; x < 25; x++)
        {
            for (int y = 36; y < 39; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Rock,
                    HeightLevel: 2,
                    WaterLevel: 3,
                    StructureId: 0,
                    Flags: TileFlags.None,
                    VariantSeed: 0,
                    Liquid: LiquidType.Lava));
            }
        }

        // Toxic puddle: small 2×2 area
        for (int x = 28; x < 30; x++)
        {
            for (int y = 36; y < 38; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Mud,
                    HeightLevel: 2,
                    WaterLevel: 3,
                    StructureId: 0,
                    Flags: TileFlags.None,
                    VariantSeed: 0,
                    Liquid: LiquidType.Toxic));
            }
        }

        // Waterfall edge: high terrain with water that drops off a cliff
        // Upper plateau with water at height 12
        for (int x = 4; x < 8; x++)
        {
            for (int y = 18; y < 21; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Rock,
                    HeightLevel: 12,
                    WaterLevel: 13,
                    StructureId: 0,
                    Flags: TileFlags.None,
                    VariantSeed: 0,
                    Liquid: LiquidType.Water));
            }
        }
        // Cliff drop below the waterfall (height 4, no water)
        for (int x = 4; x < 8; x++)
        {
            map.SetTileData(x, 21, new TileData(
                Surface: SurfaceType.Rock,
                HeightLevel: 4,
                WaterLevel: 0,
                StructureId: 0,
                Flags: TileFlags.Walkable,
                VariantSeed: 0));
        }

        // Town area: top-right chunk (2,0) — Hybrid mode overlay test
        // Paved concrete floor with wall structures at tile edges
        for (int x = 32; x < 48; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                int structureId = 0;
                var halfCover = CoverEdges.None;
                var fullCover = CoverEdges.None;

                // Outer wall ring for the town
                if (x == 33 && y >= 2 && y <= 13)
                {
                    structureId = 1;
                    fullCover = CoverEdges.West;
                }
                if (x == 44 && y >= 2 && y <= 13)
                {
                    structureId = 1;
                    fullCover = CoverEdges.East;
                }
                if (y == 2 && x >= 33 && x <= 44)
                {
                    structureId = 1;
                    fullCover = CoverEdges.North;
                }
                if (y == 13 && x >= 33 && x <= 44)
                {
                    structureId = 1;
                    fullCover = CoverEdges.South;
                }

                // Door gap in south wall
                if (y == 13 && x >= 38 && x <= 39)
                {
                    structureId = 0;
                    fullCover = CoverEdges.None;
                }

                // Interior prop in town centre
                if (x == 38 && y == 7)
                    structureId = 2;

                var surface = (x >= 33 && x <= 44 && y >= 2 && y <= 13)
                    ? SurfaceType.Concrete
                    : SurfaceType.Grass;

                map.SetTileData(x, y, new TileData(
                    Surface: surface,
                    HeightLevel: 5,
                    WaterLevel: 0,
                    StructureId: structureId,
                    Flags: TileFlags.Walkable,
                    VariantSeed: (byte)((x + y) % 256),
                    HalfCover: halfCover,
                    FullCover: fullCover));
            }
        }

        // Forested areas: two patches of grass with the Forested flag
        // Dense forest: top-left chunk (0,0), tiles (0..10, 0..8)
        for (int x = 0; x < 11; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                // Skip the hills area already set above (4..17, 4..15)
                if (x >= 4 && y >= 4) continue;

                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Grass,
                    HeightLevel: 4,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable | TileFlags.Forested,
                    VariantSeed: (byte)((x * 7 + y * 13) % 256)));
            }
        }

        // Sparse forest: chunk (1,2), bottom area tiles (16..31, 32..40) in grass
        for (int x = 16; x < 32; x++)
        {
            for (int y = 26; y < 30; y++)
            {
                map.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Grass,
                    HeightLevel: 4,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable | TileFlags.Forested,
                    VariantSeed: (byte)((x * 7 + y * 13) % 256)));
            }
        }

        return map;
    }

    /// <summary>
    /// Creates a WorldMapData (3×3 chunks) from the test map, with sample linear features
    /// for visual testing of road and river rendering.
    /// </summary>
    public static WorldMapData CreateTestWorldMap(TileMapData mapData)
    {
        const int chunkSize = ChunkData.Size; // 16
        int chunksX = (mapData.Width + chunkSize - 1) / chunkSize;   // 3
        int chunksY = (mapData.Height + chunkSize - 1) / chunkSize;  // 3
        var worldMap = new WorldMapData(chunksX, chunksY);

        for (int cy = 0; cy < chunksY; cy++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                var tiles = new TileMapData(chunkSize, chunkSize);
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    for (int ly = 0; ly < chunkSize; ly++)
                    {
                        int gx = cx * chunkSize + lx;
                        int gy = cy * chunkSize + ly;
                        if (gx < mapData.Width && gy < mapData.Height)
                            tiles.SetTileData(lx, ly, mapData.TileDataGrid[gx, gy]);
                    }
                }

                var features = GetLinearFeaturesForChunk(cx, cy);
                var mode = GetChunkMode(cx, cy);
                worldMap.SetChunk(cx, cy,
                    new ChunkData(cx, cy, tiles, linearFeatures: features, mode: mode));
            }
        }

        return worldMap;
    }

    /// <summary>
    /// Returns the ChunkMode for a given chunk coordinate in the test world.
    /// Chunk (2,0) (top-right) is Hybrid (town), all others are Heightmap.
    /// </summary>
    private static ChunkMode GetChunkMode(int cx, int cy)
        => (cx == 2 && cy == 0) ? ChunkMode.Hybrid : ChunkMode.Heightmap;

    /// <summary>
    /// Returns sample linear features for a given chunk.
    /// Road: runs horizontally through chunks (0,1), (1,1), (2,1) at Y≈12 in local chunk space.
    /// River: runs diagonally through chunks (1,0), (1,1), (1,2).
    /// </summary>
    private static IReadOnlyList<LinearFeature> GetLinearFeaturesForChunk(int cx, int cy)
    {
        const float tileSize = 2f; // HeightmapMeshGenerator.TileWorldSize
        var features = new List<LinearFeature>();

        // Road across the middle row (chunk row 1), spans full chunk width
        if (cy == 1)
        {
            float roadY = 12f * tileSize; // local Y in chunk (tile row 12)
            features.Add(new LinearFeature
            {
                Type = LinearFeatureType.Secondary, Style = "asphalt", Width = 5f,
                Nodes =
                [
                    new LinearFeatureNode { Position = new Vector2(0f, roadY) },
                    new LinearFeatureNode { Position = new Vector2(8f * tileSize, roadY - 2f) },
                    new LinearFeatureNode { Position = new Vector2(16f * tileSize, roadY) },
                ],
            });
        }

        // River through the middle column (chunk column 1), runs top to bottom
        if (cx == 1)
        {
            float riverX = 8f * tileSize; // local X in chunk (tile col 8)
            features.Add(new LinearFeature
            {
                Type = LinearFeatureType.River, Style = "water", Width = 5f,
                Nodes =
                [
                    new LinearFeatureNode { Position = new Vector2(riverX, 0f) },
                    new LinearFeatureNode { Position = new Vector2(riverX + 4f, 10f * tileSize) },
                    new LinearFeatureNode { Position = new Vector2(riverX - 3f, 16f * tileSize) },
                ],
            });
        }

        // Bridge segment in the centre chunk where road crosses river
        if (cx == 1 && cy == 1)
        {
            float bridgeY = 12f * tileSize;
            float bridgeX = 8f * tileSize;
            features.Add(new LinearFeature
            {
                Type = LinearFeatureType.Secondary, Style = "concrete", Width = 6f,
                Nodes =
                [
                    new LinearFeatureNode { Position = new Vector2(bridgeX - 5f, bridgeY), OverrideHeight = 2.5f },
                    new LinearFeatureNode { Position = new Vector2(bridgeX + 5f, bridgeY), OverrideHeight = 2.5f },
                ],
            });
        }

        return features;
    }

    /// <summary>
    /// Creates a single ChunkData with terrain modifiers for testing the modifier pipeline.
    /// </summary>
    public static ChunkData CreateTestChunkWithCrater()
    {
        const int size = ChunkData.Size;
        var tiles = new TileMapData(size, size);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                tiles.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Grass,
                    HeightLevel: 4,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: 0));
            }
        }

        var modifiers = new List<TerrainModifier>
        {
            new Crater(Centre: new Vector2(16f, 16f), Radius: 8f, Depth: 2f)
        };

        return new ChunkData(0, 0, tiles, terrainModifiers: modifiers);
    }
}
