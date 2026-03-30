namespace Oravey2.Core.AI;

public sealed class AIBlackboard
{
    public (float X, float Y)? LastKnownTargetPosition { get; set; }
    public string? CurrentTargetId { get; set; }
    public float TimeSinceLastSeen { get; set; }

    public float ThreatLevel { get; set; }
    public bool UnderFire { get; set; }

    public AIState CurrentState { get; set; } = AIState.Idle;
    public bool HasAmmo { get; set; } = true;
    public float HealthPercent { get; set; } = 1.0f;

    public int AllyCount { get; set; }
    public int EnemyCount { get; set; }

    public bool CoverNearby { get; set; }

    private readonly Dictionary<string, object> _customData = new();
    public IReadOnlyDictionary<string, object> CustomData => _customData;

    public void SetCustom(string key, object value) => _customData[key] = value;

    public T? GetCustom<T>(string key)
        => _customData.TryGetValue(key, out var val) && val is T typed ? typed : default;

    public void ResetTransient()
    {
        UnderFire = false;
    }
}
