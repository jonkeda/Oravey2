namespace Oravey2.Core.Dialogue;

public sealed record DialogueNode(
    string Id,
    string Speaker,
    string Text,
    string? Portrait,
    DialogueChoice[] Choices);

public sealed record DialogueChoice(
    string Text,
    string? NextNodeId,
    IDialogueCondition? Condition,
    IConsequenceAction[] Consequences);

public sealed record DialogueTree(
    string Id,
    string StartNodeId,
    IReadOnlyDictionary<string, DialogueNode> Nodes);
