namespace Oravey2.Core.AI.Utility;

public static class CombatConsiderations
{
    public static AIActionDefinition Attack() => new("Attack",
    [
        new("weapon_available", bb => bb.HasAmmo ? 1f : 0f, 0.3f),
        new("target_in_range", bb => bb.CurrentTargetId != null ? 1f : 0f, 0.4f),
        new("health_ok", bb => bb.HealthPercent > 0.30f ? 1f : 0f, 0.3f),
    ]);

    public static AIActionDefinition Flee() => new("Flee",
    [
        new("low_health", bb => bb.HealthPercent < 0.25f ? 1f : 0f, 0.5f),
        new("outnumbered", bb => bb.EnemyCount > bb.AllyCount ? 1f : 0f, 0.3f),
        new("no_ammo", bb => !bb.HasAmmo ? 1f : 0f, 0.2f),
    ]);

    public static AIActionDefinition TakeCover() => new("TakeCover",
    [
        new("under_fire", bb => bb.UnderFire ? 1f : 0f, 0.4f),
        new("cover_nearby", bb => bb.CoverNearby ? 1f : 0f, 0.4f),
        new("health_low", bb => bb.HealthPercent < 0.50f ? 1f : 0f, 0.2f),
    ]);

    public static AIActionDefinition Investigate() => new("Investigate",
    [
        new("heard_noise", bb => bb.ThreatLevel > 0f && bb.CurrentTargetId == null ? 1f : 0f, 0.5f),
        new("lost_target", bb => bb.TimeSinceLastSeen > 0f && bb.CurrentTargetId != null ? 1f : 0f, 0.5f),
    ]);

    public static AIActionDefinition Patrol() => new("Patrol",
    [
        new("no_threats", bb => bb.ThreatLevel == 0f ? 1f : 0f, 0.6f),
        new("at_waypoint", bb => bb.CurrentTargetId == null ? 1f : 0f, 0.4f),
    ]);

    public static AIActionDefinition[] AllCombatActions() =>
    [
        Attack(), Flee(), TakeCover(), Investigate(), Patrol()
    ];
}
