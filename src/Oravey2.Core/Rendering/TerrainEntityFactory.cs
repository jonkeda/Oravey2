using System.Numerics;
using Oravey2.Core.World.Terrain;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
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

        var mesh = CreateMesh(device, terrain);
        var material = TerrainMaterialFactory.CreateChunkMaterial(device, terrain);

        var model = new Model();
        model.Meshes.Add(mesh);
        model.Materials.Add(material);

        entity.Add(new ModelComponent(model));

        return entity;
    }

    private static Mesh CreateMesh(GraphicsDevice device, ChunkTerrainMesh terrain)
    {
        var vertexData = ConvertVertices(terrain.Vertices);

        var vertexBuffer = Stride.Graphics.Buffer.Vertex.New(device, vertexData);
        var indexBuffer = Stride.Graphics.Buffer.Index.New(device, terrain.Indices);

        var meshDraw = new MeshDraw
        {
            PrimitiveType = PrimitiveType.TriangleList,
            DrawCount = terrain.Indices.Length,
            VertexBuffers = new[]
            {
                new VertexBufferBinding(vertexBuffer,
                    VertexPositionNormalTexture.Layout, terrain.Vertices.Length)
            },
            IndexBuffer = new IndexBufferBinding(indexBuffer, true, terrain.Indices.Length),
        };

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

        return new Mesh
        {
            Draw = meshDraw,
            BoundingBox = boundingBox,
            BoundingSphere = boundingSphere,
        };
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
