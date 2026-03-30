namespace Oravey2.Core.Crafting;

using Oravey2.Core.Character.Skills;

public sealed record RecipeDefinition(
    string Id,
    string Name,
    string OutputItemId,
    int OutputCount,
    IReadOnlyDictionary<string, int> Ingredients,
    StationType RequiredStation,
    SkillType? RequiredSkill = null,
    int SkillThreshold = 0);
