using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Oravey2.Core.Data;

public static class DatabaseInitializer
{
    public static void InitializeWorld(SqliteConnection connection)
        => ExecuteSchema(connection, "Oravey2.Core.Data.WorldDbSchema.sql");

    public static void InitializeSave(SqliteConnection connection)
        => ExecuteSchema(connection, "Oravey2.Core.Data.SaveDbSchema.sql");

    private static void ExecuteSchema(SqliteConnection connection, string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
