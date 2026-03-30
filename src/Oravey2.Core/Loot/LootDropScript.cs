using Oravey2.Core.Inventory.Items;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Oravey2.Core.Loot;

/// <summary>
/// Spawns yellow cube loot entities at enemy death positions.
/// Called from CombatSyncScript.CleanupDead() rather than event-driven,
/// because we need the enemy's position before the entity is removed.
/// </summary>
public class LootDropScript : SyncScript
{
    internal LootTable? LootTable { get; set; }

    private readonly Queue<(Vector3 Position, List<ItemInstance> Items)> _pendingDrops = new();

    /// <summary>
    /// Static registry of loot items per entity. Used instead of PropertyKey
    /// to avoid Stride assembly processor serialization issues.
    /// </summary>
    private static readonly Dictionary<Entity, List<ItemInstance>> _lootRegistry = new();

    public static bool TryGetLootItems(Entity entity, out List<ItemInstance>? items)
        => _lootRegistry.TryGetValue(entity, out items);

    public static bool HasLoot(Entity entity) => _lootRegistry.ContainsKey(entity);

    public static void RemoveLoot(Entity entity) => _lootRegistry.Remove(entity);

    /// <summary>
    /// Called by CombatSyncScript when an enemy dies, before entity removal.
    /// </summary>
    internal void QueueDrop(Vector3 position)
    {
        var items = LootTable?.Roll() ?? [];
        if (items.Count > 0)
            _pendingDrops.Enqueue((position, items));
    }

    public override void Update()
    {
        while (_pendingDrops.TryDequeue(out var drop))
        {
            SpawnLootEntity(drop.Position, drop.Items);
        }
    }

    private void SpawnLootEntity(Vector3 position, List<ItemInstance> items)
    {
        var lootEntity = new Entity($"loot_{position.X:F0}_{position.Z:F0}");
        lootEntity.Transform.Position = position;

        // Visual: small yellow cube
        var visual = new Entity("LootVisual");
        var cubeMesh = GeometricPrimitive.Cube.New(Game.GraphicsDevice, 0.3f).ToMeshDraw();
        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = cubeMesh });
        if (_lootMaterial != null)
            model.Materials.Add(_lootMaterial);
        visual.Add(new ModelComponent(model));
        lootEntity.AddChild(visual);

        // Store items in the static registry
        _lootRegistry[lootEntity] = items;

        Entity.Scene?.Entities.Add(lootEntity);
    }

    // Material cached on Start
    private MaterialInstance? _lootMaterial;

    public override void Start()
    {
        base.Start();
        var descriptor = new MaterialDescriptor
        {
            Attributes =
            {
                Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor(new Color4(0.9f, 0.8f, 0.1f, 1.0f))),
                DiffuseModel = new MaterialDiffuseLambertModelFeature()
            }
        };
        var material = Material.New(Game.GraphicsDevice, descriptor);
        _lootMaterial = new MaterialInstance(material);
    }
}
