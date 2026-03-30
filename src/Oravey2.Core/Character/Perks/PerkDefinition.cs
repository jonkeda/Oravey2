namespace Oravey2.Core.Character.Perks;

public sealed record PerkDefinition(
    string Id,
    string Name,
    string Description,
    PerkCondition Condition,
    string[] Effects,
    string[]? MutuallyExclusive = null);
