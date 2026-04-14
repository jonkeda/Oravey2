namespace Oravey2.Core.Crafting;

using Oravey2.Core.Character.Skills;

public sealed class RecipeDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string OutputItemId { get; set; } = "";
    public int OutputCount { get; set; }
    public IReadOnlyDictionary<string, int> Ingredients { get; set; } = new Dictionary<string, int>();
    public StationType RequiredStation { get; set; }
    public SkillType? RequiredSkill { get; set; }
    public int SkillThreshold { get; set; }
}
