namespace Oravey2.Core.Combat;

public sealed record DamageResult(
    bool Hit,
    int Damage,
    HitLocation Location,
    bool Critical);
