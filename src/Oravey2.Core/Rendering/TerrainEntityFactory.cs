using System.Numerics;
using Oravey2.Core.World.Terrain;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Oravey2.Core.Rendering;

/// <summary>
/// Converts a ChunkTerrainMesh into a Stride Entity with model + material.
/// </summary>
public static class TerrainEntityFactory
{
    /// <summary>
    /// Creates a Stride Entity containing the heightmap mesh for a single chunk.
    /// The entity is positioned at the chunk's world-space origin.
    /// </summary>
    public static Entity CreateChunkEntity(
        GraphicsDevice device,
        ChunkTerrainMesh terrain,
        int chunkX,
        int chunkY,
        float chunkWorldSize)
    {
        var entity = new Entity($"Terrain_{chunkX}_{chunkY}");
        entity.Transform.Position = new Stride.Core.Mathematics.Vector3(
            chunkX * chunkWorldSize,
            0f,
            chunkY * chunkWorldSize);

        var (mesh, primitive) = CreateMesh(device, terrain);
        var material = TerrainMaterialFactory.CreateChunkMaterial(device, terrain);

        var model = new Model();
        model.Meshes.Add(mesh);
        model.Materials.Add(material);

        entity.Add(new ModelComponent(model));

        return entity;
    }

    /// <summary>
    /// Creates a Mesh + GeometricPrimitive from terrain data.
    /// Uses GeometricPrimitive + GeometricMeshData for correct buffer lifecycle (not raw Buffer.Vertex.New).
    /// The caller must keep the returned GeometricPrimitive alive as long as the mesh is in use.
    /// </summary>
    public static (Mesh mesh, GeometricPrimitive primitive) CreateMesh(
        GraphicsDevice device, ChunkTerrainMesh terrain)
    {
        var vertexData = ConvertVertices(terrain.Vertices);

        // Use GeometricMeshData + GeometricPrimitive — this is the proven working pattern.
        // isLeftHanded=true tells Stride to flip our CW winding to CCW (Stride is right-handed).
        var meshData = new GeometricMeshData<VertexPositionNormalTexture>(
            vertexData, terrain.Indices, isLeftHanded: true);
        var primitive = new GeometricPrimitive(device, meshData);
        var meshDraw = primitive.ToMeshDraw();

        // Compute bounding box from vertex positions — required for frustum culling
        var min = new Stride.Core.Mathematics.Vector3(float.MaxValue);
        var max = new Stride.Core.Mathematics.Vector3(float.MinValue);
        foreach (var v in vertexData)
        {
            if (v.Position.X < min.X) min.X = v.Position.X;
            if (v.Position.Y < min.Y) min.Y = v.Position.Y;
            if (v.Position.Z < min.Z) min.Z = v.Position.Z;
            if (v.Position.X > max.X) max.X = v.Position.X;
            if (v.Position.Y > max.Y) max.Y = v.Position.Y;
            if (v.Position.Z > max.Z) max.Z = v.Position.Z;
        }

        var boundingBox = new BoundingBox(min, max);
        var boundingSphere = BoundingSphere.FromBox(boundingBox);

        var mesh = new Mesh
        {
            Draw = meshDraw,
            BoundingBox = boundingBox,
            BoundingSphere = boundingSphere,
        };

        return (mesh, primitive);
    }

    private static VertexPositionNormalTexture[] ConvertVertices(VertexData[] source)
    {
        var result = new VertexPositionNormalTexture[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            result[i] = new VertexPositionNormalTexture(
                new Stride.Core.Mathematics.Vector3(source[i].Position.X, source[i].Position.Y, source[i].Position.Z),
                new Stride.Core.Mathematics.Vector3(source[i].Normal.X, source[i].Normal.Y, source[i].Normal.Z),
                new Stride.Core.Mathematics.Vector2(source[i].TexCoord.X, source[i].TexCoord.Y));
        }

        return result;
    }
}
