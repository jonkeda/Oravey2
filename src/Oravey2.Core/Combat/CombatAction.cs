namespace Oravey2.Core.Combat;

public sealed record CombatAction(
    string ActorId,
    CombatActionType Type,
    string? TargetId = null);
