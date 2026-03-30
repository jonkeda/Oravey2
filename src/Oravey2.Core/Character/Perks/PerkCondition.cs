using Oravey2.Core.Character.Stats;

namespace Oravey2.Core.Character.Perks;

public sealed record PerkCondition(
    int RequiredLevel,
    Stat? RequiredStat = null,
    int? StatThreshold = null,
    string? RequiredPerk = null);
