using Oravey2.Core.Dialogue;
using Oravey2.Core.NPC;

namespace Oravey2.Tests.NPC;

public class TownDialogueTreeTests
{
    [Fact]
    public void ElderTree_HasStartNode()
    {
        var tree = TownDialogueTrees.ElderDialogue();
        Assert.Equal("elder_dialogue", tree.Id);
        Assert.Equal("greeting", tree.StartNodeId);
        Assert.True(tree.Nodes.ContainsKey("greeting"));
    }

    [Fact]
    public void ElderTree_GreetingHasChoices()
    {
        var tree = TownDialogueTrees.ElderDialogue();
        var node = tree.Nodes["greeting"];
        Assert.True(node.Choices.Length >= 2);
    }

    [Fact]
    public void ElderTree_QuestSetsFlag()
    {
        var tree = TownDialogueTrees.ElderDialogue();
        var questNode = tree.Nodes["quest_offer"];
        // "I'll help" is the first choice — has StartQuestAction
        var helpChoice = questNode.Choices[0];
        Assert.Single(helpChoice.Consequences);
        Assert.IsType<StartQuestAction>(helpChoice.Consequences[0]);
    }

    [Fact]
    public void MerchantTree_HasBuyChoices()
    {
        var tree = TownDialogueTrees.MerchantDialogue();
        var node = tree.Nodes["greeting"];
        Assert.True(node.Choices.Length >= 3);
        Assert.Contains("Medkit", node.Choices[0].Text);
        Assert.Contains("Leather Jacket", node.Choices[1].Text);
    }

    [Fact]
    public void MerchantTree_LeaveEndsDialogue()
    {
        var tree = TownDialogueTrees.MerchantDialogue();
        var node = tree.Nodes["greeting"];
        var leaveChoice = node.Choices[^1]; // Last choice = "Never mind"
        Assert.Null(leaveChoice.NextNodeId);
    }

    [Fact]
    public void CivilianTree_HasEndChoice()
    {
        var tree = TownDialogueTrees.CivilianDialogue();
        var node = tree.Nodes["greeting"];
        Assert.Single(node.Choices);
        Assert.Null(node.Choices[0].NextNodeId);
    }

    [Fact]
    public void AllTrees_NoDanglingRefs()
    {
        var trees = new[] {
            TownDialogueTrees.ElderDialogue(),
            TownDialogueTrees.MerchantDialogue(),
            TownDialogueTrees.CivilianDialogue(),
        };

        foreach (var tree in trees)
        {
            Assert.True(tree.Nodes.ContainsKey(tree.StartNodeId),
                $"Tree '{tree.Id}' missing start node '{tree.StartNodeId}'");

            foreach (var node in tree.Nodes.Values)
            {
                foreach (var choice in node.Choices)
                {
                    if (choice.NextNodeId != null)
                    {
                        Assert.True(tree.Nodes.ContainsKey(choice.NextNodeId),
                            $"Tree '{tree.Id}' node '{node.Id}' has dangling ref '{choice.NextNodeId}'");
                    }
                }
            }
        }
    }

    [Fact]
    public void GetTree_ResolvesAll()
    {
        Assert.NotNull(TownDialogueTrees.GetTree("elder_dialogue"));
        Assert.NotNull(TownDialogueTrees.GetTree("merchant_dialogue"));
        Assert.NotNull(TownDialogueTrees.GetTree("civilian_dialogue"));
        Assert.Null(TownDialogueTrees.GetTree("nonexistent"));
    }

    [Fact]
    public void GameAction_HasDialogueChoices()
    {
        Assert.True(Enum.IsDefined(typeof(Oravey2.Core.Input.GameAction),
            Oravey2.Core.Input.GameAction.DialogueChoice1));
        Assert.True(Enum.IsDefined(typeof(Oravey2.Core.Input.GameAction),
            Oravey2.Core.Input.GameAction.DialogueChoice4));
    }

    [Fact]
    public void ElderTree_Speaker_IsElderTomas()
    {
        var tree = TownDialogueTrees.ElderDialogue();
        Assert.Equal("Elder Tomas", tree.Nodes["greeting"].Speaker);
    }
}
