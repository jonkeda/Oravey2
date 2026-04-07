using Oravey2.Core.World.Terrain;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Oravey2.Core.Rendering;

/// <summary>
/// Creates Stride materials for terrain chunks from splat map data.
/// Uses the built-in material system (no custom SDSL pipeline required).
/// Each chunk gets a material with a diffuse color derived from the dominant surface blend.
/// </summary>
public static class TerrainMaterialFactory
{
    // Palette: surface type → approximate colour
    private static readonly Color4[] SurfaceColors =
    {
        new(0.55f, 0.40f, 0.26f, 1f), // Dirt
        new(0.25f, 0.25f, 0.27f, 1f), // Asphalt
        new(0.65f, 0.65f, 0.62f, 1f), // Concrete
        new(0.30f, 0.52f, 0.20f, 1f), // Grass
        new(0.80f, 0.72f, 0.50f, 1f), // Sand
        new(0.35f, 0.28f, 0.18f, 1f), // Mud
        new(0.50f, 0.48f, 0.42f, 1f), // Rock
        new(0.45f, 0.47f, 0.50f, 1f), // Metal
    };

    /// <summary>
    /// Creates a Stride Texture from raw RGBA bytes.
    /// </summary>
    public static Texture CreateSplatTexture(GraphicsDevice device, byte[] rgba, int width, int height)
    {
        return Texture.New2D(device, width, height, PixelFormat.R8G8B8A8_UNorm, rgba);
    }

    /// <summary>
    /// Creates a flat-colored material for a chunk based on the dominant surface type.
    /// Used for rendering; later replaced by the full splat shader.
    /// </summary>
    public static Material CreateChunkMaterial(GraphicsDevice device, ChunkTerrainMesh terrain)
    {
        // Compute average colour from splat map 0 and 1
        var color = ComputeAverageColor(terrain.SplatMap0, terrain.SplatMap1,
            TerrainSplatBuilder.SplatSize);

        var materialDesc = new MaterialDescriptor
        {
            Attributes =
            {
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor { Key = MaterialKeys.DiffuseValue })
            }
        };

        var material = Material.New(device, materialDesc);
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, color);
        return material;
    }

    /// <summary>
    /// Computes a weighted average color from two splat maps.
    /// </summary>
    private static Color4 ComputeAverageColor(byte[] splat0, byte[] splat1, int splatSize)
    {
        float r = 0, g = 0, b = 0;
        int pixelCount = splatSize * splatSize;

        for (int i = 0; i < pixelCount; i++)
        {
            int offset = i * 4;

            // Splat0: Dirt(R), Asphalt(G), Concrete(B), Grass(A)
            float wDirt = splat0[offset + 0] / 255f;
            float wAsphalt = splat0[offset + 1] / 255f;
            float wConcrete = splat0[offset + 2] / 255f;
            float wGrass = splat0[offset + 3] / 255f;

            // Splat1: Sand(R), Mud(G), Rock(B), Metal(A)
            float wSand = splat1[offset + 0] / 255f;
            float wMud = splat1[offset + 1] / 255f;
            float wRock = splat1[offset + 2] / 255f;
            float wMetal = splat1[offset + 3] / 255f;

            r += wDirt * SurfaceColors[0].R + wAsphalt * SurfaceColors[1].R
               + wConcrete * SurfaceColors[2].R + wGrass * SurfaceColors[3].R
               + wSand * SurfaceColors[4].R + wMud * SurfaceColors[5].R
               + wRock * SurfaceColors[6].R + wMetal * SurfaceColors[7].R;

            g += wDirt * SurfaceColors[0].G + wAsphalt * SurfaceColors[1].G
               + wConcrete * SurfaceColors[2].G + wGrass * SurfaceColors[3].G
               + wSand * SurfaceColors[4].G + wMud * SurfaceColors[5].G
               + wRock * SurfaceColors[6].G + wMetal * SurfaceColors[7].G;

            b += wDirt * SurfaceColors[0].B + wAsphalt * SurfaceColors[1].B
               + wConcrete * SurfaceColors[2].B + wGrass * SurfaceColors[3].B
               + wSand * SurfaceColors[4].B + wMud * SurfaceColors[5].B
               + wRock * SurfaceColors[6].B + wMetal * SurfaceColors[7].B;
        }

        r /= pixelCount;
        g /= pixelCount;
        b /= pixelCount;

        return new Color4(r, g, b, 1f);
    }
}
