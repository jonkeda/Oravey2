namespace Oravey2.Core.Save;

/// <summary>
/// Migrates save data from one format version to the next.
/// </summary>
public interface ISaveMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    SaveData Migrate(SaveData data);
}

/// <summary>
/// Runs a chain of migrations to bring save data up to the latest format version.
/// </summary>
public sealed class SaveMigrationChain
{
    private readonly SortedList<int, ISaveMigration> _migrations = new();

    public void Register(ISaveMigration migration)
    {
        if (_migrations.ContainsKey(migration.FromVersion))
            throw new InvalidOperationException(
                $"Migration from version {migration.FromVersion} already registered.");
        _migrations[migration.FromVersion] = migration;
    }

    /// <summary>
    /// Migrates save data from its current FormatVersion to the target version.
    /// Throws if a required migration is missing.
    /// </summary>
    public SaveData MigrateToLatest(SaveData data, int targetVersion = -1)
    {
        if (targetVersion < 0)
            targetVersion = SaveHeader.CurrentFormatVersion;

        int current = data.Header.FormatVersion;
        while (current < targetVersion)
        {
            if (!_migrations.TryGetValue(current, out var migration))
                throw new InvalidOperationException(
                    $"No migration registered from version {current}.");

            data = migration.Migrate(data);
            current = data.Header.FormatVersion;
        }
        return data;
    }

    /// <summary>
    /// Returns true if the save data needs migration.
    /// </summary>
    public bool NeedsMigration(SaveData data, int targetVersion = -1)
    {
        if (targetVersion < 0) targetVersion = SaveHeader.CurrentFormatVersion;
        return data.Header.FormatVersion < targetVersion;
    }

    /// <summary>Number of registered migrations.</summary>
    public int Count => _migrations.Count;
}
