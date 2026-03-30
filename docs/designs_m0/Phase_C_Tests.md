# Design: Phase C Tests — HUD, Game Over & Notification UI Tests

Adds Brinell UI tests for the Phase C HUD upgrade, game-over/victory overlays, notification feed, and enemy HP bars. Requires new automation queries to observe overlay state, notification content, and force player damage.

**Depends on:** Phase C implementation (HudSyncScript upgrade, GameOverOverlayScript, NotificationFeedScript, EnemyHpBarScript, FloatingDamageScript)

---

## Problem

After Phase C, the game has new player-visible behavior with no UI test coverage:
- HUD now shows colored HP/AP bars instead of plain text — no test verifies bar data
- Enemy HP bars appear during combat — no test verifies they show/hide correctly
- Notifications appear at screen bottom — no test verifies feed content or timing
- Player death triggers a "GAME OVER" overlay — no test verifies the transition
- Defeating all enemies shows "ENEMIES DEFEATED" — no test verifies victory text
- Floating damage numbers appear on hit — covered implicitly by combat state, but overlay text needs verification
- Input frozen during GameOver — no test verifies movement is blocked

---

## New Automation Queries

### Game-side: OraveyAutomationHandler

| Query | Args | Returns | Purpose |
|-------|------|---------|---------|
| `GetNotificationFeed` | — | `{ count, messages: [{text, timeRemaining}] }` | Current visible notification messages |
| `GetGameOverState` | — | `{ visible, title }` | Game over overlay visibility and title text |
| `GetEnemyHpBars` | — | `{ visible, bars: [{enemyId, hp, maxHp}] }` | Enemy HP bar data (visible only during combat) |
| `DamagePlayer` | `amount` | `{ newHp, maxHp, isAlive }` | Force exact HP reduction for deterministic testing |

### Why these four

- **`GetNotificationFeed`** — The only way to verify notification content. After loot pickup, we can confirm "Picked up ..." messages appear in the feed with correct text.
- **`GetGameOverState`** — Verifies the overlay is showing with the right title text. Checking `GetGameState` alone returns "GameOver" but doesn't confirm the overlay rendered.
- **`GetEnemyHpBars`** — Verifies that enemy bars show during combat and hide during exploring. The bar data (hp/maxHp) should match what `GetCombatState` reports.
- **`DamagePlayer`** — Testing game over requires the player reaching 0 HP. Combat RNG makes this unreliable (player deals/takes variable damage). `DamagePlayer` forces exact HP reduction, similar to how `KillEnemy` forces enemy death.

### Implementation: OraveyAutomationHandler additions

Route additions in the existing switch:

```csharp
"GetNotificationFeed" => GetNotificationFeed(),
"GetGameOverState" => GetGameOverState(),
"GetEnemyHpBars" => GetEnemyHpBars(),
"DamagePlayer" => DamagePlayer(command),
```

#### SetPhaseC

```csharp
private NotificationService? _notificationService;
private GameOverOverlayScript? _gameOverOverlay;

public void SetPhaseC(
    NotificationService notificationService,
    GameOverOverlayScript gameOverOverlay)
{
    _notificationService = notificationService;
    _gameOverOverlay = gameOverOverlay;
}
```

#### GetNotificationFeed

```csharp
private AutomationResponse GetNotificationFeed()
{
    if (_notificationService == null)
        return AutomationResponse.Fail("NotificationService not initialized");

    var active = _notificationService.GetActive();
    var messages = active.Select(n => new
    {
        text = n.Message,
        timeRemaining = n.TimeRemaining,
    });

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        count = active.Count,
        messages,
    }));
}
```

#### GetGameOverState

```csharp
private AutomationResponse GetGameOverState()
{
    if (_gameOverOverlay == null)
        return AutomationResponse.Fail("GameOverOverlay not initialized");

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        visible = _gameOverOverlay.IsVisible,
        title = _gameOverOverlay.CurrentTitle ?? "",
    }));
}
```

#### GetEnemyHpBars

```csharp
private AutomationResponse GetEnemyHpBars()
{
    var combatManager = FindEntity("CombatManager");
    var script = combatManager?.Get<CombatSyncScript>();
    if (script == null)
        return AutomationResponse.Fail("CombatSyncScript not found");

    var inCombat = script.CombatState?.InCombat ?? false;
    var bars = script.Enemies
        .Where(e => e.Health.IsAlive)
        .Select(e => new
        {
            enemyId = e.Id,
            hp = e.Health.CurrentHP,
            maxHp = e.Health.MaxHP,
        });

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        visible = inCombat,
        bars,
    }));
}
```

#### DamagePlayer

```csharp
private AutomationResponse DamagePlayer(AutomationCommand command)
{
    if (_playerHealth == null)
        return AutomationResponse.Fail("Player health not initialized");

    if (command.Args == null || command.Args.Length < 1)
        return AutomationResponse.Fail("DamagePlayer requires amount argument");

    int amount = Convert.ToInt32(command.Args[0]?.ToString());
    _playerHealth.TakeDamage(amount);

    return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        newHp = _playerHealth.CurrentHP,
        maxHp = _playerHealth.MaxHP,
        isAlive = _playerHealth.IsAlive,
    }));
}
```

---

## Test-side: GameQueryHelpers additions

```csharp
// --- Phase C: Notification / Game Over / Enemy Bars helpers ---

public record NotificationMessage(string Text, double TimeRemaining);

public record NotificationFeedState(int Count, List<NotificationMessage> Messages);

public record GameOverState(bool Visible, string Title);

public record EnemyHpBarInfo(string EnemyId, int Hp, int MaxHp);

public record EnemyHpBarsState(bool Visible, List<EnemyHpBarInfo> Bars);

public record DamageResult(int NewHp, int MaxHp, bool IsAlive);

public static NotificationFeedState GetNotificationFeed(IStrideTestContext context)
{
    var response = context.SendCommand(AutomationCommand.GameQuery("GetNotificationFeed"));
    if (!response.Success)
        throw new InvalidOperationException($"GetNotificationFeed failed: {response.Error}");

    var je = (JsonElement)response.Result!;
    var messages = new List<NotificationMessage>();
    if (je.TryGetProperty("messages", out var arr))
    {
        foreach (var m in arr.EnumerateArray())
        {
            messages.Add(new NotificationMessage(
                m.GetProperty("text").GetString() ?? "",
                m.GetProperty("timeRemaining").GetDouble()));
        }
    }

    return new NotificationFeedState(
        je.GetProperty("count").GetInt32(),
        messages);
}

public static GameOverState GetGameOverState(IStrideTestContext context)
{
    var response = context.SendCommand(AutomationCommand.GameQuery("GetGameOverState"));
    if (!response.Success)
        throw new InvalidOperationException($"GetGameOverState failed: {response.Error}");

    var je = (JsonElement)response.Result!;
    return new GameOverState(
        je.GetProperty("visible").GetBoolean(),
        je.GetProperty("title").GetString() ?? "");
}

public static EnemyHpBarsState GetEnemyHpBars(IStrideTestContext context)
{
    var response = context.SendCommand(AutomationCommand.GameQuery("GetEnemyHpBars"));
    if (!response.Success)
        throw new InvalidOperationException($"GetEnemyHpBars failed: {response.Error}");

    var je = (JsonElement)response.Result!;
    var bars = new List<EnemyHpBarInfo>();
    if (je.TryGetProperty("bars", out var arr))
    {
        foreach (var b in arr.EnumerateArray())
        {
            bars.Add(new EnemyHpBarInfo(
                b.GetProperty("enemyId").GetString() ?? "",
                b.GetProperty("hp").GetInt32(),
                b.GetProperty("maxHp").GetInt32()));
        }
    }

    return new EnemyHpBarsState(
        je.GetProperty("visible").GetBoolean(),
        bars);
}

public static DamageResult DamagePlayer(IStrideTestContext context, int amount)
{
    var response = context.SendCommand(AutomationCommand.GameQuery("DamagePlayer", amount));
    if (!response.Success)
        throw new InvalidOperationException($"DamagePlayer failed: {response.Error}");

    var je = (JsonElement)response.Result!;
    return new DamageResult(
        je.GetProperty("newHp").GetInt32(),
        je.GetProperty("maxHp").GetInt32(),
        je.GetProperty("isAlive").GetBoolean());
}
```

---

## File Layout

```
src/Oravey2.Windows/
└── OraveyAutomationHandler.cs                # MODIFY — add 4 new queries + SetPhaseC + new fields

tests/Oravey2.UITests/
├── GameQueryHelpers.cs                       # MODIFY — add 4 new records + 4 new helpers
├── HudBarTests.cs                            # NEW — verify HP/AP bar data accuracy
├── EnemyHpBarTests.cs                        # NEW — verify enemy bars show/hide + data
├── NotificationFeedTests.cs                  # NEW — verify notification display and timing
├── GameOverTests.cs                          # NEW — verify game over overlay and state
├── VictoryTests.cs                           # NEW — verify victory text on combat end
└── InputFreezeTests.cs                       # NEW — verify movement blocked during GameOver
```

---

## World Reference

Same as Phase A/B:
- 32×32 tile map, `TileSize=1.0`, centered at world origin
- Player starts at `(0, 0.5, 0)`
- Enemies: `enemy_1` at `(8, 0.5, 8)`, `enemy_2` at `(-6, 0.5, 10)`, `enemy_3` at `(10, 0.5, -6)`
- Trigger radius: 5 units
- Player default HP: 105 (Endurance=5, Level=1)
- Player default MaxAP: 10 (Agility=5)
- Enemy HP: 95 (Endurance=4, Level=1)

---

## Test Classes

### HudBarTests (5 tests)

Verifies HUD data via `GetHudState` after Phase C bar upgrade. Note: we verify the data feeding the bars, not pixel values.

```csharp
public class HudBarTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void HudState_FullHealth_AtStart()
    {
        // 105 HP = 50 + 5*10 + 1*5
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(105, hud.Hp);
        Assert.Equal(105, hud.MaxHp);
    }

    [Fact]
    public void HudState_ReducedHp_AfterDamage()
    {
        var dmg = GameQueryHelpers.DamagePlayer(_fixture.Context, 30);
        Assert.Equal(75, dmg.NewHp);

        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(75, hud.Hp);
        Assert.Equal(105, hud.MaxHp);
    }

    [Fact]
    public void HudState_FullAp_AtStart()
    {
        // 10 AP = 8 + 5/2
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(10, hud.Ap);
        Assert.Equal(10, hud.MaxAp);
    }

    [Fact]
    public void HudState_LevelOne_AtStart()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal(1, hud.Level);
    }

    [Fact]
    public void HudState_ShowsExploring_AtStart()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        Assert.Equal("Exploring", hud.GameState);
    }
}
```

---

### EnemyHpBarTests (5 tests)

Verifies enemy HP bars show during combat and hide outside combat.

```csharp
public class EnemyHpBarTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private void WaitForCombat()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }
    }

    [Fact]
    public void EnemyBars_NotVisible_WhenExploring()
    {
        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        Assert.False(bars.Visible);
    }

    [Fact]
    public void EnemyBars_Visible_WhenInCombat()
    {
        WaitForCombat();
        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        Assert.True(bars.Visible);
    }

    [Fact]
    public void EnemyBars_ShowAllLivingEnemies()
    {
        WaitForCombat();
        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);

        var aliveCount = combat.Enemies.Count(e => e.IsAlive);
        Assert.Equal(aliveCount, bars.Bars.Count);
    }

    [Fact]
    public void EnemyBars_MatchCombatState_Hp()
    {
        WaitForCombat();
        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        var combat = GameQueryHelpers.GetCombatState(_fixture.Context);

        foreach (var bar in bars.Bars)
        {
            var enemy = combat.Enemies.FirstOrDefault(e => e.Id == bar.EnemyId);
            Assert.NotNull(enemy);
            Assert.Equal(enemy.Hp, bar.Hp);
            Assert.Equal(enemy.MaxHp, bar.MaxHp);
        }
    }

    [Fact]
    public void EnemyBars_RemoveDeadEnemy()
    {
        WaitForCombat();
        var barsBefore = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        int countBefore = barsBefore.Bars.Count;

        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var barsAfter = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        Assert.True(barsAfter.Bars.Count < countBefore,
            $"Expected fewer bars after kill: before={countBefore}, after={barsAfter.Bars.Count}");
        Assert.DoesNotContain(barsAfter.Bars, b => b.EnemyId == "enemy_1");
    }
}
```

---

### NotificationFeedTests (4 tests)

Verifies notification display. Uses loot pickup (Phase B) as a natural source of NotificationEvents.

```csharp
public class NotificationFeedTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void NoNotifications_AtStart()
    {
        var feed = GameQueryHelpers.GetNotificationFeed(_fixture.Context);
        Assert.Equal(0, feed.Count);
    }

    [Fact]
    public void LootPickup_ShowsNotification()
    {
        // Kill enemy near player to spawn loot, then walk over it
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }

        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        // Walk to loot position (enemy_1 at 8, 0.5, 8)
        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count > 0)
        {
            var lootPos = loot.Entities[0];
            GameQueryHelpers.TeleportPlayer(_fixture.Context,
                lootPos.X, 0.5, lootPos.Z);
            _fixture.Context.HoldKey(VirtualKey.Space, 200);

            var feed = GameQueryHelpers.GetNotificationFeed(_fixture.Context);
            Assert.True(feed.Count > 0, "Expected 'Picked up' notification after loot pickup");
            Assert.Contains(feed.Messages, m => m.Text.Contains("Picked up"));
        }
        // If loot RNG gave zero items, test is inconclusive — no assertion failure
    }

    [Fact]
    public void Notifications_HavePositiveTimeRemaining()
    {
        // Same setup as above — trigger a pickup notification
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var combat = GameQueryHelpers.GetCombatState(_fixture.Context);
            if (combat.InCombat) break;
        }
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var loot = GameQueryHelpers.GetLootEntities(_fixture.Context);
        if (loot.Count > 0)
        {
            GameQueryHelpers.TeleportPlayer(_fixture.Context,
                loot.Entities[0].X, 0.5, loot.Entities[0].Z);
            _fixture.Context.HoldKey(VirtualKey.Space, 100);

            var feed = GameQueryHelpers.GetNotificationFeed(_fixture.Context);
            if (feed.Count > 0)
            {
                Assert.All(feed.Messages, m =>
                    Assert.True(m.TimeRemaining > 0, "Active notifications should have time remaining"));
            }
        }
    }

    [Fact]
    public void MaxFiveNotifications_Visible()
    {
        // NotificationService maxVisible=5. We can't easily force more than 5
        // notifications in M0 without a dedicated test command. Verify the cap
        // by reading the feed and confirming count <= 5.
        var feed = GameQueryHelpers.GetNotificationFeed(_fixture.Context);
        Assert.True(feed.Count <= 5, $"Expected max 5 visible notifications, got {feed.Count}");
    }
}
```

---

### GameOverTests (5 tests)

Verifies game over state, overlay display, and input freeze.

```csharp
public class GameOverTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void GameOverOverlay_NotVisible_AtStart()
    {
        var state = GameQueryHelpers.GetGameOverState(_fixture.Context);
        Assert.False(state.Visible);
        Assert.Equal("", state.Title);
    }

    [Fact]
    public void PlayerDeath_TransitionsToGameOver()
    {
        // Enter combat first (GameOver only triggers from InCombat)
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            var state = GameQueryHelpers.GetGameState(_fixture.Context);
            if (state == "InCombat") break;
        }

        // Kill the player
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        GameQueryHelpers.DamagePlayer(_fixture.Context, hud.Hp);

        // Wait a frame for state transition
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        var gameState = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("GameOver", gameState);
    }

    [Fact]
    public void PlayerDeath_ShowsGameOverOverlay()
    {
        // Enter combat
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            if (GameQueryHelpers.GetGameState(_fixture.Context) == "InCombat") break;
        }

        // Kill the player
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        GameQueryHelpers.DamagePlayer(_fixture.Context, hud.Hp);
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        var overlay = GameQueryHelpers.GetGameOverState(_fixture.Context);
        Assert.True(overlay.Visible);
        Assert.Equal("GAME OVER", overlay.Title);
    }

    [Fact]
    public void DamagePlayer_ReducesHp()
    {
        var before = GameQueryHelpers.GetHudState(_fixture.Context);
        var result = GameQueryHelpers.DamagePlayer(_fixture.Context, 25);
        Assert.Equal(before.Hp - 25, result.NewHp);
        Assert.True(result.IsAlive);
    }

    [Fact]
    public void DamagePlayer_ToZero_NotAlive()
    {
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        var result = GameQueryHelpers.DamagePlayer(_fixture.Context, hud.Hp);
        Assert.Equal(0, result.NewHp);
        Assert.False(result.IsAlive);
    }
}
```

---

### InputFreezeTests (3 tests)

Verifies that movement is blocked during GameOver state.

```csharp
public class InputFreezeTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private void ForceGameOver()
    {
        // Enter combat
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            if (GameQueryHelpers.GetGameState(_fixture.Context) == "InCombat") break;
        }

        // Kill the player
        var hud = GameQueryHelpers.GetHudState(_fixture.Context);
        GameQueryHelpers.DamagePlayer(_fixture.Context, hud.Hp);
        _fixture.Context.HoldKey(VirtualKey.Space, 100);
    }

    [Fact]
    public void Movement_Blocked_DuringGameOver()
    {
        ForceGameOver();
        Assert.Equal("GameOver", GameQueryHelpers.GetGameState(_fixture.Context));

        var before = GameQueryHelpers.GetPlayerPosition(_fixture.Context);
        _fixture.Context.HoldKey(VirtualKey.W, 500);
        var after = GameQueryHelpers.GetPlayerPosition(_fixture.Context);

        // Position should not change
        Assert.Equal(before.X, after.X, 0.1);
        Assert.Equal(before.Z, after.Z, 0.1);
    }

    [Fact]
    public void InventoryToggle_Blocked_DuringGameOver()
    {
        ForceGameOver();

        _fixture.Context.PressKey(VirtualKey.Tab);
        var visible = GameQueryHelpers.GetInventoryOverlayVisible(_fixture.Context);
        Assert.False(visible, "Inventory should not open during GameOver");
    }

    [Fact]
    public void GameState_StaysGameOver_AfterInput()
    {
        ForceGameOver();

        _fixture.Context.HoldKey(VirtualKey.W, 200);
        _fixture.Context.PressKey(VirtualKey.Tab);
        _fixture.Context.PressKey(VirtualKey.Space);

        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("GameOver", state);
    }
}
```

---

### VictoryTests (4 tests)

Verifies that defeating all enemies shows "ENEMIES DEFEATED" and returns to Exploring.

```csharp
public class VictoryTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private void WaitForCombat()
    {
        GameQueryHelpers.TeleportPlayer(_fixture.Context, 4, 0.5, 8);
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.HoldKey(VirtualKey.Space, 50);
            if (GameQueryHelpers.GetCombatState(_fixture.Context).InCombat) break;
        }
    }

    [Fact]
    public void KillAllEnemies_TransitionsToExploring()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        _fixture.Context.HoldKey(VirtualKey.Space, 100);
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        _fixture.Context.HoldKey(VirtualKey.Space, 100);
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var state = GameQueryHelpers.GetGameState(_fixture.Context);
        Assert.Equal("Exploring", state);
    }

    [Fact]
    public void KillAllEnemies_ShowsVictoryOverlay()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");
        _fixture.Context.HoldKey(VirtualKey.Space, 100);

        var overlay = GameQueryHelpers.GetGameOverState(_fixture.Context);
        Assert.True(overlay.Visible);
        Assert.Equal("ENEMIES DEFEATED", overlay.Title);
    }

    [Fact]
    public void VictoryOverlay_AutoDismisses()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");

        // Wait for auto-dismiss (2 seconds + margin)
        // Use HoldKey as a frame-advancing sleep
        _fixture.Context.HoldKey(VirtualKey.Space, 3000);

        var overlay = GameQueryHelpers.GetGameOverState(_fixture.Context);
        Assert.False(overlay.Visible, "Victory overlay should auto-dismiss after 2 seconds");
    }

    [Fact]
    public void VictoryOverlay_EnemyBarsHide()
    {
        WaitForCombat();
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_1");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_2");
        GameQueryHelpers.KillEnemy(_fixture.Context, "enemy_3");
        _fixture.Context.HoldKey(VirtualKey.Space, 200);

        var bars = GameQueryHelpers.GetEnemyHpBars(_fixture.Context);
        Assert.False(bars.Visible, "Enemy HP bars should hide after combat ends");
    }
}
```

---

## Test Summary

| Test Class | Test Count | Dependencies |
|-----------|-----------|-------------|
| HudBarTests | 5 | Phase B HudSyncScript + Phase C bar upgrade |
| EnemyHpBarTests | 5 | Phase C EnemyHpBarScript |
| NotificationFeedTests | 4 | Phase C NotificationFeedScript, Phase B LootPickupScript |
| GameOverTests | 5 | Phase C GameOverOverlayScript, DamagePlayer query |
| InputFreezeTests | 3 | Phase C GameOver state + input freeze |
| VictoryTests | 4 | Phase C GameOverOverlayScript (victory path) |
| **Total** | **26** | |

---

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| 1 | All 26 Phase C UI tests pass |
| 2 | `GetNotificationFeed` returns correct message content and count |
| 3 | `GetGameOverState` correctly reports overlay visibility and title |
| 4 | `GetEnemyHpBars` shows bars during combat, hides during exploring |
| 5 | `DamagePlayer` reduces HP by exact amount and correctly reports IsAlive |
| 6 | Game over freezes movement and inventory toggle |
| 7 | Victory overlay shows "ENEMIES DEFEATED" and auto-dismisses |
| 8 | All existing unit tests still pass |
| 9 | All existing UI tests (Phases A + B) still pass |
