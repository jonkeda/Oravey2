namespace Oravey2.Core.NPC;

public enum NpcRole { QuestGiver, Merchant, Civilian }

public record NpcDefinition(
    string Id,
    string DisplayName,
    NpcRole Role,
    string DialogueTreeId);
