using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Items;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Oravey2.Core.Combat;

/// <summary>
/// Reusable enemy spawning logic. Used by both ScenarioLoader and OraveyAutomationHandler.
/// </summary>
public sealed class EnemySpawner
{
    private readonly Game _game;
    private readonly IEventBus _eventBus;
    private MaterialInstance? _cachedMaterial;

    public EnemySpawner(Game game, IEventBus eventBus)
    {
        _game = game;
        _eventBus = eventBus;
    }

    public EnemyInfo Spawn(
        Scene scene,
        string id,
        float x, float z,
        int endurance, int luck,
        int weaponDamage, float weaponAccuracy,
        string? tag = null,
        int? overrideHp = null)
    {
        var enemyEntity = new Entity(id);
        enemyEntity.Transform.Position = new Vector3(x, 0.5f, z);

        var visual = new Entity($"{id}_Visual");
        var mesh = GeometricPrimitive.Capsule.New(_game.GraphicsDevice, 0.3f, 0.8f).ToMeshDraw();
        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = mesh });
        _cachedMaterial ??= _game.CreateMaterial(new Color(0.8f, 0.15f, 0.15f));
        model.Materials.Add(_cachedMaterial);
        visual.Add(new ModelComponent(model));
        enemyEntity.AddChild(visual);

        scene.Entities.Add(enemyEntity);

        var stats = new StatsComponent(new Dictionary<Stat, int>
        {
            { Stat.Strength, 3 }, { Stat.Perception, 3 }, { Stat.Endurance, endurance },
            { Stat.Charisma, 2 }, { Stat.Intelligence, 2 }, { Stat.Agility, 4 },
            { Stat.Luck, luck },
        });
        var level = new LevelComponent(stats);
        var health = new HealthComponent(stats, level, _eventBus);
        var combat = new CombatComponent { InCombat = false };

        if (overrideHp.HasValue && overrideHp.Value < health.MaxHP)
            health.TakeDamage(health.MaxHP - overrideHp.Value);

        var weapon = new WeaponData(
            Damage: weaponDamage, Range: 1.5f, ApCost: 3,
            Accuracy: weaponAccuracy, SkillType: "melee", CritMultiplier: 1.5f);

        return new EnemyInfo
        {
            Entity = enemyEntity,
            Id = id,
            Health = health,
            Combat = combat,
            Stats = stats,
            Weapon = weapon,
            Tag = tag,
        };
    }

    /// <summary>
    /// Spawn enemies from a list of spawn points, adding them to the provided combat systems.
    /// </summary>
    public List<EnemyInfo> SpawnFromPoints(
        Scene scene,
        IReadOnlyList<EnemySpawnPoint> spawnPoints,
        CombatSyncScript combatScript,
        EncounterTriggerScript encounterTrigger)
    {
        var spawned = new List<EnemyInfo>();
        var counter = 0;

        foreach (var sp in spawnPoints)
        {
            for (int i = 0; i < sp.Count; i++)
            {
                var id = $"{sp.GroupId}_{counter++}";
                // Offset each enemy slightly so they don't overlap
                var offsetX = sp.X + (i * 1.5f);
                var info = Spawn(scene, id, offsetX, sp.Z,
                    sp.Endurance, sp.Luck, sp.WeaponDamage, sp.WeaponAccuracy, sp.Tag);

                combatScript.Enemies.Add(info);
                spawned.Add(info);
            }
        }

        return spawned;
    }
}
