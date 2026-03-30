namespace Oravey2.Core.Dialogue;

using Oravey2.Core.Character.Skills;

public interface IDialogueCondition
{
    bool Evaluate(DialogueContext context);
}

public sealed class SkillCheckCondition : IDialogueCondition
{
    public SkillType Skill { get; }
    public int Threshold { get; }
    public bool Hidden { get; }

    public SkillCheckCondition(SkillType skill, int threshold, bool hidden = false)
    {
        Skill = skill;
        Threshold = threshold;
        Hidden = hidden;
    }

    public bool Evaluate(DialogueContext context)
        => context.Skills.GetEffective(Skill) >= Threshold;
}

public sealed class FlagCondition : IDialogueCondition
{
    public string Flag { get; }
    public bool Expected { get; }

    public FlagCondition(string flag, bool expected = true)
    {
        Flag = flag;
        Expected = expected;
    }

    public bool Evaluate(DialogueContext context)
        => context.WorldState.GetFlag(Flag) == Expected;
}

public sealed class ItemCondition : IDialogueCondition
{
    public string ItemId { get; }
    public int Count { get; }

    public ItemCondition(string itemId, int count = 1)
    {
        ItemId = itemId;
        Count = count;
    }

    public bool Evaluate(DialogueContext context)
        => context.Inventory.Contains(ItemId, Count);
}

public sealed class LevelCondition : IDialogueCondition
{
    public int MinLevel { get; }

    public LevelCondition(int minLevel)
    {
        MinLevel = minLevel;
    }

    public bool Evaluate(DialogueContext context)
        => context.Level.Level >= MinLevel;
}
