using Oravey2.Core.World;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Oravey2.Core.Bootstrap.Spawners;

public sealed class BuildingSpawnerFactory : IEntitySpawnerFactory
{
    private const int ChunkSize = 16;
    private readonly Game _game;
    private readonly Dictionary<string, Model> _modelCache = new();

    public BuildingSpawnerFactory(Game game) => _game = game;

    public bool CanHandle(string prefabId)
        => prefabId.StartsWith("building:", StringComparison.OrdinalIgnoreCase)
        || prefabId.StartsWith("building_", StringComparison.OrdinalIgnoreCase);

    public Entity? Spawn(Scene scene, EntitySpawnInfo spawn, int chunkX, int chunkY)
    {
        // PrefabId format: "building:id:shape:size" or legacy "building:id"
        var parts = spawn.PrefabId.Split(':');
        var shape = parts.Length > 2 ? parts[2] : "cube";
        var size = parts.Length > 3 ? parts[3] : "medium";

        var (scaleXZ, scaleY) = size.ToLowerInvariant() switch
        {
            "small" => (0.8f, 1.2f),
            "large" => (2.0f, 3.0f),
            _ => (1.2f, 2.0f), // medium
        };

        var worldPos = new Vector3(
            chunkX * ChunkSize + spawn.LocalX,
            scaleY / 2f,
            chunkY * ChunkSize + spawn.LocalZ);

        var cacheKey = $"{shape}:{size}";
        if (!_modelCache.TryGetValue(cacheKey, out var model))
        {
            model = CreateModel(shape, scaleXZ, scaleY);
            _modelCache[cacheKey] = model;
        }

        var entity = new Entity($"building_{spawn.PrefabId}");
        entity.Transform.Position = worldPos;
        entity.Transform.RotationEulerXYZ = new Vector3(0, MathUtil.DegreesToRadians(spawn.RotationY), 0);
        entity.Add(new ModelComponent(model));

        scene.Entities.Add(entity);
        return entity;
    }

    private Model CreateModel(string shape, float scaleXZ, float scaleY)
    {
        var gd = _game.GraphicsDevice;
        var meshDraw = shape.ToLowerInvariant() switch
        {
            "pyramid" => GeometricPrimitive.Cone.New(gd, scaleXZ, scaleY, 4).ToMeshDraw(),
            "cylinder" => GeometricPrimitive.Cylinder.New(gd, scaleY, scaleXZ / 2f).ToMeshDraw(),
            "sphere" => GeometricPrimitive.Sphere.New(gd, scaleXZ).ToMeshDraw(),
            _ => GeometricPrimitive.Cube.New(gd, new Vector3(scaleXZ, scaleY, scaleXZ)).ToMeshDraw(),
        };

        var color = shape.ToLowerInvariant() switch
        {
            "pyramid" => new Color(0.85f, 0.75f, 0.35f), // gold
            _ => new Color(0.65f, 0.45f, 0.25f),          // brown
        };

        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = meshDraw });
        model.Materials.Add(_game.CreateMaterial(color));
        return model;
    }
}
