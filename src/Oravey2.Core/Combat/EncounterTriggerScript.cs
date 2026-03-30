using Oravey2.Core.Framework.State;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Oravey2.Core.Combat;

public class EncounterTriggerScript : SyncScript
{
    /// <summary>
    /// Distance in world units at which enemies trigger combat.
    /// </summary>
    public float TriggerRadius { get; set; } = 8f;

    /// <summary>
    /// The player entity to measure distance from.
    /// </summary>
    public Entity? Player { get; set; }

    /// <summary>
    /// Reference to the game state manager for state checks.
    /// </summary>
    public GameStateManager? StateManager { get; set; }

    /// <summary>
    /// The combat state manager to call EnterCombat on.
    /// </summary>
    public CombatStateManager? CombatState { get; set; }

    /// <summary>
    /// Live list of enemies — entries are removed by CombatSyncScript on death.
    /// </summary>
    internal List<EnemyInfo> Enemies { get; set; } = [];

    public override void Start() { }

    public override void Update()
    {
        if (Player == null || StateManager == null || CombatState == null)
            return;

        // Only trigger when exploring — don't re-trigger while already fighting
        if (StateManager.CurrentState != GameState.Exploring)
            return;

        var playerPos = Player.Transform.Position;

        foreach (var enemy in Enemies)
        {
            var dist = Vector3.Distance(playerPos, enemy.Entity.Transform.Position);
            if (dist <= TriggerRadius)
            {
                var ids = Enemies
                    .Where(e => e.Health.IsAlive)
                    .Select(e => e.Id)
                    .ToArray();

                if (ids.Length > 0)
                {
                    // Mark all combatants as in-combat for AP regen
                    foreach (var e in Enemies)
                        e.Combat.InCombat = true;

                    CombatState.EnterCombat(ids);
                }
                return; // Only need one trigger
            }
        }
    }
}
