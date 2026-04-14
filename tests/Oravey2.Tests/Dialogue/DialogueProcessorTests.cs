using Oravey2.Core.Character.Stats;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Dialogue;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.World;

namespace Oravey2.Tests.Dialogue;

public class DialogueProcessorTests
{
    private static (DialogueProcessor proc, DialogueContext ctx, EventBus bus) Setup()
    {
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);
        var inv = new InventoryComponent(stats);
        var world = new WorldStateService();
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        var ctx = new DialogueContext(skills, inv, world, level, bus);
        var proc = new DialogueProcessor(bus);
        return (proc, ctx, bus);
    }

    private static DialogueTree MakeSimpleTree()
    {
        var nodes = new Dictionary<string, DialogueNode>
        {
            ["start"] = new DialogueNode { Id = "start", Speaker = "NPC", Text = "Hello!", Choices =
            [
                new DialogueChoice { Text = "Go to next", NextNodeId = "middle", Consequences = [] },
                new DialogueChoice { Text = "Goodbye", Consequences = [] },
            ] },
            ["middle"] = new DialogueNode { Id = "middle", Speaker = "NPC", Text = "Middle node", Choices =
            [
                new DialogueChoice { Text = "End", Consequences = [] },
            ] },
        };
        return new DialogueTree { Id = "test_tree", StartNodeId = "start", Nodes = nodes };
    }

    [Fact]
    public void StartDialogue_SetsActiveTree()
    {
        var (proc, _, _) = Setup();
        var tree = MakeSimpleTree();
        proc.StartDialogue(tree);
        Assert.Same(tree, proc.ActiveTree);
    }

    [Fact]
    public void StartDialogue_SetsCurrentNodeToStart()
    {
        var (proc, _, _) = Setup();
        var tree = MakeSimpleTree();
        proc.StartDialogue(tree);
        Assert.Equal("start", proc.CurrentNode!.Id);
    }

    [Fact]
    public void StartDialogue_PublishesDialogueStartedEvent()
    {
        var (proc, _, bus) = Setup();
        DialogueStartedEvent? received = null;
        bus.Subscribe<DialogueStartedEvent>(e => received = e);

        proc.StartDialogue(MakeSimpleTree());

        Assert.NotNull(received);
        Assert.Equal("test_tree", received.Value.TreeId);
    }

    [Fact]
    public void StartDialogue_IsActive_True()
    {
        var (proc, _, _) = Setup();
        proc.StartDialogue(MakeSimpleTree());
        Assert.True(proc.IsActive);
    }

    [Fact]
    public void GetAvailableChoices_NoConditions_AllAvailable()
    {
        var (proc, ctx, _) = Setup();
        proc.StartDialogue(MakeSimpleTree());
        var choices = proc.GetAvailableChoices(ctx);
        Assert.Equal(2, choices.Count);
        Assert.All(choices, c => Assert.True(c.Available));
    }

    [Fact]
    public void GetAvailableChoices_FailedCondition_MarkedUnavailable()
    {
        var (proc, ctx, _) = Setup();
        // Speech=20 (default Charisma=5), threshold=40 → fails
        var conditionedChoice = new DialogueChoice
        {
            Text = "[Speech 40] Persuade", NextNodeId = "middle",
            Condition = new SkillCheckCondition(SkillType.Speech, 40), Consequences = [],
        };
        var normalChoice = new DialogueChoice { Text = "Normal", NextNodeId = "middle", Consequences = [] };

        var nodes = new Dictionary<string, DialogueNode>
        {
            ["start"] = new DialogueNode { Id = "start", Speaker = "NPC", Text = "Hello!", Choices = [conditionedChoice, normalChoice] },
            ["middle"] = new DialogueNode { Id = "middle", Speaker = "NPC", Text = "Ok", Choices = [] },
        };
        var tree = new DialogueTree { Id = "test", StartNodeId = "start", Nodes = nodes };

        proc.StartDialogue(tree);
        var choices = proc.GetAvailableChoices(ctx);

        Assert.False(choices[0].Available);
        Assert.True(choices[1].Available);
    }

    [Fact]
    public void GetAvailableChoices_PassedCondition_Available()
    {
        var (proc, ctx, _) = Setup();
        // Speech=20, threshold=20 → passes
        var conditionedChoice = new DialogueChoice
        {
            Text = "[Speech 20] Persuade", NextNodeId = "middle",
            Condition = new SkillCheckCondition(SkillType.Speech, 20), Consequences = [],
        };

        var nodes = new Dictionary<string, DialogueNode>
        {
            ["start"] = new DialogueNode { Id = "start", Speaker = "NPC", Text = "Hello!", Choices = [conditionedChoice] },
            ["middle"] = new DialogueNode { Id = "middle", Speaker = "NPC", Text = "Ok", Choices = [] },
        };
        var tree = new DialogueTree { Id = "test", StartNodeId = "start", Nodes = nodes };

        proc.StartDialogue(tree);
        var choices = proc.GetAvailableChoices(ctx);
        Assert.True(choices[0].Available);
    }

    [Fact]
    public void GetAvailableChoices_NotActive_ReturnsEmpty()
    {
        var (proc, ctx, _) = Setup();
        var choices = proc.GetAvailableChoices(ctx);
        Assert.Empty(choices);
    }

    [Fact]
    public void SelectChoice_ValidIndex_AdvancesToNextNode()
    {
        var (proc, ctx, _) = Setup();
        proc.StartDialogue(MakeSimpleTree());
        proc.SelectChoice(0, ctx); // "Go to next" → middle
        Assert.Equal("middle", proc.CurrentNode!.Id);
    }

    [Fact]
    public void SelectChoice_InvalidIndex_ReturnsFalse()
    {
        var (proc, ctx, _) = Setup();
        proc.StartDialogue(MakeSimpleTree());
        Assert.False(proc.SelectChoice(-1, ctx));
        Assert.False(proc.SelectChoice(99, ctx));
    }

    [Fact]
    public void SelectChoice_UnavailableCondition_ReturnsFalse()
    {
        var (proc, ctx, _) = Setup();
        var conditionedChoice = new DialogueChoice
        {
            Text = "[Speech 99] Impossible",
            Condition = new SkillCheckCondition(SkillType.Speech, 99), Consequences = [],
        };

        var nodes = new Dictionary<string, DialogueNode>
        {
            ["start"] = new DialogueNode { Id = "start", Speaker = "NPC", Text = "Hello!", Choices = [conditionedChoice] },
        };
        var tree = new DialogueTree { Id = "test", StartNodeId = "start", Nodes = nodes };
        proc.StartDialogue(tree);

        Assert.False(proc.SelectChoice(0, ctx));
        Assert.Equal("start", proc.CurrentNode!.Id); // stays on same node
    }

    [Fact]
    public void SelectChoice_ExecutesConsequences()
    {
        var (proc, ctx, _) = Setup();
        var flagAction = new SetFlagAction("choice_made");
        var choice = new DialogueChoice { Text = "Do it", Consequences = [flagAction] };

        var nodes = new Dictionary<string, DialogueNode>
        {
            ["start"] = new DialogueNode { Id = "start", Speaker = "NPC", Text = "Hello!", Choices = [choice] },
        };
        var tree = new DialogueTree { Id = "test", StartNodeId = "start", Nodes = nodes };
        proc.StartDialogue(tree);
        proc.SelectChoice(0, ctx);

        Assert.True(ctx.WorldState.GetFlag("choice_made"));
    }

    [Fact]
    public void SelectChoice_NullNextNode_EndsDialogue()
    {
        var (proc, ctx, _) = Setup();
        proc.StartDialogue(MakeSimpleTree());
        proc.SelectChoice(1, ctx); // "Goodbye" → null → end
        Assert.False(proc.IsActive);
    }

    [Fact]
    public void SelectChoice_NotActive_ReturnsFalse()
    {
        var (proc, ctx, _) = Setup();
        Assert.False(proc.SelectChoice(0, ctx));
    }

    [Fact]
    public void EndDialogue_ClearsState()
    {
        var (proc, _, _) = Setup();
        proc.StartDialogue(MakeSimpleTree());
        proc.EndDialogue();
        Assert.Null(proc.ActiveTree);
        Assert.Null(proc.CurrentNode);
        Assert.False(proc.IsActive);
    }

    [Fact]
    public void EndDialogue_PublishesDialogueEndedEvent()
    {
        var (proc, _, bus) = Setup();
        proc.StartDialogue(MakeSimpleTree());

        DialogueEndedEvent? received = null;
        bus.Subscribe<DialogueEndedEvent>(e => received = e);
        proc.EndDialogue();

        Assert.NotNull(received);
        Assert.Equal("test_tree", received.Value.TreeId);
    }

    [Fact]
    public void EndDialogue_WhenNotActive_NoEvent()
    {
        var (proc, _, bus) = Setup();
        DialogueEndedEvent? received = null;
        bus.Subscribe<DialogueEndedEvent>(e => received = e);
        proc.EndDialogue();
        Assert.Null(received);
    }
}
