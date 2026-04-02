namespace Oravey2.Core.Dialogue;

public sealed class AndCondition : IDialogueCondition
{
    public IDialogueCondition[] Conditions { get; }

    public AndCondition(params IDialogueCondition[] conditions)
    {
        Conditions = conditions;
    }

    public bool Evaluate(DialogueContext context)
    {
        foreach (var c in Conditions)
        {
            if (!c.Evaluate(context))
                return false;
        }
        return true;
    }
}
