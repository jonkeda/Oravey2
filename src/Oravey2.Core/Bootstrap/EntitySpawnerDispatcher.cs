using Oravey2.Core.World;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Oravey2.Core.Bootstrap;

public sealed class EntitySpawnerDispatcher
{
    private readonly IReadOnlyList<IEntitySpawnerFactory> _factories;

    public EntitySpawnerDispatcher(IReadOnlyList<IEntitySpawnerFactory> factories)
        => _factories = factories;

    public List<Entity> SpawnAll(Scene scene,
        IEnumerable<(int ChunkX, int ChunkY, EntitySpawnInfo Spawn)> spawns,
        float tileSize = 1f, Vector3 worldOffset = default)
    {
        var entities = new List<Entity>();
        foreach (var (cx, cy, spawn) in spawns)
        {
            var factory = _factories.FirstOrDefault(f => f.CanHandle(spawn.PrefabId));
            var entity = factory?.Spawn(scene, spawn, cx, cy);
            if (entity != null)
            {
                // Scale tile coords to world units and apply centering offset
                var pos = entity.Transform.Position;
                pos = new Vector3(
                    pos.X * tileSize + worldOffset.X,
                    pos.Y,
                    pos.Z * tileSize + worldOffset.Z);
                entity.Transform.Position = pos;
                entities.Add(entity);
            }
        }
        return entities;
    }
}
