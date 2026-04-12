using Oravey2.Core.World;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Oravey2.Core.Bootstrap.Spawners;

public sealed class PropSpawnerFactory : IEntitySpawnerFactory
{
    private const int ChunkSize = 16;
    private readonly Game _game;
    private Model? _model;

    public PropSpawnerFactory(Game game) => _game = game;

    public bool CanHandle(string prefabId) => prefabId.StartsWith("prop:", StringComparison.OrdinalIgnoreCase);

    public Entity? Spawn(Scene scene, EntitySpawnInfo spawn, int chunkX, int chunkY)
    {
        var propId = spawn.PrefabId["prop:".Length..];
        var worldPos = new Vector3(
            chunkX * ChunkSize + spawn.LocalX,
            0.15f,
            chunkY * ChunkSize + spawn.LocalZ);

        _model ??= CreateModel();

        var entity = new Entity($"prop_{propId}");
        entity.Transform.Position = worldPos;
        entity.Transform.RotationEulerXYZ = new Vector3(0, MathUtil.DegreesToRadians(spawn.RotationY), 0);
        entity.Add(new ModelComponent(_model));

        scene.Entities.Add(entity);
        return entity;
    }

    private Model CreateModel()
    {
        var mesh = GeometricPrimitive.Sphere.New(_game.GraphicsDevice, 0.3f).ToMeshDraw();
        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = mesh });
        model.Materials.Add(_game.CreateMaterial(new Color(0.6f, 0.6f, 0.6f))); // gray
        return model;
    }
}
