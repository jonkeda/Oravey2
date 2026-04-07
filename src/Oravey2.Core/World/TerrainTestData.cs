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

        return map;
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
