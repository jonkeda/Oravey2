using Oravey2.Core.Combat;

namespace Oravey2.Tests.Combat;

public class EnemySpawnPointTests
{
    [Fact]
    public void EnemySpawnPoint_Defaults_OptionalFields()
    {
        var sp = new EnemySpawnPoint("grp", 1f, 2f, 1, 2, 3, 5, 0.5f);
        Assert.Null(sp.Tag);
        Assert.Null(sp.RequiredQuestId);
        Assert.Null(sp.RequiredQuestStage);
    }

    [Fact]
    public void EnemySpawnPoint_Tag_Preserved()
    {
        var sp = new EnemySpawnPoint("rats", 0f, 0f, 3, 1, 3, 4, 0.5f, Tag: "radrat");
        Assert.Equal("radrat", sp.Tag);
        Assert.Equal(3, sp.Count);
    }

    [Fact]
    public void EnemySpawnPoint_QuestGated()
    {
        var sp = new EnemySpawnPoint("boss", 10f, 0f, 1, 3, 5, 8, 0.65f,
            Tag: "scar", RequiredQuestId: "q_raider_camp", RequiredQuestStage: "kill_scar");
        Assert.Equal("q_raider_camp", sp.RequiredQuestId);
        Assert.Equal("kill_scar", sp.RequiredQuestStage);
    }
}
