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
            // --- Main greeting: shows state-gated choices ---
            ["greeting"] = new DialogueNode {
                Id = "greeting",
                Speaker = "Elder Tomas",
                Text = "I'm Elder Tomas, leader of this settlement. We've been holding on, but times are tough.",
                Portrait = "green",
                Choices =
                [
                    // Q1 offer: no quest active, Q1 not done
                    new DialogueChoice { Text = "Any work?", NextNodeId = "quest_offer_1",
                        Condition = new AndCondition(
                            new FlagCondition("q_rat_hunt_active", expected: false),
                            new FlagCondition("q_rat_hunt_done", expected: false)),
                        Consequences = [] },

                    // Q1 active, rats cleared → turn in
                    new DialogueChoice { Text = "The rats are dead.", NextNodeId = "quest_1_complete",
                        Condition = new AndCondition(
                            new FlagCondition("q_rat_hunt_active"),
                            new FlagCondition("rats_cleared")),
                        Consequences = [] },

                    // Q1 active, rats NOT cleared → in-progress
                    new DialogueChoice { Text = "Still working on it.", NextNodeId = null,
                        Condition = new AndCondition(
                            new FlagCondition("q_rat_hunt_active"),
                            new FlagCondition("rats_cleared", expected: false)),
                        Consequences = [] },

                    // Q2 offer: Q1 done, Q2 not started/done
                    new DialogueChoice { Text = "Got another job?", NextNodeId = "quest_offer_2",
                        Condition = new AndCondition(
                            new FlagCondition("q_rat_hunt_done"),
                            new FlagCondition("q_raider_camp_active", expected: false),
                            new FlagCondition("q_raider_camp_done", expected: false)),
                        Consequences = [] },

                    // Q2 active, Scar not dead → in-progress
                    new DialogueChoice { Text = "Working on the camp.", NextNodeId = null,
                        Condition = new AndCondition(
                            new FlagCondition("q_raider_camp_active"),
                            new FlagCondition("scar_killed", expected: false)),
                        Consequences = [] },

                    // Q3: report back after camp cleared
                    new DialogueChoice { Text = "The camp is clear.", NextNodeId = "quest_3_complete",
                        Condition = new FlagCondition("q_safe_passage_active"),
                        Consequences = [] },

                    // Post-completion
                    new DialogueChoice { Text = "How are things?", NextNodeId = "post_complete",
                        Condition = new FlagCondition("m1_complete"),
                        Consequences = [] },

                    // Always available
                    new DialogueChoice { Text = "Goodbye.", NextNodeId = null, Condition = null, Consequences = [] },
                ] },

            // --- Q1 offer ---
            ["quest_offer_1"] = new DialogueNode {
                Id = "quest_offer_1",
                Speaker = "Elder Tomas",
                Text = "Radrats have been attacking scavengers near the road. Kill three of them and I'll make it worth your while.",
                Portrait = "green",
                Choices =
                [
                    new DialogueChoice { Text = "I'll handle it.", NextNodeId = null, Condition = null,
                        Consequences = [new StartQuestAction("q_rat_hunt")] },
                    new DialogueChoice { Text = "Not now.", NextNodeId = null, Condition = null, Consequences = [] },
                ] },

            // --- Q1 turn-in ---
            ["quest_1_complete"] = new DialogueNode {
                Id = "quest_1_complete",
                Speaker = "Elder Tomas",
                Text = "Excellent. The road should be safer now. Here's your reward.",
                Portrait = "green",
                Choices =
                [
                    new DialogueChoice { Text = "Thanks.", NextNodeId = null, Condition = null,
                        Consequences =
                        [
                            new SetFlagAction("q_rat_hunt_reported"),
                            new GiveCapsAction(15),
                        ] },
                ] },

            // --- Q2 offer ---
            ["quest_offer_2"] = new DialogueNode {
                Id = "quest_offer_2",
                Speaker = "Elder Tomas",
                Text = "Good work with the rats. But there's a bigger problem. Raiders set up camp in the eastern ruins. Their leader is a nasty piece of work called Scar. Take him out.",
                Portrait = "green",
                Choices =
                [
                    new DialogueChoice { Text = "Consider it done.", NextNodeId = null, Condition = null,
                        Consequences = [new StartQuestAction("q_raider_camp")] },
                    new DialogueChoice { Text = "I need to prepare first.", NextNodeId = null, Condition = null, Consequences = [] },
                ] },

            // --- Q3 turn-in ---
            ["quest_3_complete"] = new DialogueNode {
                Id = "quest_3_complete",
                Speaker = "Elder Tomas",
                Text = "You've done it. Haven is safer because of you. We won't forget this.",
                Portrait = "green",
                Choices =
                [
                    new DialogueChoice { Text = "Glad to help.", NextNodeId = null, Condition = null,
                        Consequences =
                        [
                            new SetFlagAction("reported_to_elder"),
                            new GiveCapsAction(50),
                        ] },
                ] },

            // --- Post-completion ---
            ["post_complete"] = new DialogueNode {
                Id = "post_complete",
                Speaker = "Elder Tomas",
                Text = "Haven is thriving thanks to you, stranger. You're always welcome here.",
                Portrait = "green",
                Choices =
                [
                    new DialogueChoice { Text = "Take care.", NextNodeId = null, Condition = null, Consequences = [] },
                ] },
        };

        return new DialogueTree { Id = "elder_dialogue", StartNodeId = "greeting", Nodes = nodes };
    }

    public static DialogueTree MerchantDialogue()
    {
        var nodes = new Dictionary<string, DialogueNode>
        {
            ["greeting"] = new DialogueNode {
                Id = "greeting",
                Speaker = "Mara",
                Text = "Welcome to my stall. I've got supplies if you've got caps.",
                Portrait = "blue",
                Choices =
                [
                    new DialogueChoice { Text = "Buy Medkit (10 caps)", NextNodeId = "greeting", Condition = null,
                        Consequences = [new BuyItemAction("medkit", 10)] },
                    new DialogueChoice { Text = "Buy Leather Jacket (25 caps)", NextNodeId = "greeting", Condition = null,
                        Consequences = [new BuyItemAction("leather_jacket", 25)] },
                    new DialogueChoice { Text = "Sell Scrap Metal (5 caps)", NextNodeId = "greeting", Condition = null,
                        Consequences = [new SellItemAction("scrap_metal", 5)] },
                    new DialogueChoice { Text = "Never mind.", NextNodeId = null, Condition = null, Consequences = [] },
                ] },
        };

        return new DialogueTree { Id = "merchant_dialogue", StartNodeId = "greeting", Nodes = nodes };
    }

    public static DialogueTree CivilianDialogue()
    {
        var nodes = new Dictionary<string, DialogueNode>
        {
            ["greeting"] = new DialogueNode {
                Id = "greeting",
                Speaker = "Settler",
                Text = "Stay safe out there, stranger.",
                Portrait = "gray",
                Choices =
                [
                    new DialogueChoice { Text = "Thanks.", NextNodeId = null, Condition = null, Consequences = [] },
                ] },
        };

        return new DialogueTree { Id = "civilian_dialogue", StartNodeId = "greeting", Nodes = nodes };
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
