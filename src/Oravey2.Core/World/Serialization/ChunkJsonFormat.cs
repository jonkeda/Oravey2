namespace Oravey2.Core.World.Serialization;

public sealed record ChunkJson(
    int ChunkX,
    int ChunkY,
    int[][] Surface,
    int[][] Height,
    int[][] Water,
    int[][] Structure,
    int[][] Flags,
    int[][] Variant,
    EntitySpawnJson[]? Entities
);

public sealed record EntitySpawnJson(
    string PrefabId,
    float LocalX,
    float LocalZ,
    float RotationY,
    string? Faction,
    int? Level,
    string? DialogueId,
    string? LootTable,
    bool Persistent,
    string? ConditionFlag
);
