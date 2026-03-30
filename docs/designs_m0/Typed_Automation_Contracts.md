# Design: Typed Automation Contracts

Replace manual `GetProperty("x").GetInt32()` JSON parsing on both sides of the automation pipe with shared record types that `System.Text.Json` serializes/deserializes automatically.

---

## Problem

Every automation command currently has 3 places where property names must match exactly:

1. **Handler** — `JsonSerializer.SerializeToElement(new { success = true, playerHp = ... })`
2. **Handler** — `config.GetProperty("damage").GetInt32()` for request parsing
3. **GameQueryHelpers** — `je.GetProperty("playerHp").GetInt32()` for response parsing

A typo in any of these fails silently or throws at runtime. Adding a new field requires editing all 3 locations.

---

## Approach: Shared Contracts in Oravey2.Core

Both `Oravey2.Windows` and `Oravey2.UITests` already reference `Oravey2.Core`. Place all request/response records in a single file in Core:

```
src/Oravey2.Core/
└── Automation/
    └── AutomationContracts.cs    # All request/response records
```

No new project needed. The contracts are plain C# records with no Stride dependencies.

---

## Contract Records

```csharp
// src/Oravey2.Core/Automation/AutomationContracts.cs
namespace Oravey2.Core.Automation;

// ---- Phase E: Scenario commands ----

public record SpawnEnemyRequest
{
    public string? Id { get; init; }
    public double X { get; init; }
    public double Z { get; init; }
    public int? Hp { get; init; }
    public int? Endurance { get; init; }
    public int? Luck { get; init; }
    public int? WeaponDamage { get; init; }
    public float? WeaponAccuracy { get; init; }
}

public record SpawnEnemyResponse(bool Success, string Id, int Hp, int MaxHp);

public record ScenarioResetResponse(bool Success, int PlayerHp, int EnemyCount);

public record SetPlayerStatsRequest
{
    public int? Endurance { get; init; }
    public int? Luck { get; init; }
    public int? Strength { get; init; }
    public int? Hp { get; init; }
}

public record SetStatsResponse(bool Success, int Hp, int MaxHp);

public record SetPlayerWeaponRequest
{
    public int Damage { get; init; }
    public float Accuracy { get; init; }
    public float Range { get; init; } = 2f;
    public int ApCost { get; init; } = 3;
    public float CritMultiplier { get; init; } = 1.5f;
}

public record SetWeaponResponse(bool Success, int Damage, float Accuracy);

// ---- Existing commands (migrate incrementally) ----

public record PositionResponse(double X, double Y, double Z);

public record CameraStateResponse(
    double X, double Y, double Z,
    double Yaw, double Pitch, double Zoom);

public record HudStateResponse(
    int Hp, int MaxHp, int Ap, int MaxAp,
    int Level, string GameState);

public record DamagePlayerResponse(int NewHp, int MaxHp, bool IsAlive);

public record CombatConfigResponse
{
    public WeaponConfigDto Player { get; init; } = default!;
    public WeaponConfigDto Enemy { get; init; } = default!;
    public float MeleeDistance { get; init; }
}

public record WeaponConfigDto(
    int Damage, float Accuracy, float Range,
    float CritMultiplier, int ApCost);

public record EnemyStateDto(
    string Id, int Hp, int MaxHp, int Ap, int MaxAp,
    bool IsAlive, double X, double Y, double Z);

public record CombatStateResponse(
    bool InCombat, int EnemyCount,
    List<EnemyStateDto> Enemies,
    int PlayerHp, int PlayerMaxHp,
    int PlayerAp, int PlayerMaxAp);

public record EquipItemResponse(bool Success, string Slot, string ItemName);
```

---

## Handler Side (Before / After)

### Before (manual JSON parsing):
```csharp
private AutomationResponse SpawnEnemy(AutomationCommand command)
{
    var json = command.Args[0]?.ToString();
    var config = JsonSerializer.Deserialize<JsonElement>(json);
    var id = config.TryGetProperty("id", out var idProp) ? idProp.GetString() : ...;
    var x = (float)config.GetProperty("x").GetDouble();
    var endurance = TryGetInt(config, "endurance") ?? 1;
    // ... 10+ lines of manual parsing
}
```

### After (typed deserialization):
```csharp
private AutomationResponse SpawnEnemy(AutomationCommand command)
{
    var req = DeserializeArg<SpawnEnemyRequest>(command);
    if (req == null) return AutomationResponse.Fail("Invalid SpawnEnemy config");

    var id = req.Id ?? $"enemy_{Guid.NewGuid():N}";
    var endurance = req.Endurance ?? 1;
    // ... use req.X, req.Z, req.WeaponDamage directly

    return Respond(new SpawnEnemyResponse(true, id, health.CurrentHP, health.MaxHP));
}

// Shared helpers at class level:
private static T? DeserializeArg<T>(AutomationCommand command) where T : class
{
    var json = command.Args?.FirstOrDefault()?.ToString();
    return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json, _jsonOpts);
}

private static AutomationResponse Respond<T>(T result)
    => AutomationResponse.Ok(JsonSerializer.SerializeToElement(result, _jsonOpts));

private static readonly JsonSerializerOptions _jsonOpts = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};
```

---

## Test Side (Before / After)

### Before (manual JSON drilling):
```csharp
public static SpawnEnemyResult SpawnEnemy(IStrideTestContext context,
    string id, double x, double z, int? hp = null, ...)
{
    var config = JsonSerializer.Serialize(new { id, x, z, hp, ... });
    var response = context.SendCommand(AutomationCommand.GameQuery("SpawnEnemy", config));
    var je = (JsonElement)response.Result!;
    return new SpawnEnemyResult(
        je.GetProperty("success").GetBoolean(),
        je.GetProperty("id").GetString() ?? "",
        je.GetProperty("hp").GetInt32(),
        je.GetProperty("maxHp").GetInt32());
}
```

### After (typed serialization):
```csharp
public static SpawnEnemyResponse SpawnEnemy(IStrideTestContext context,
    string id, double x, double z, int? hp = null, ...)
{
    var req = new SpawnEnemyRequest { Id = id, X = x, Z = z, Hp = hp, ... };
    return SendQuery<SpawnEnemyResponse>("SpawnEnemy", context, req);
}

// Shared query helper:
private static TResponse SendQuery<TResponse>(
    string method, IStrideTestContext context, object? request = null)
{
    var args = request != null
        ? new object[] { JsonSerializer.Serialize(request, _jsonOpts) }
        : Array.Empty<object>();

    var response = context.SendCommand(AutomationCommand.GameQuery(method, args));
    if (!response.Success)
        throw new InvalidOperationException($"{method} failed: {response.Error}");

    var json = ((JsonElement)response.Result!).GetRawText();
    return JsonSerializer.Deserialize<TResponse>(json, _jsonOpts)
        ?? throw new InvalidOperationException($"{method} returned null");
}
```

---

## Migration Strategy

Migrate incrementally, one command at a time. Each migration is a standalone change:

| Wave | Commands | Records |
|------|----------|---------|
| 1 | Phase E commands (SpawnEnemy, ResetScenario, SetPlayerStats, SetPlayerWeapon) | 8 records |
| 2 | Phase D commands (GetCombatConfig, EquipItem) | 4 records |
| 3 | Phase B/C commands (GetHudState, GetCombatState, DamagePlayer, etc.) | ~10 records |
| 4 | Phase A commands (GetPlayerPosition, GetCameraState, etc.) | ~8 records |

Each wave:
1. Add contract records to `AutomationContracts.cs`
2. Update handler to use `DeserializeArg<T>` / `Respond<T>`
3. Update `GameQueryHelpers` to use `SendQuery<TResponse>`
4. Remove old manual parsing and duplicate record types from `GameQueryHelpers.cs`
5. Run tests — behavior is unchanged

---

## What Gets Deleted

After full migration, `GameQueryHelpers.cs` loses ~200 lines of duplicate record types and manual `GetProperty` parsing. The file shrinks from ~640 lines to ~300 lines (just the thin wrapper methods calling `SendQuery<T>`).

The handler loses all `TryGetInt`/`TryGetFloat` helpers and manual `JsonElement` parsing.

---

## File Layout

```
src/Oravey2.Core/
└── Automation/
    └── AutomationContracts.cs         # NEW — all request/response records

src/Oravey2.Windows/
└── OraveyAutomationHandler.cs         # MODIFY — use DeserializeArg<T> / Respond<T>

tests/Oravey2.UITests/
└── GameQueryHelpers.cs                # MODIFY — use SendQuery<TResponse>, remove duplicate records
```

---

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| 1 | All contract records live in `Oravey2.Core.Automation` |
| 2 | Handler uses `DeserializeArg<T>` for all request parsing |
| 3 | Handler uses `Respond<T>` for all response building |
| 4 | `GameQueryHelpers` uses `SendQuery<TResponse>` for all queries |
| 5 | No duplicate record types remain in `GameQueryHelpers.cs` |
| 6 | `JsonNamingPolicy.CamelCase` ensures wire compatibility with existing JSON shape |
| 7 | All 127 UI tests pass unchanged after migration |
| 8 | Property name strings appear exactly once (in the record definition) |
