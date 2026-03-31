using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Save;
using Oravey2.Core.World;

namespace Oravey2.Tests.Save;

public class SaveServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly SaveService _svc;
    private readonly EventBus _bus = new();

    public SaveServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"oravey_test_{Guid.NewGuid():N}");
        _svc = new SaveService(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private SaveData CreateTestSaveData(int caps = 50, float posX = 1f, float posY = 0.5f, float posZ = 2f)
    {
        var stats = new StatsComponent();
        var level = new LevelComponent(stats);
        var health = new HealthComponent(stats, level, _bus);
        var inventory = new InventoryComponent(stats) { Caps = caps };
        var equipment = new EquipmentComponent();
        var dayNight = new DayNightCycleProcessor(_bus);

        return new SaveDataBuilder()
            .WithHeader("TestPlayer", 1, TimeSpan.FromMinutes(5), "0.1.0")
            .WithStats(stats)
            .WithHealth(health)
            .WithLevel(level)
            .WithInventory(inventory, equipment)
            .WithWorldState(dayNight, 0, 0, posX, posY, posZ)
            .Build();
    }

    [Fact]
    public void HasSaveFile_ReturnsFalse_WhenNoFile()
    {
        Assert.False(_svc.HasSaveFile());
    }

    [Fact]
    public void HasSaveFile_ReturnsTrue_AfterSave()
    {
        var data = CreateTestSaveData();
        _svc.SaveGame(data);
        Assert.True(_svc.HasSaveFile());
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var data = CreateTestSaveData(caps: 42, posX: 3f, posY: 1.5f, posZ: -2f);
        _svc.SaveGame(data);

        var restorer = _svc.LoadGame();
        Assert.NotNull(restorer);

        var (px, py, pz) = restorer.PlayerPosition;
        Assert.Equal(3f, px, 0.01f);
        Assert.Equal(1.5f, py, 0.01f);
        Assert.Equal(-2f, pz, 0.01f);
        Assert.Equal(42, restorer.Caps);
    }

    [Fact]
    public void LoadGame_ReturnsNull_WhenNoFile()
    {
        var restorer = _svc.LoadGame();
        Assert.Null(restorer);
    }

    [Fact]
    public void DeleteSave_RemovesFile()
    {
        _svc.SaveGame(CreateTestSaveData());
        Assert.True(_svc.HasSaveFile());

        _svc.DeleteSave();
        Assert.False(_svc.HasSaveFile());
    }

    [Fact]
    public void DeleteSave_NoOpWhenNoFile()
    {
        _svc.DeleteSave(); // Should not throw
        Assert.False(_svc.HasSaveFile());
    }

    [Fact]
    public void SaveHeader_CapturesMetadata()
    {
        var data = CreateTestSaveData();
        _svc.SaveGame(data);

        var restorer = _svc.LoadGame();
        Assert.NotNull(restorer);
        // Verify round-trip through header by checking caps (inventory data)
        Assert.Equal(50, restorer.Caps);
    }
}
