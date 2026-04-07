using Oravey2.Core.World;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Tests.Terrain;

public class TerrainSplatBuilderTests
{
    [Fact]
    public void UniformSurface_ProducesSingleChannelSplat()
    {
        var tiles = new TileData[ChunkData.Size, ChunkData.Size];
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                tiles[x, y] = new TileData(SurfaceType.Grass, 0, 0, 0, TileFlags.Walkable, 0);

        var (splat0, splat1) = TerrainSplatBuilder.Build(tiles);

        // All-grass → splat0.A (channel 3) should be 255 everywhere
        for (int i = 0; i < TerrainSplatBuilder.SplatSize * TerrainSplatBuilder.SplatSize; i++)
        {
            int offset = i * 4;
            Assert.Equal(255, splat0[offset + 3]); // A = Grass = 1.0

            // Other channels should be 0
            Assert.Equal(0, splat0[offset + 0]); // R = Dirt
            Assert.Equal(0, splat0[offset + 1]); // G = Asphalt
            Assert.Equal(0, splat0[offset + 2]); // B = Concrete

            // Splat1 should be all zeros
            Assert.Equal(0, splat1[offset + 0]);
            Assert.Equal(0, splat1[offset + 1]);
            Assert.Equal(0, splat1[offset + 2]);
            Assert.Equal(0, splat1[offset + 3]);
        }
    }

    [Fact]
    public void MixedSurface_ProducesBlendsAtBoundaries()
    {
        var tiles = new TileData[ChunkData.Size, ChunkData.Size];
        // Checkerboard of Dirt (left half) and Asphalt (right half)
        for (int x = 0; x < ChunkData.Size; x++)
        {
            for (int y = 0; y < ChunkData.Size; y++)
            {
                var surface = x < ChunkData.Size / 2 ? SurfaceType.Dirt : SurfaceType.Asphalt;
                tiles[x, y] = new TileData(surface, 0, 0, 0, TileFlags.Walkable, 0);
            }
        }

        var (splat0, _) = TerrainSplatBuilder.Build(tiles);

        // At the boundary (texels near x=16 in a 32-wide splat for 16-wide tile grid),
        // some texels should have partial weights for both Dirt (R) and Asphalt (G)
        bool hasBlendsAtBorder = false;
        int midTexel = TerrainSplatBuilder.SplatSize / 2; // 16

        for (int py = 0; py < TerrainSplatBuilder.SplatSize; py++)
        {
            // Check texels near the centre boundary
            for (int px = midTexel - 1; px <= midTexel + 1; px++)
            {
                int offset = (py * TerrainSplatBuilder.SplatSize + px) * 4;
                int dirt = splat0[offset + 0];
                int asphalt = splat0[offset + 1];

                if (dirt > 0 && asphalt > 0)
                {
                    hasBlendsAtBorder = true;
                    break;
                }
            }
            if (hasBlendsAtBorder) break;
        }

        Assert.True(hasBlendsAtBorder,
            "Boundary between Dirt and Asphalt should produce blended texels");
    }

    [Fact]
    public void SplatTextureSize_Is32x32()
    {
        var tiles = new TileData[ChunkData.Size, ChunkData.Size];
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                tiles[x, y] = new TileData(SurfaceType.Grass, 0, 0, 0, TileFlags.Walkable, 0);

        var (splat0, splat1) = TerrainSplatBuilder.Build(tiles);

        Assert.Equal(32 * 32 * 4, splat0.Length);
        Assert.Equal(32 * 32 * 4, splat1.Length);
    }
}
