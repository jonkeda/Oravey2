using System.Text.Json;
using Oravey2.Core.World;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Tests.Serialization;

public class ChunkSerializerTests : IDisposable
{
    private readonly string _tempDir;

    public ChunkSerializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"oravey2_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void RoundTrip_AllTileData_Matches()
    {
        var chunk = ChunkData.CreateDefault(2, 3);
        chunk.Tiles.SetTileData(5, 5, TileDataFactory.Road(variant: 42));
        chunk.Tiles.SetTileData(10, 10, TileDataFactory.Water(waterLevel: 5, terrainHeight: 1));
        chunk.Tiles.SetTileData(0, 0, TileDataFactory.Wall(height: 3));

        var json = ChunkSerializer.SerializeChunk(chunk);
        var restored = ChunkSerializer.DeserializeChunk(json);

        Assert.Equal(chunk.ChunkX, restored.ChunkX);
        Assert.Equal(chunk.ChunkY, restored.ChunkY);

        for (int x = 0; x < ChunkData.Size; x++)
        {
            for (int y = 0; y < ChunkData.Size; y++)
            {
                var original = chunk.Tiles.GetTileData(x, y);
                var loaded = restored.Tiles.GetTileData(x, y);
                Assert.Equal(original, loaded);
            }
        }
    }

    [Fact]
    public void RoundTrip_TownMapBuilder_LayoutMatches()
    {
        var townMap = TownMapBuilder.CreateTownMap();
        var chunk = new ChunkData(0, 0, townMap);

        var json = ChunkSerializer.SerializeChunk(chunk);
        var restored = ChunkSerializer.DeserializeChunk(json);

        // Check known positions
        Assert.Equal(TileType.Wall, restored.Tiles.GetTile(0, 0));
        Assert.Equal(TileType.Road, restored.Tiles.GetTile(10, 8));
        Assert.Equal(TileType.Ground, restored.Tiles.GetTile(12, 17));
        Assert.True(restored.Tiles.IsWalkable(30, 17)); // gate
    }

    [Fact]
    public void RoundTrip_GroundTiles_CorrectSurfaceAndFlags()
    {
        var chunk = ChunkData.CreateDefault(0, 0);

        var json = ChunkSerializer.SerializeChunk(chunk);
        var restored = ChunkSerializer.DeserializeChunk(json);

        var data = restored.Tiles.GetTileData(5, 5);
        Assert.Equal(SurfaceType.Dirt, data.Surface);
        Assert.Equal(1, data.HeightLevel);
        Assert.Equal(TileFlags.Walkable, data.Flags);
    }

    [Fact]
    public void RoundTrip_WallTiles_CorrectStructureAndNonWalkable()
    {
        var chunk = new ChunkData(0, 0);
        chunk.Tiles.SetTileData(3, 3, TileDataFactory.Wall());

        var json = ChunkSerializer.SerializeChunk(chunk);
        var restored = ChunkSerializer.DeserializeChunk(json);

        var data = restored.Tiles.GetTileData(3, 3);
        Assert.Equal(1, data.StructureId);
        Assert.False(data.IsWalkable);
        Assert.Equal(TileType.Wall, data.LegacyTileType);
    }

    [Fact]
    public void RoundTrip_WaterTiles_CorrectWaterLevel()
    {
        var chunk = new ChunkData(0, 0);
        chunk.Tiles.SetTileData(7, 7, TileDataFactory.Water(waterLevel: 4, terrainHeight: 1));

        var json = ChunkSerializer.SerializeChunk(chunk);
        var restored = ChunkSerializer.DeserializeChunk(json);

        var data = restored.Tiles.GetTileData(7, 7);
        Assert.True(data.HasWater);
        Assert.Equal(4, data.WaterLevel);
        Assert.Equal(1, data.HeightLevel);
        Assert.Equal(TileType.Water, data.LegacyTileType);
    }

    [Fact]
    public void RoundTrip_EntitySpawns_Survive()
    {
        var entities = new List<EntitySpawnInfo>
        {
            new("npc_guard", 5f, 10f, 90f, "Haven", 3, "dlg_guard", "loot_common", true, "quest_done"),
            new("barrel", 2f, 3f)
        };
        var chunk = new ChunkData(1, 2, entities: entities);

        var json = ChunkSerializer.SerializeChunk(chunk);
        var restored = ChunkSerializer.DeserializeChunk(json);

        Assert.Equal(2, restored.Entities.Count);

        var guard = restored.Entities[0];
        Assert.Equal("npc_guard", guard.PrefabId);
        Assert.Equal(5f, guard.LocalX);
        Assert.Equal(90f, guard.RotationY);
        Assert.Equal("Haven", guard.Faction);
        Assert.Equal(3, guard.Level);
        Assert.True(guard.Persistent);

        var barrel = restored.Entities[1];
        Assert.Equal("barrel", barrel.PrefabId);
        Assert.Null(barrel.Faction);
        Assert.False(barrel.Persistent);
    }

    [Fact]
    public void DeserializeChunk_InvalidJson_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => ChunkSerializer.DeserializeChunk("not valid json"));
    }

    [Fact]
    public void LoadChunk_MissingFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ChunkSerializer.LoadChunk(_tempDir, 99, 99));
    }

    [Fact]
    public void SaveChunk_LoadChunk_RoundTrip()
    {
        var chunk = ChunkData.CreateDefault(3, 4);
        chunk.Tiles.SetTileData(1, 1, TileDataFactory.Road());

        ChunkSerializer.SaveChunk(chunk, _tempDir);
        var restored = ChunkSerializer.LoadChunk(_tempDir, 3, 4);

        Assert.Equal(3, restored.ChunkX);
        Assert.Equal(4, restored.ChunkY);
        Assert.Equal(TileType.Road, restored.Tiles.GetTile(1, 1));
    }
}
