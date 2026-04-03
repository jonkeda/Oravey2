using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.Serialization;

public class ChunkJsonFormatTests
{
    [Fact]
    public void ChunkJson_Instantiates_WithValidData()
    {
        var surface = new int[][] { new int[] { 0, 1 }, new int[] { 2, 3 } };
        var height = new int[][] { new int[] { 1, 1 }, new int[] { 1, 1 } };
        var water = new int[][] { new int[] { 0, 0 }, new int[] { 0, 0 } };
        var structure = new int[][] { new int[] { 0, 0 }, new int[] { 0, 0 } };
        var flags = new int[][] { new int[] { 1, 1 }, new int[] { 1, 1 } };
        var variant = new int[][] { new int[] { 0, 0 }, new int[] { 0, 0 } };

        var chunk = new ChunkJson(0, 0, surface, height, water, structure, flags, variant, null);

        Assert.Equal(0, chunk.ChunkX);
        Assert.Equal(0, chunk.ChunkY);
        Assert.Null(chunk.Entities);
    }

    [Fact]
    public void EntitySpawnJson_Instantiates_WithNullables()
    {
        var entity = new EntitySpawnJson("npc_guard", 5f, 10f, 90f, null, null, null, null, false, null);

        Assert.Equal("npc_guard", entity.PrefabId);
        Assert.Null(entity.Faction);
        Assert.Null(entity.Level);
        Assert.False(entity.Persistent);
    }

    [Fact]
    public void EntitySpawnJson_AllFields_Set()
    {
        var entity = new EntitySpawnJson(
            "npc_merchant", 3f, 7f, 180f,
            "Haven", 5, "dlg_01", "loot_common",
            true, "quest_started");

        Assert.Equal("Haven", entity.Faction);
        Assert.Equal(5, entity.Level);
        Assert.Equal("dlg_01", entity.DialogueId);
        Assert.True(entity.Persistent);
        Assert.Equal("quest_started", entity.ConditionFlag);
    }
}
