namespace Oravey2.Core.World;

/// <summary>
/// Describes an entity to spawn when a chunk loads.
/// Position is local to the chunk (0–15 range per axis).
/// </summary>
public sealed class EntitySpawnInfo
{
    public string PrefabId { get; set; } = "";
    public float LocalX { get; set; }
    public float LocalZ { get; set; }
    public float RotationY { get; set; }
    public string? Faction { get; set; }
    public int? Level { get; set; }
    public string? DialogueId { get; set; }
    public string? LootTable { get; set; }
    public bool Persistent { get; set; }
    public string? ConditionFlag { get; set; }
}
