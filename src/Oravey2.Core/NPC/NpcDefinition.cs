namespace Oravey2.Core.NPC;

public enum NpcRole { QuestGiver, Merchant, Civilian }

public sealed class NpcDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public NpcRole Role { get; set; }
    public string DialogueTreeId { get; set; } = "";
}
