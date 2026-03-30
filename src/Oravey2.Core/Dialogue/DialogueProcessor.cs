namespace Oravey2.Core.Dialogue;

using Oravey2.Core.Framework.Events;

public sealed class DialogueProcessor
{
    private readonly IEventBus _eventBus;

    public DialogueTree? ActiveTree { get; private set; }
    public DialogueNode? CurrentNode { get; private set; }
    public bool IsActive => ActiveTree != null;

    public DialogueProcessor(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void StartDialogue(DialogueTree tree)
    {
        ActiveTree = tree;
        CurrentNode = tree.Nodes[tree.StartNodeId];
        _eventBus.Publish(new DialogueStartedEvent(tree.Id));
    }

    public IReadOnlyList<(DialogueChoice Choice, bool Available)> GetAvailableChoices(
        DialogueContext context)
    {
        if (CurrentNode == null)
            return [];

        var result = new List<(DialogueChoice, bool)>();
        foreach (var choice in CurrentNode.Choices)
        {
            var available = choice.Condition?.Evaluate(context) ?? true;
            result.Add((choice, available));
        }
        return result;
    }

    public bool SelectChoice(int index, DialogueContext context)
    {
        if (CurrentNode == null || index < 0 || index >= CurrentNode.Choices.Length)
            return false;

        var choice = CurrentNode.Choices[index];

        if (choice.Condition != null && !choice.Condition.Evaluate(context))
            return false;

        foreach (var consequence in choice.Consequences)
            consequence.Execute(context);

        if (choice.NextNodeId == null)
        {
            EndDialogue();
        }
        else
        {
            CurrentNode = ActiveTree!.Nodes[choice.NextNodeId];
        }

        return true;
    }

    public void EndDialogue()
    {
        if (ActiveTree == null) return;

        var treeId = ActiveTree.Id;
        ActiveTree = null;
        CurrentNode = null;
        _eventBus.Publish(new DialogueEndedEvent(treeId));
    }
}
