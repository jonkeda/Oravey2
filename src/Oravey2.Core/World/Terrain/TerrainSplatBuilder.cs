namespace Oravey2.Core.World.Terrain;

/// <summary>
/// Generates two 32×32 RGBA splat-map textures from a 16×16 TileData grid.
/// Splat0: R=Dirt, G=Asphalt, B=Concrete, A=Grass
/// Splat1: R=Sand, G=Mud, B=Rock, A=Metal
/// The 2× oversampling (32 texels for 16 tiles) uses bilinear blending at tile boundaries.
/// </summary>
public static class TerrainSplatBuilder
{
    public const int SplatSize = 32;

    /// <summary>
    /// Builds two splat maps from the tile grid.
    /// Each map is 32×32 pixels, 4 bytes per pixel (RGBA), stored row-major.
    /// </summary>
    public static (byte[] Splat0, byte[] Splat1) Build(TileData[,] tiles)
    {
        int tilesPerSide = tiles.GetLength(0);
        int pixelCount = SplatSize * SplatSize;
        var splat0 = new byte[pixelCount * 4];
        var splat1 = new byte[pixelCount * 4];

        for (int py = 0; py < SplatSize; py++)
        {
            for (int px = 0; px < SplatSize; px++)
            {
                // Map texel centre to tile space
                // Texel (0,0) covers the top-left quarter of tile (0,0)
                // Texel (1,0) covers the right half of tile (0,0) into tile (1,0)
                float tileX = (px + 0.5f) * tilesPerSide / SplatSize - 0.5f;
                float tileY = (py + 0.5f) * tilesPerSide / SplatSize - 0.5f;

                // Gather the 4 nearest tiles for bilinear blend
                int x0 = Math.Clamp((int)MathF.Floor(tileX), 0, tilesPerSide - 1);
                int y0 = Math.Clamp((int)MathF.Floor(tileY), 0, tilesPerSide - 1);
                int x1 = Math.Min(x0 + 1, tilesPerSide - 1);
                int y1 = Math.Min(y0 + 1, tilesPerSide - 1);

                float fx = tileX - MathF.Floor(tileX);
                float fy = tileY - MathF.Floor(tileY);

                // Clamp fractions for edge texels
                if (tileX < 0) fx = 0;
                if (tileY < 0) fy = 0;

                float w00 = (1 - fx) * (1 - fy);
                float w10 = fx * (1 - fy);
                float w01 = (1 - fx) * fy;
                float w11 = fx * fy;

                // Accumulate surface weights
                float w0R = 0, w0G = 0, w0B = 0, w0A = 0;
                float w1R = 0, w1G = 0, w1B = 0, w1A = 0;

                AccumulateSurface(tiles[x0, y0].Surface, w00, ref w0R, ref w0G, ref w0B, ref w0A, ref w1R, ref w1G, ref w1B, ref w1A);
                AccumulateSurface(tiles[x1, y0].Surface, w10, ref w0R, ref w0G, ref w0B, ref w0A, ref w1R, ref w1G, ref w1B, ref w1A);
                AccumulateSurface(tiles[x0, y1].Surface, w01, ref w0R, ref w0G, ref w0B, ref w0A, ref w1R, ref w1G, ref w1B, ref w1A);
                AccumulateSurface(tiles[x1, y1].Surface, w11, ref w0R, ref w0G, ref w0B, ref w0A, ref w1R, ref w1G, ref w1B, ref w1A);

                int offset = (py * SplatSize + px) * 4;
                splat0[offset + 0] = ToByte(w0R); // R = Dirt
                splat0[offset + 1] = ToByte(w0G); // G = Asphalt
                splat0[offset + 2] = ToByte(w0B); // B = Concrete
                splat0[offset + 3] = ToByte(w0A); // A = Grass

                splat1[offset + 0] = ToByte(w1R); // R = Sand
                splat1[offset + 1] = ToByte(w1G); // G = Mud
                splat1[offset + 2] = ToByte(w1B); // B = Rock
                splat1[offset + 3] = ToByte(w1A); // A = Metal
            }
        }

        return (splat0, splat1);
    }

    private static void AccumulateSurface(SurfaceType surface, float weight,
        ref float w0R, ref float w0G, ref float w0B, ref float w0A,
        ref float w1R, ref float w1G, ref float w1B, ref float w1A)
    {
        switch (surface)
        {
            case SurfaceType.Dirt:     w0R += weight; break;
            case SurfaceType.Asphalt:  w0G += weight; break;
            case SurfaceType.Concrete: w0B += weight; break;
            case SurfaceType.Grass:    w0A += weight; break;
            case SurfaceType.Sand:     w1R += weight; break;
            case SurfaceType.Mud:      w1G += weight; break;
            case SurfaceType.Rock:     w1B += weight; break;
            case SurfaceType.Metal:    w1A += weight; break;
        }
    }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)(value * 255f + 0.5f), 0, 255);
}
