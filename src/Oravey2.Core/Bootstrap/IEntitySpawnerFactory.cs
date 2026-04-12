using Oravey2.Core.World;
using Stride.Engine;

namespace Oravey2.Core.Bootstrap;

public interface IEntitySpawnerFactory
{
    bool CanHandle(string prefabId);
    Entity? Spawn(Scene scene, EntitySpawnInfo spawn, int chunkX, int chunkY);
}
