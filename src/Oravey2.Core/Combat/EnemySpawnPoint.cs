namespace Oravey2.Core.Combat;

/// <summary>
/// Defines an enemy spawn point in a zone. Used by EnemySpawner to place enemies.
/// </summary>
public record EnemySpawnPoint(
    string GroupId,
    float X,
    float Z,
    int Count,
    int Endurance,
    int Luck,
    int WeaponDamage,
    float WeaponAccuracy,
    string? Tag = null,
    string? RequiredQuestId = null,
    string? RequiredQuestStage = null);
