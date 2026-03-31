using Oravey2.Core.Dialogue;

namespace Oravey2.Core.NPC;

/// <summary>
/// Builds the dialogue trees for all town NPCs.
/// </summary>
public static class TownDialogueTrees
{
    public static DialogueTree ElderDialogue()
    {
        var nodes = new Dictionary<string, DialogueNode>
        {
            ["greeting"] = new DialogueNode(
                Id: "greeting",
                Speaker: "Elder Tomas",
                Text: "I'm Elder Tomas, leader of this settlement. We've been holding on, but times are tough.",
                Portrait: "green",
                Choices:
                [
                    new DialogueChoice("What's going on?", NextNodeId: "quest_offer", Condition: null, Consequences: []),
                    new DialogueChoice("Goodbye.", NextNodeId: null, Condition: null, Consequences: []),
                ]),
            ["quest_offer"] = new DialogueNode(
                Id: "quest_offer",
                Speaker: "Elder Tomas",
                Text: "Radrats have been attacking our supply runs to the east. If you could clear them out, we'd be grateful.",
                Portrait: "green",
                Choices:
                [
                    new DialogueChoice("I'll help.", NextNodeId: null, Condition: null,
                        Consequences: [new StartQuestAction("clear_radrats")]),
                    new DialogueChoice("Not now.", NextNodeId: null, Condition: null, Consequences: []),
                ]),
        };

        return new DialogueTree("elder_dialogue", "greeting", nodes);
    }

    public static DialogueTree MerchantDialogue()
    {
        var nodes = new Dictionary<string, DialogueNode>
        {
            ["greeting"] = new DialogueNode(
                Id: "greeting",
                Speaker: "Mara",
                Text: "Welcome to my stall. I've got supplies if you've got caps.",
                Portrait: "blue",
                Choices:
                [
                    new DialogueChoice("Buy Medkit (10 caps)", NextNodeId: "greeting", Condition: null,
                        Consequences: [new BuyItemAction("medkit", 10)]),
                    new DialogueChoice("Buy Leather Jacket (25 caps)", NextNodeId: "greeting", Condition: null,
                        Consequences: [new BuyItemAction("leather_jacket", 25)]),
                    new DialogueChoice("Sell Scrap Metal (5 caps)", NextNodeId: "greeting", Condition: null,
                        Consequences: [new SellItemAction("scrap_metal", 5)]),
                    new DialogueChoice("Never mind.", NextNodeId: null, Condition: null, Consequences: []),
                ]),
        };

        return new DialogueTree("merchant_dialogue", "greeting", nodes);
    }

    public static DialogueTree CivilianDialogue()
    {
        var nodes = new Dictionary<string, DialogueNode>
        {
            ["greeting"] = new DialogueNode(
                Id: "greeting",
                Speaker: "Settler",
                Text: "Stay safe out there, stranger.",
                Portrait: "gray",
                Choices:
                [
                    new DialogueChoice("Thanks.", NextNodeId: null, Condition: null, Consequences: []),
                ]),
        };

        return new DialogueTree("civilian_dialogue", "greeting", nodes);
    }

    /// <summary>
    /// Resolves a dialogue tree ID to the corresponding tree.
    /// </summary>
    public static DialogueTree? GetTree(string treeId)
    {
        return treeId switch
        {
            "elder_dialogue" => ElderDialogue(),
            "merchant_dialogue" => MerchantDialogue(),
            "civilian_dialogue" => CivilianDialogue(),
            _ => null,
        };
    }
}
