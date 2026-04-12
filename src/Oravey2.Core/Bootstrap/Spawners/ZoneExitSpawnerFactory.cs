using Oravey2.Core.World;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Oravey2.Core.Bootstrap.Spawners;

public sealed class ZoneExitSpawnerFactory : IEntitySpawnerFactory
{
    private const int ChunkSize = 16;
    private readonly Game _game;
    private Model? _model;

    public ZoneExitSpawnerFactory(Game game) => _game = game;

    public bool CanHandle(string prefabId) => prefabId.StartsWith("zone_exit:", StringComparison.OrdinalIgnoreCase);

    public Entity? Spawn(Scene scene, EntitySpawnInfo spawn, int chunkX, int chunkY)
    {
        var targetRegion = spawn.PrefabId["zone_exit:".Length..];
        var worldPos = new Vector3(
            chunkX * ChunkSize + spawn.LocalX,
            0.5f,
            chunkY * ChunkSize + spawn.LocalZ);

        _model ??= CreateModel();

        var entity = new Entity($"zone_exit_{targetRegion}");
        entity.Transform.Position = worldPos;
        entity.Add(new ModelComponent(_model));

        scene.Entities.Add(entity);
        return entity;
    }

    private Model CreateModel()
    {
        var mesh = GeometricPrimitive.Cylinder.New(_game.GraphicsDevice, 1.0f, 0.6f).ToMeshDraw();
        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = mesh });
        model.Materials.Add(_game.CreateMaterial(new Color(0.9f, 0.8f, 0.1f))); // yellow
        return model;
    }
}
