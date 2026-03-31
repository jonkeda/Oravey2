using Oravey2.Core.NPC;

namespace Oravey2.Tests.NPC;

public class NpcDefinitionTests
{
    [Fact]
    public void NpcDefinition_Creates_WithCorrectProperties()
    {
        var def = new NpcDefinition("elder", "Elder Tomas", NpcRole.QuestGiver, "elder_dialogue");

        Assert.Equal("elder", def.Id);
        Assert.Equal("Elder Tomas", def.DisplayName);
        Assert.Equal(NpcRole.QuestGiver, def.Role);
        Assert.Equal("elder_dialogue", def.DialogueTreeId);
    }

    [Fact]
    public void NpcDefinition_QuestGiver_Role()
    {
        var def = new NpcDefinition("elder", "Elder Tomas", NpcRole.QuestGiver, "elder_dialogue");
        Assert.Equal(NpcRole.QuestGiver, def.Role);
    }

    [Fact]
    public void NpcDefinition_Merchant_Role()
    {
        var def = new NpcDefinition("merchant", "Mara", NpcRole.Merchant, "merchant_dialogue");
        Assert.Equal(NpcRole.Merchant, def.Role);
    }

    [Fact]
    public void NpcDefinition_Civilian_Role()
    {
        var def = new NpcDefinition("civilian_1", "Settler", NpcRole.Civilian, "civilian_dialogue");
        Assert.Equal(NpcRole.Civilian, def.Role);
    }

    [Fact]
    public void NpcDefinition_Equality()
    {
        var a = new NpcDefinition("elder", "Elder Tomas", NpcRole.QuestGiver, "elder_dialogue");
        var b = new NpcDefinition("elder", "Elder Tomas", NpcRole.QuestGiver, "elder_dialogue");

        Assert.Equal(a, b);
    }
}
