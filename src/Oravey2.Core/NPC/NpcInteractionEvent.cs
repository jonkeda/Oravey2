using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.NPC;

public record NpcInteractionEvent(string NpcId, string DialogueTreeId) : IGameEvent;
