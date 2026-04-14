using Oravey2.Core.Save;

namespace Oravey2.Tests.Save;

public class SaveMigrationChainTests
{
    private static SaveData MakeData(int formatVersion)
    {
        return new SaveData
        {
            Header = new SaveHeader { FormatVersion = formatVersion, GameVersion = "1.0.0", Timestamp = DateTime.UtcNow, PlayerName = "P", PlayerLevel = 1, PlayTime = TimeSpan.Zero }
        };
    }

    private sealed class TestMigration : ISaveMigration
    {
        public int FromVersion { get; }
        public int ToVersion { get; }
        private readonly Action<SaveData>? _action;

        public TestMigration(int from, int to, Action<SaveData>? action = null)
        {
            FromVersion = from;
            ToVersion = to;
            _action = action;
        }

        public SaveData Migrate(SaveData data)
        {
            _action?.Invoke(data);
            data.Header = new SaveHeader
            {
                FormatVersion = ToVersion,
                GameVersion = data.Header.GameVersion,
                Timestamp = data.Header.Timestamp,
                PlayerName = data.Header.PlayerName,
                PlayerLevel = data.Header.PlayerLevel,
                PlayTime = data.Header.PlayTime,
            };
            return data;
        }
    }

    [Fact]
    public void NoMigrationsNeededForCurrentVersion()
    {
        var chain = new SaveMigrationChain();
        var data = MakeData(SaveHeader.CurrentFormatVersion);

        var result = chain.MigrateToLatest(data);

        Assert.Same(data, result);
    }

    [Fact]
    public void NeedsMigrationTrueForOldVersion()
    {
        var chain = new SaveMigrationChain();
        var data = MakeData(0);

        Assert.True(chain.NeedsMigration(data));
    }

    [Fact]
    public void NeedsMigrationFalseForCurrent()
    {
        var chain = new SaveMigrationChain();
        var data = MakeData(SaveHeader.CurrentFormatVersion);

        Assert.False(chain.NeedsMigration(data));
    }

    [Fact]
    public void SingleMigrationUpgradesVersion()
    {
        var chain = new SaveMigrationChain();
        chain.Register(new TestMigration(0, 1));
        var data = MakeData(0);

        var result = chain.MigrateToLatest(data, targetVersion: 1);

        Assert.Equal(1, result.Header.FormatVersion);
    }

    [Fact]
    public void ChainedMigrationsRunInOrder()
    {
        var order = new List<int>();
        var chain = new SaveMigrationChain();
        chain.Register(new TestMigration(0, 1, _ => order.Add(0)));
        chain.Register(new TestMigration(1, 2, _ => order.Add(1)));
        chain.Register(new TestMigration(2, 3, _ => order.Add(2)));
        var data = MakeData(0);

        chain.MigrateToLatest(data, targetVersion: 3);

        Assert.Equal([0, 1, 2], order);
    }

    [Fact]
    public void MissingMigrationThrows()
    {
        var chain = new SaveMigrationChain();
        var data = MakeData(0);

        Assert.Throws<InvalidOperationException>(() => chain.MigrateToLatest(data, targetVersion: 1));
    }

    [Fact]
    public void DuplicateRegistrationThrows()
    {
        var chain = new SaveMigrationChain();
        chain.Register(new TestMigration(0, 1));

        Assert.Throws<InvalidOperationException>(() =>
            chain.Register(new TestMigration(0, 1)));
    }

    [Fact]
    public void MigrationAppliesDataChanges()
    {
        var chain = new SaveMigrationChain();
        chain.Register(new TestMigration(0, 1, d => d.HP = 999));
        var data = MakeData(0);

        var result = chain.MigrateToLatest(data, targetVersion: 1);

        Assert.Equal(999, result.HP);
    }

    [Fact]
    public void CountReturnsRegisteredCount()
    {
        var chain = new SaveMigrationChain();
        Assert.Equal(0, chain.Count);

        chain.Register(new TestMigration(0, 1));
        chain.Register(new TestMigration(1, 2));
        Assert.Equal(2, chain.Count);
    }

    [Fact]
    public void MigrateToLatestWithCustomTarget()
    {
        var chain = new SaveMigrationChain();
        chain.Register(new TestMigration(0, 1));
        chain.Register(new TestMigration(1, 2));
        var data = MakeData(0);

        var result = chain.MigrateToLatest(data, targetVersion: 1);

        Assert.Equal(1, result.Header.FormatVersion);
    }
}
