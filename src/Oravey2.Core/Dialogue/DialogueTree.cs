namespace Oravey2.Core.Dialogue;

public sealed class DialogueNode
{
    public string Id { get; set; } = "";
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Portrait { get; set; }
    public DialogueChoice[] Choices { get; set; } = [];
}

public sealed class DialogueChoice
{
    public string Text { get; set; } = "";
    public string? NextNodeId { get; set; }
    public IDialogueCondition? Condition { get; set; }
    public IConsequenceAction[] Consequences { get; set; } = [];
}

public sealed class DialogueTree
{
    public string Id { get; set; } = "";
    public string StartNodeId { get; set; } = "";
    public IReadOnlyDictionary<string, DialogueNode> Nodes { get; set; } = new Dictionary<string, DialogueNode>();
}
