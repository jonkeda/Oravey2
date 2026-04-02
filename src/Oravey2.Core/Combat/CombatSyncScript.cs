using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Loot;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Oravey2.Core.Combat;

/// <summary>
/// Holds pure-C# combat data associated with a Stride entity.
/// </summary>
public sealed class EnemyInfo
{
    public required Entity Entity { get; init; }
    public required string Id { get; init; }
    public required HealthComponent Health { get; init; }
    public required CombatComponent Combat { get; init; }
    public required StatsComponent Stats { get; init; }
    public WeaponData? Weapon { get; init; }
    public string? Tag { get; init; }
}

public class CombatSyncScript : SyncScript
{
    // --- Player refs (set from Program.cs) ---
    public Entity? Player { get; set; }
    public HealthComponent? PlayerHealth { get; set; }
    public CombatComponent? PlayerCombat { get; set; }

    // --- Shared enemy list (also used by EncounterTriggerScript) ---
    internal List<EnemyInfo> Enemies { get; set; } = [];

    // --- Combat services (set from Program.cs) ---
    public CombatEngine? Engine { get; set; }
    public ActionQueue? Queue { get; set; }
    public CombatStateManager? CombatState { get; set; }
    public GameStateManager? StateManager { get; set; }
    public LootDropScript? LootDrop { get; set; }
    public IEventBus? EventBus { get; set; }

    // --- Phase D: equipment/stats refs (set from Program.cs) ---
    public EquipmentComponent? PlayerEquipment { get; set; }
    public StatsComponent? PlayerStats { get; set; }

    /// <summary>
    /// Set during ProcessNextAction() for FloatingDamageScript to read.
    /// Reset to null at the start of each frame.
    /// </summary>
    public Entity? LastHitTarget { get; internal set; }

    // --- Unarmed fallback when no weapon equipped ---
    private static readonly WeaponData UnarmedWeapon = new(
        Damage: 5, Range: 1.5f, ApCost: 3,
        Accuracy: 0.50f, SkillType: "melee", CritMultiplier: 1.5f);

    private const string PlayerId = "player";

    // --- Hit flash state ---
    private readonly Dictionary<Entity, (MaterialInstance Original, float Timer)> _flashingEntities = [];
    private MaterialInstance? _flashMaterial;
    private const float FlashDuration = 0.2f;

    private IInputProvider? _inputProvider;

    public override void Start()
    {
        if (ServiceLocator.Instance.TryGet<IInputProvider>(out var inputProvider))
            _inputProvider = inputProvider;
    }

    public override void Update()
    {
        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        LastHitTarget = null;

        // Always tick flash timers (even outside combat for lingering flashes)
        UpdateHitFlash(dt);

        if (StateManager == null || StateManager.CurrentState != GameState.InCombat)
            return;

        if (Engine == null || Queue == null || CombatState == null)
            return;

        // 1. Regen AP for all combatants
        PlayerCombat?.Regen(dt);
        foreach (var enemy in Enemies)
            enemy.Combat.Regen(dt);

        // 2. Player attack input (A4)
        HandlePlayerInput();

        // 3. Enemy AI — auto-queue melee attacks when AP allows
        RunEnemyAI();

        // 4. Process one queued action per frame
        ProcessNextAction();

        // 5. Check for dead enemies and remove them
        CleanupDead();

        // 6. Check for player death
        if (PlayerHealth != null && !PlayerHealth.IsAlive)
        {
            StateManager?.TransitionTo(GameState.GameOver);
            return;
        }
    }

    // ---- A4: Player Attack Input ----

    private void HandlePlayerInput()
    {
        if (_inputProvider == null || Player == null || PlayerCombat == null)
            return;

        if (!_inputProvider.IsActionPressed(GameAction.Attack))
            return;

        var equipped = PlayerEquipment?.GetEquipped(EquipmentSlot.PrimaryWeapon);
        var weapon = equipped?.Definition.Weapon ?? UnarmedWeapon;
        if (!PlayerCombat.CanAfford(weapon.ApCost))
            return;

        // Target the nearest living enemy
        var nearest = FindNearestEnemy();
        if (nearest == null) return;

        Queue!.Enqueue(new CombatAction(PlayerId, CombatActionType.MeleeAttack, nearest.Id));
    }

    private EnemyInfo? FindNearestEnemy()
    {
        if (Player == null) return null;

        var playerPos = Player.Transform.Position;
        EnemyInfo? best = null;
        var bestDist = float.MaxValue;

        foreach (var enemy in Enemies)
        {
            if (!enemy.Health.IsAlive) continue;
            var dist = Vector3.Distance(playerPos, enemy.Entity.Transform.Position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        return best;
    }

    // ---- A3: Enemy AI (M0 — simple auto-attack) ----

    private void RunEnemyAI()
    {
        foreach (var enemy in Enemies)
        {
            if (!enemy.Health.IsAlive) continue;

            var weapon = enemy.Weapon ?? UnarmedWeapon;
            if (!enemy.Combat.CanAfford(weapon.ApCost)) continue;

            // Check if there's already a queued action for this enemy
            if (Queue!.PendingActions.Any(a => a.ActorId == enemy.Id))
                continue;

            Queue.Enqueue(new CombatAction(enemy.Id, CombatActionType.MeleeAttack, PlayerId));
        }
    }

    // ---- A3: Process ActionQueue ----

    private void ProcessNextAction()
    {
        var action = Queue!.Dequeue();
        if (action == null) return;

        if (action.Type != CombatActionType.MeleeAttack || action.TargetId == null)
            return;

        // Resolve attacker, target, weapon, and stats
        CombatComponent? attackerCombat;
        HealthComponent? targetHealth;
        Entity? targetEntity;
        WeaponData weapon;
        int attackerLuck;
        int targetArmorDR = 0;
        float distance;

        if (action.ActorId == PlayerId)
        {
            attackerCombat = PlayerCombat;
            var target = Enemies.FirstOrDefault(e => e.Id == action.TargetId);
            if (target == null || !target.Health.IsAlive) return;
            targetHealth = target.Health;
            targetEntity = target.Entity;
            distance = Vector3.Distance(
                Player!.Transform.Position, target.Entity.Transform.Position);

            var equipped = PlayerEquipment?.GetEquipped(EquipmentSlot.PrimaryWeapon);
            weapon = equipped?.Definition.Weapon ?? UnarmedWeapon;
            attackerLuck = PlayerStats?.GetEffective(Stat.Luck) ?? 5;
            // Enemies have no armor in M0
        }
        else
        {
            var attacker = Enemies.FirstOrDefault(e => e.Id == action.ActorId);
            if (attacker == null || !attacker.Health.IsAlive) return;
            attackerCombat = attacker.Combat;
            targetHealth = PlayerHealth;
            targetEntity = Player;
            distance = Vector3.Distance(
                attacker.Entity.Transform.Position, Player!.Transform.Position);

            weapon = attacker.Weapon ?? UnarmedWeapon;
            attackerLuck = attacker.Stats.GetEffective(Stat.Luck);

            // Read player armor DR
            if (PlayerEquipment != null)
            {
                var torsoArmor = PlayerEquipment.GetEquipped(EquipmentSlot.Torso);
                targetArmorDR = torsoArmor?.Definition.Armor?.DamageReduction ?? 0;
            }
        }

        if (attackerCombat == null || targetHealth == null || targetEntity == null)
            return;

        // D3: Melee attacks use effective distance 0 (combatants at striking range)
        var effectiveDistance = action.Type == CombatActionType.MeleeAttack
            ? 0f
            : distance;

        var context = new AttackContext(
            WeaponAccuracy: weapon.Accuracy,
            WeaponDamage: weapon.Damage,
            WeaponRange: weapon.Range,
            CritMultiplier: weapon.CritMultiplier,
            SkillLevel: 0,
            Luck: attackerLuck,
            ArmorDR: targetArmorDR,
            Cover: CoverLevel.None,
            Distance: effectiveDistance);

        var result = Engine!.ProcessAttack(
            attackerCombat, context, targetHealth, weapon.ApCost);

        // A5: flash target on hit
        if (result is { Hit: true })
        {
            LastHitTarget = targetEntity;
            StartHitFlash(targetEntity);
        }
    }

    // ---- A5: Hit Feedback ----

    private void StartHitFlash(Entity entity)
    {
        // Find the visual child entity with a ModelComponent
        var visual = entity.GetChildren()
            .FirstOrDefault(c => c.Get<ModelComponent>() != null);
        if (visual == null) return;

        var model = visual.Get<ModelComponent>();
        if (model?.Model == null || model.Model.Materials.Count == 0) return;

        // Store original material if not already flashing
        if (_flashingEntities.ContainsKey(entity)) return;

        var originalMat = model.Model.Materials[0];

        // Create flash material once (lazy init)
        _flashMaterial ??= new MaterialInstance(CreateFlashMaterial());

        model.Model.Materials[0] = _flashMaterial;
        _flashingEntities[entity] = (originalMat, FlashDuration);
    }

    private void UpdateHitFlash(float dt)
    {
        var finished = new List<Entity>();

        foreach (var (entity, (original, timer)) in _flashingEntities)
        {
            var newTimer = timer - dt;
            if (newTimer <= 0f)
            {
                // Restore original material
                var visual = entity.GetChildren()
                    .FirstOrDefault(c => c.Get<ModelComponent>() != null);
                var model = visual?.Get<ModelComponent>();
                if (model?.Model != null && model.Model.Materials.Count > 0)
                    model.Model.Materials[0] = original;

                finished.Add(entity);
            }
            else
            {
                _flashingEntities[entity] = (original, newTimer);
            }
        }

        foreach (var entity in finished)
            _flashingEntities.Remove(entity);
    }

    private Material CreateFlashMaterial()
    {
        var materialDesc = new MaterialDescriptor
        {
            Attributes =
            {
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor { Key = MaterialKeys.DiffuseValue })
            }
        };

        var material = Material.New(Game.GraphicsDevice, materialDesc);
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, new Color4(1f, 1f, 1f, 1f));
        return material;
    }

    // ---- A5: Death Cleanup ----

    private void CleanupDead()
    {
        for (var i = Enemies.Count - 1; i >= 0; i--)
        {
            var enemy = Enemies[i];
            if (enemy.Health.IsAlive) continue;

            // Drop loot before removing the entity
            LootDrop?.QueueDrop(enemy.Entity.Transform.Position);

            // Publish death event with entity context for kill tracking
            EventBus?.Publish(new EntityDiedEvent(enemy.Id, enemy.Tag));

            // Remove the entity from the scene
            enemy.Entity.Scene?.Entities.Remove(enemy.Entity);
            enemy.Combat.InCombat = false;

            // Notify CombatStateManager (auto-exits combat if last enemy)
            CombatState!.RemoveCombatant(enemy.Id);

            // Remove from our tracking list
            Enemies.RemoveAt(i);
        }

        // If combat ended (all enemies dead), reset player combat state
        if (!CombatState!.InCombat && PlayerCombat != null)
        {
            PlayerCombat.InCombat = false;
            PlayerCombat.ResetAP();
            Queue!.Clear();
        }
    }
}
