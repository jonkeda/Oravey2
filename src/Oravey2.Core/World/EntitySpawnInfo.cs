namespace Oravey2.Core.World;

/// <summary>
/// Describes an entity to spawn when a chunk loads.
/// Position is local to the chunk (0–15 range per axis).
/// </summary>
public sealed record EntitySpawnInfo(
    string PrefabId,
    float LocalX,
    float LocalZ,
    float RotationY = 0f,
    string? Faction = null,
    int? Level = null,
    string? DialogueId = null,
    string? LootTable = null,
    bool Persistent = false,
    string? ConditionFlag = null
);
