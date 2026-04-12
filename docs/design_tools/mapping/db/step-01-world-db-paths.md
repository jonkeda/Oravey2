# Step 01 — WorldDbPaths Helper

## Goal

Create a single source of truth for the user's persistent world.db path.
All consumers (game, MapGen, tests) resolve the path through this helper
instead of hard-coding locations.

## Deliverables

### 1.1 `WorldDbPaths` static class

New file: `src/Oravey2.Core/Data/WorldDbPaths.cs`

```csharp
namespace Oravey2.Core.Data;

public static class WorldDbPaths
{
    /// <summary>
    /// User's persistent world.db in LocalApplicationData.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public static string GetUserWorldDbPath()
    {
        var envOverride = Environment.GetEnvironmentVariable("ORAVEY2_WORLD_DB");
        if (!string.IsNullOrEmpty(envOverride))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(envOverride)!);
            return envOverride;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Oravey2");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "world.db");
    }

    /// <summary>
    /// Finds the per-pack world.db inside a content pack directory.
    /// Returns null if the pack has no world.db (not yet exported).
    /// </summary>
    public static string? GetPackWorldDbPath(string contentPackDir)
    {
        var path = Path.Combine(contentPackDir, "world.db");
        return File.Exists(path) ? path : null;
    }
}
```

### 1.2 Behaviour

| Call | Returns |
|------|---------|
| `GetUserWorldDbPath()` (default) | `%LOCALAPPDATA%\Oravey2\world.db` |
| `GetUserWorldDbPath()` with `ORAVEY2_WORLD_DB=C:\tmp\test.db` | `C:\tmp\test.db` |
| `GetPackWorldDbPath("content/Oravey2.Apocalyptic.NL.NH")` when `world.db` exists | Full path to that `world.db` |
| `GetPackWorldDbPath(...)` when no `world.db` | `null` |

### 1.3 Unit tests

New file: `tests/Oravey2.Tests/Data/WorldDbPathsTests.cs`

```csharp
[Fact]
public void GetUserWorldDbPath_DefaultReturnsLocalAppData()
{
    Environment.SetEnvironmentVariable("ORAVEY2_WORLD_DB", null);
    var path = WorldDbPaths.GetUserWorldDbPath();
    var expected = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Oravey2", "world.db");
    Assert.Equal(expected, path);
}

[Fact]
public void GetUserWorldDbPath_RespectsEnvVar()
{
    var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "custom.db");
    try
    {
        Environment.SetEnvironmentVariable("ORAVEY2_WORLD_DB", tmp);
        var path = WorldDbPaths.GetUserWorldDbPath();
        Assert.Equal(tmp, path);
        Assert.True(Directory.Exists(Path.GetDirectoryName(tmp)));
    }
    finally
    {
        Environment.SetEnvironmentVariable("ORAVEY2_WORLD_DB", null);
        if (Directory.Exists(Path.GetDirectoryName(tmp)!))
            Directory.Delete(Path.GetDirectoryName(tmp)!, true);
    }
}

[Fact]
public void GetPackWorldDbPath_ReturnsNullWhenMissing()
{
    var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(tmp);
    try
    {
        Assert.Null(WorldDbPaths.GetPackWorldDbPath(tmp));
    }
    finally { Directory.Delete(tmp, true); }
}

[Fact]
public void GetPackWorldDbPath_ReturnsPathWhenExists()
{
    var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(tmp);
    File.WriteAllText(Path.Combine(tmp, "world.db"), "");
    try
    {
        var result = WorldDbPaths.GetPackWorldDbPath(tmp);
        Assert.NotNull(result);
        Assert.EndsWith("world.db", result);
    }
    finally { Directory.Delete(tmp, true); }
}
```

## Dependencies

None — this is the foundation step.

## Estimated scope

- New files: 2 (`WorldDbPaths.cs`, `WorldDbPathsTests.cs`)
- Modified files: 0
