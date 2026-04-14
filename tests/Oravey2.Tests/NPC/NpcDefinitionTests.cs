using Oravey2.Core.NPC;

namespace Oravey2.Tests.NPC;

public class NpcDefinitionTests
{
    [Fact]
    public void NpcDefinition_Creates_WithCorrectProperties()
    {
        var def = new NpcDefinition { Id = "elder", DisplayName = "Elder Tomas", Role = NpcRole.QuestGiver, DialogueTreeId = "elder_dialogue" };

        Assert.Equal("elder", def.Id);
        Assert.Equal("Elder Tomas", def.DisplayName);
        Assert.Equal(NpcRole.QuestGiver, def.Role);
        Assert.Equal("elder_dialogue", def.DialogueTreeId);
    }

    [Fact]
    public void NpcDefinition_QuestGiver_Role()
    {
        var def = new NpcDefinition { Id = "elder", DisplayName = "Elder Tomas", Role = NpcRole.QuestGiver, DialogueTreeId = "elder_dialogue" };
        Assert.Equal(NpcRole.QuestGiver, def.Role);
    }

    [Fact]
    public void NpcDefinition_Merchant_Role()
    {
        var def = new NpcDefinition { Id = "merchant", DisplayName = "Mara", Role = NpcRole.Merchant, DialogueTreeId = "merchant_dialogue" };
        Assert.Equal(NpcRole.Merchant, def.Role);
    }

    [Fact]
    public void NpcDefinition_Civilian_Role()
    {
        var def = new NpcDefinition { Id = "civilian_1", DisplayName = "Settler", Role = NpcRole.Civilian, DialogueTreeId = "civilian_dialogue" };
        Assert.Equal(NpcRole.Civilian, def.Role);
    }

    [Fact]
    public void NpcDefinition_Equality()
    {
        var a = new NpcDefinition { Id = "elder", DisplayName = "Elder Tomas", Role = NpcRole.QuestGiver, DialogueTreeId = "elder_dialogue" };
        var b = new NpcDefinition { Id = "elder", DisplayName = "Elder Tomas", Role = NpcRole.QuestGiver, DialogueTreeId = "elder_dialogue" };

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.DisplayName, b.DisplayName);
        Assert.Equal(a.Role, b.Role);
        Assert.Equal(a.DialogueTreeId, b.DialogueTreeId);
    }
}
