using Oravey2.Core.NPC;

namespace Oravey2.Tests.NPC;

public class NpcInteractionTests
{
    [Fact]
    public void NpcInteractionEvent_Constructor()
    {
        var evt = new NpcInteractionEvent("elder", "elder_dialogue");

        Assert.Equal("elder", evt.NpcId);
        Assert.Equal("elder_dialogue", evt.DialogueTreeId);
    }

    [Fact]
    public void NpcInteractionEvent_Equality()
    {
        var a = new NpcInteractionEvent("elder", "elder_dialogue");
        var b = new NpcInteractionEvent("elder", "elder_dialogue");

        Assert.Equal(a, b);
    }

    [Fact]
    public void InteractionTriggerScript_DefaultRadius_IsTwo()
    {
        var script = new InteractionTriggerScript();
        Assert.Equal(2.0f, script.InteractionRadius);
    }

    [Fact]
    public void NpcComponent_HoldsDefinition()
    {
        var def = new NpcDefinition { Id = "merchant", DisplayName = "Mara", Role = NpcRole.Merchant, DialogueTreeId = "merchant_dialogue" };
        var component = new NpcComponent { Definition = def };

        Assert.Equal("merchant", component.Definition.Id);
        Assert.Equal("Mara", component.Definition.DisplayName);
        Assert.Equal(NpcRole.Merchant, component.Definition.Role);
    }
}
