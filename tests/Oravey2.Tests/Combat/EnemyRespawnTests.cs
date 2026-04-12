using Oravey2.Core.Combat;
using Oravey2.Core.Quests;

namespace Oravey2.Tests.Combat;

public class EnemyRespawnTests
{
    [Fact]
    public void RadratSpawnPoints_AlwaysPresent()
    {
        // Radrat spawn points are included regardless of quest state
        var spawnPoints = BuildWastelandSpawnPoints(new QuestLogComponent());

        Assert.Equal(3, spawnPoints.Count(sp => sp.Tag == "radrat"));
    }

    [Fact]
    public void ScarBoss_Spawns_WhenQuestActive()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q_raider_camp", "kill_scar");

        var spawnPoints = BuildWastelandSpawnPoints(log);

        Assert.Contains(spawnPoints, sp => sp.Tag == "scar");
    }

    [Fact]
    public void ScarBoss_DoesNotSpawn_WhenQuestNotStarted()
    {
        var log = new QuestLogComponent();

        var spawnPoints = BuildWastelandSpawnPoints(log);

        Assert.DoesNotContain(spawnPoints, sp => sp.Tag == "scar");
    }

    [Fact]
    public void ScarBoss_DoesNotSpawn_WhenQuestCompleted()
    {
        var log = new QuestLogComponent();
        log.StartQuest("q_raider_camp", "kill_scar");
        log.CompleteQuest("q_raider_camp");

        var spawnPoints = BuildWastelandSpawnPoints(log);

        Assert.DoesNotContain(spawnPoints, sp => sp.Tag == "scar");
    }

    [Fact]
    public void SpawnPoints_FreshOnEveryCall()
    {
        // Every call builds a new list — simulates zone re-entry creating fresh enemies
        var log = new QuestLogComponent();
        var points1 = BuildWastelandSpawnPoints(log);
        var points2 = BuildWastelandSpawnPoints(log);

        Assert.NotSame(points1, points2);
        Assert.Equal(points1.Count, points2.Count);
    }

    /// <summary>
    /// Mirrors the spawn point building logic from the former ScenarioLoader.LoadWasteland().
    /// Now seeded via WorldDbSeeder.
    /// </summary>
    private static List<EnemySpawnPoint> BuildWastelandSpawnPoints(QuestLogComponent questLog)
    {
        var spawnPoints = new List<EnemySpawnPoint>
        {
            new("radrat_south", -2f, -2f, Count: 1, Endurance: 1, Luck: 3, WeaponDamage: 4, WeaponAccuracy: 0.50f, Tag: "radrat"),
            new("radrat_east",   2f, -2f, Count: 1, Endurance: 1, Luck: 3, WeaponDamage: 4, WeaponAccuracy: 0.50f, Tag: "radrat"),
            new("radrat_road",  -2f,  0f, Count: 1, Endurance: 1, Luck: 3, WeaponDamage: 4, WeaponAccuracy: 0.50f, Tag: "radrat"),
        };

        if (questLog.GetStatus("q_raider_camp") == QuestStatus.Active)
        {
            spawnPoints.Add(new EnemySpawnPoint(
                "scar_boss", 10f, 0f, Count: 1,
                Endurance: 3, Luck: 5, WeaponDamage: 8, WeaponAccuracy: 0.65f,
                Tag: "scar"));
        }

        return spawnPoints;
    }
}
