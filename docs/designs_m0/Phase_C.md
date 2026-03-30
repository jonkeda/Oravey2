# Design: Phase C — Minimal HUD & Game Over

Upgrades the text-only HUD from Phase B into a proper combat HUD with visual HP/AP bars, enemy HP bars, a notification feed, and game-over / victory states. After this phase, the player sees polished health bars during combat, floating damage text, and clear end-state screens.

**Depends on:** Phase A (combat working), Phase B (HudSyncScript, InventoryOverlayScript, NotificationService)

---

## Scope

| Sub-task | Summary |
|----------|---------|
| C1 | Upgrade HudSyncScript — visual HP/AP bars with color gradients, combat state banner |
| C2 | Enemy HP bars — floating bars above enemy heads, visible only during InCombat |
| C3 | Notification feed — wire NotificationService into the HUD, show timed messages |
| C4 | Game over state — detect player death, show overlay, freeze input |
| C5 | Victory text — show "ENEMIES DEFEATED" on combat end, auto-dismiss |
| C6 | Floating damage numbers — brief text at hit location showing damage dealt |

### What's Deferred

| Item | Deferred To |
|------|-------------|
| Minimap | Post-M0 |
| Quick-slot bar rendering | Post-M0 |
| Status effect icons | Post-M0 |
| Animated health bar transitions | Post-M0 |
| Enemy nameplate / level indicator | Post-M0 |
| Sound effects on game over / victory | Post-M0 |
| Restart from game over | Post-M0 (M0: game over freezes, user closes window) |

---

## File Layout

```
src/Oravey2.Core/
├── Framework/
│   └── State/
│       ├── GameState.cs                       # MODIFY — add GameOver enum value
│       └── GameStateManager.cs                # MODIFY — add GameOver transitions
├── Combat/
│   └── CombatSyncScript.cs                   # MODIFY — detect player death, publish events
├── UI/
│   └── Stride/
│       ├── HudSyncScript.cs                  # MODIFY — replace TextBlocks with bar UI
│       ├── EnemyHpBarScript.cs               # NEW — floating HP bars over enemies
│       ├── NotificationFeedScript.cs         # NEW — timed notification text feed
│       ├── GameOverOverlayScript.cs          # NEW — full-screen game over / victory overlay
│       └── FloatingDamageScript.cs           # NEW — damage numbers at hit positions
src/Oravey2.Windows/
├── Program.cs                                # MODIFY — wire new UI scripts
└── OraveyAutomationHandler.cs               # MODIFY — add Phase C automation queries
```

---

## Existing APIs We'll Use

### NotificationService (already implemented)

```csharp
void Add(string message, float durationSeconds = 3f)
void Update(float deltaSeconds)
IReadOnlyList<Notification> GetActive()     // snapshots of visible messages
```

Currently unused in the live game. Phase C wires it into the HUD.

### Events (already defined)

```csharp
AttackResolvedEvent(Hit, Damage, Location, Critical)   // for floating damage
EntityDiedEvent()                                       // for enemy death
CombatStartedEvent(EnemyIds[])                         // for showing enemy HP bars
CombatEndedEvent()                                     // for victory text
HealthChangedEvent(OldHP, NewHP)                       // for player death detection
NotificationEvent(Message, DurationSeconds)            // for notification feed
```

### HealthComponent

```csharp
int CurrentHP { get; }
int MaxHP { get; }
bool IsAlive { get; }
```

### CombatComponent

```csharp
float CurrentAP { get; }
int MaxAP { get; }
bool InCombat { get; set; }
```

### CombatSyncScript.Enemies

```csharp
internal List<EnemyInfo> Enemies { get; set; }
// EnemyInfo: Entity, Id, Health, Combat
```

---

## C1 — Upgrade HudSyncScript

### Design

Replace the plain TextBlock HUD with a visual layout:

```
┌──────────────────────────────────────┐
│ HP ████████████░░░░  75/105          │  ← green→yellow→red gradient
│ AP ██████░░░░░░░░░░  6/10           │  ← blue bar
│ LVL 1  XP 0/100                     │
│ [Exploring]                          │  ← State banner (changes color per state)
└──────────────────────────────────────┘
```

Each "bar" is a `Grid` with two overlapping children:
1. Background `Border` (dark gray, full width)
2. Foreground `Border` (colored, width = fraction of max)

Bar width is 200px. The bar fills as a fraction: `HP bar width = (CurrentHP / MaxHP) × 200`.

### HP Bar Color

Computed each frame based on HP fraction:
- ≥ 60%: Green `(0.2, 0.8, 0.2)`
- 25–59%: Yellow `(0.9, 0.8, 0.1)`
- < 25%: Red `(0.9, 0.2, 0.1)`

### State Banner Color

- `Exploring`: LightGray
- `InCombat`: OrangeRed
- `GameOver`: DarkRed

### Implementation

Modify the existing `HudSyncScript` in-place. The `Start()` method rebuilds the UI with a `Grid`-based layout instead of the current `StackPanel` of `TextBlock`s. The `Update()` loop adjusts bar widths and colors.

```csharp
public override void Start()
{
    base.Start();

    // HP row
    _hpBarBg = new Border
    {
        BackgroundColor = new Color(40, 40, 40, 200),
        Width = BarWidth,
        Height = BarHeight,
    };
    _hpBarFill = new Border
    {
        BackgroundColor = new Color(50, 200, 50),
        Width = BarWidth,
        Height = BarHeight,
        HorizontalAlignment = HorizontalAlignment.Left,
    };
    _hpText = new TextBlock
    {
        Text = "HP: --/--",
        TextSize = 14,
        TextColor = Color.White,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(BarWidth + 8, 0, 0, 0),
    };
    var hpRow = new Grid { Children = { _hpBarBg, _hpBarFill, _hpText } };

    // AP row (same pattern, blue fill)
    // ... identical structure with _apBarBg, _apBarFill, _apText

    // Level text
    _levelText = new TextBlock { ... };

    // State banner
    _stateText = new TextBlock { ... };

    var stack = new StackPanel
    {
        Orientation = Orientation.Vertical,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        BackgroundColor = new Color(0, 0, 0, 120),
        Margin = new Thickness(10, 10, 0, 0),
        Children = { hpRow, apRow, _levelText, _stateText }
    };

    var page = new UIPage { RootElement = stack };
    Entity.Add(new UIComponent { Page = page });
}

public override void Update()
{
    if (Health != null)
    {
        float frac = (float)Health.CurrentHP / Health.MaxHP;
        _hpBarFill!.Width = frac * BarWidth;
        _hpBarFill.BackgroundColor = frac >= 0.6f
            ? new Color(50, 200, 50)
            : frac >= 0.25f
                ? new Color(230, 200, 25)
                : new Color(230, 50, 25);
        _hpText!.Text = $"{Health.CurrentHP}/{Health.MaxHP}";
    }

    if (Combat != null)
    {
        float apFrac = Combat.CurrentAP / Combat.MaxAP;
        _apBarFill!.Width = apFrac * BarWidth;
        _apText!.Text = $"{Combat.CurrentAP:F0}/{Combat.MaxAP}";
    }

    // Level and state text updates (same as before)
}
```

### Constants

```csharp
private const float BarWidth = 200f;
private const float BarHeight = 16f;
```

---

## C2 — Enemy HP Bars

### Design

A new `EnemyHpBarScript` SyncScript attached to the CombatManager entity. During `InCombat`, it creates and updates a small HP bar above each living enemy's world position using Stride's 3D-to-screen projection.

**Approach:** Use a single Stride `UIComponent` with a `Canvas` layout. Each enemy gets a small HP bar (100×8 px) positioned using screen-space coordinates derived from `WorldToScreen()` projection.

### Why Canvas

Stride's `Canvas` panel supports absolute positioning via `Canvas.PinOrigin` + `Canvas.AbsolutePosition`. This lets us place UI elements at arbitrary screen positions — necessary for "floating" bars that track world-space entities.

### Script Design

```csharp
public class EnemyHpBarScript : SyncScript
{
    public GameStateManager? StateManager { get; set; }
    internal List<EnemyInfo>? Enemies { get; set; }

    // We need the camera to project world→screen
    public Entity? CameraEntity { get; set; }

    private Canvas? _canvas;
    private UIComponent? _uiComponent;
    private readonly Dictionary<string, (Border bg, Border fill, TextBlock text)> _bars = [];

    private const float EnemyBarWidth = 100f;
    private const float EnemyBarHeight = 8f;
    private const float BarOffsetY = -1.5f; // World units above enemy head

    public override void Start()
    {
        base.Start();
        _canvas = new Canvas();
        var page = new UIPage { RootElement = _canvas };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);
    }

    public override void Update()
    {
        if (StateManager?.CurrentState != GameState.InCombat || Enemies == null)
        {
            // Hide all bars when not in combat
            if (_canvas != null)
                _canvas.Visibility = Visibility.Collapsed;
            return;
        }

        _canvas!.Visibility = Visibility.Visible;
        var aliveEnemies = Enemies.Where(e => e.Health.IsAlive).ToList();

        // Remove bars for dead enemies
        foreach (var id in _bars.Keys.Except(aliveEnemies.Select(e => e.Id)).ToList())
        {
            var (bg, fill, text) = _bars[id];
            _canvas.Children.Remove(bg);
            _canvas.Children.Remove(fill);
            _canvas.Children.Remove(text);
            _bars.Remove(id);
        }

        foreach (var enemy in aliveEnemies)
        {
            // Ensure bar exists
            if (!_bars.ContainsKey(enemy.Id))
                CreateEnemyBar(enemy.Id);

            // Project enemy position to screen space
            var worldPos = enemy.Entity.Transform.Position + new Vector3(0, BarOffsetY, 0);
            var screenPos = ProjectToScreen(worldPos);

            if (screenPos == null) continue; // Off-screen

            var (bg, fill, text) = _bars[enemy.Id];
            var frac = (float)enemy.Health.CurrentHP / enemy.Health.MaxHP;

            // Position bars at screen coordinates
            bg.SetCanvasAbsolutePosition(new Vector3(screenPos.Value.X - EnemyBarWidth / 2, screenPos.Value.Y, 0));
            fill.SetCanvasAbsolutePosition(new Vector3(screenPos.Value.X - EnemyBarWidth / 2, screenPos.Value.Y, 0));
            fill.Width = frac * EnemyBarWidth;

            // HP color (same gradient as player)
            fill.BackgroundColor = frac >= 0.6f
                ? new Color(50, 200, 50)
                : frac >= 0.25f
                    ? new Color(230, 200, 25)
                    : new Color(230, 50, 25);

            text.SetCanvasAbsolutePosition(new Vector3(screenPos.Value.X - EnemyBarWidth / 2, screenPos.Value.Y - 14, 0));
            text.Text = $"{enemy.Health.CurrentHP}/{enemy.Health.MaxHP}";
        }
    }

    private void CreateEnemyBar(string enemyId)
    {
        var bg = new Border
        {
            BackgroundColor = new Color(40, 40, 40, 180),
            Width = EnemyBarWidth,
            Height = EnemyBarHeight,
        };
        var fill = new Border
        {
            BackgroundColor = new Color(230, 50, 25),
            Width = EnemyBarWidth,
            Height = EnemyBarHeight,
        };
        var text = new TextBlock
        {
            TextSize = 11,
            TextColor = Color.White,
        };

        _canvas!.Children.Add(bg);
        _canvas.Children.Add(fill);
        _canvas.Children.Add(text);
        _bars[enemyId] = (bg, fill, text);
    }

    private Vector2? ProjectToScreen(Vector3 worldPos)
    {
        // Use camera projection matching OraveyAutomationHandler.BuildCameraViewProj
        var cc = CameraEntity?.Get<CameraComponent>();
        if (cc == null) return null;

        var camScript = CameraEntity!.Get<IsometricCameraScript>();
        if (camScript?.Target == null) return null;

        var targetPos = camScript.Target.Transform.Position;
        var pitchRad = MathUtil.DegreesToRadians(camScript.Pitch);
        var yawRad = MathUtil.DegreesToRadians(camScript.Yaw);

        var offset = new Vector3(
            MathF.Cos(pitchRad) * MathF.Sin(yawRad) * camScript.Distance,
            MathF.Sin(pitchRad) * camScript.Distance,
            MathF.Cos(pitchRad) * MathF.Cos(yawRad) * camScript.Distance);
        var camPos = targetPos + offset;
        var camRot = Quaternion.RotationYawPitchRoll(
            yawRad, MathUtil.DegreesToRadians(-camScript.Pitch), 0f);

        Matrix.RotationQuaternion(ref camRot, out var rotMatrix);
        var worldMatrix = rotMatrix;
        worldMatrix.TranslationVector = camPos;
        Matrix.Invert(ref worldMatrix, out var viewMatrix);

        cc.Update();
        var projMatrix = cc.ProjectionMatrix;
        var viewProj = viewMatrix * projMatrix;

        var clipPos = Vector3.TransformCoordinate(worldPos, viewProj);
        var backBuffer = Game.GraphicsDevice.Presenter.BackBuffer;

        float screenX = (clipPos.X * 0.5f + 0.5f) * backBuffer.Width;
        float screenY = (1f - (clipPos.Y * 0.5f + 0.5f)) * backBuffer.Height;

        float normX = screenX / backBuffer.Width;
        float normY = screenY / backBuffer.Height;

        // Off-screen check
        if (normX < -0.1f || normX > 1.1f || normY < -0.1f || normY > 1.1f)
            return null;

        return new Vector2(screenX, screenY);
    }
}
```

---

## C3 — Notification Feed

### Design

A `NotificationFeedScript` SyncScript that renders active notifications from `NotificationService` as a stack of text messages at the bottom-center of the screen.

```
                    ┌─────────────────────────┐
                    │  Picked up Scrap Metal   │
                    │  Picked up Medkit         │  ← bottom-center, fades
                    └─────────────────────────┘
```

Messages appear at the bottom, newest on top. Each fades after its timer expires (handled by NotificationService).

### Events → NotificationService Wiring

Wire `NotificationEvent` subscribers in CombatSyncScript or a new lightweight event listener script so that any published `NotificationEvent` automatically calls `NotificationService.Add()`.

### Script Design

```csharp
public class NotificationFeedScript : SyncScript
{
    public NotificationService? Notifications { get; set; }

    private StackPanel? _stack;
    private UIComponent? _uiComponent;

    public override void Start()
    {
        base.Start();

        _stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 80),
        };

        var page = new UIPage { RootElement = _stack };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);
    }

    public override void Update()
    {
        if (Notifications == null || _stack == null) return;

        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        Notifications.Update(dt);

        var active = Notifications.GetActive();

        // Rebuild text list each frame (simple for M0, no object pooling)
        _stack.Children.Clear();

        foreach (var notification in active)
        {
            // Fade: alpha reduces as time remaining approaches 0
            var alpha = Math.Clamp(notification.TimeRemaining / 0.5f, 0f, 1f);
            var color = new Color(255, 255, 255, (byte)(alpha * 255));

            _stack.Children.Add(new TextBlock
            {
                Text = notification.Message,
                TextSize = 16,
                TextColor = color,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2),
            });
        }
    }
}
```

### Wiring NotificationEvent → NotificationService

In `Program.cs`, subscribe to `NotificationEvent` and route to the service:

```csharp
var notificationService = new NotificationService();
eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));
```

This means any code that publishes `NotificationEvent` (e.g., loot pickup in Phase B) automatically feeds the HUD.

---

## C4 — Game Over State

### Design

When the player's HP reaches 0 during combat, the game should:
1. Transition to `GameState.GameOver`
2. Display a full-screen semi-transparent overlay with "GAME OVER" text
3. Freeze all input (movement, combat, UI toggles)

### GameState Changes

Add `GameOver` to the `GameState` enum:

```csharp
public enum GameState
{
    Loading,
    Exploring,
    InCombat,
    InDialogue,
    InMenu,
    Paused,
    GameOver    // NEW
}
```

Add valid transitions in `GameStateManager.IsValidTransition`:

```csharp
(GameState.InCombat, GameState.GameOver) => true,    // player dies during combat
```

No transitions OUT of `GameOver` for M0 — the game freezes. (Post-M0 will add restart.)

### CombatSyncScript Changes

Add a player death check at the end of `Update()`, after `CleanupDead()`:

```csharp
// 6. Check for player death
if (PlayerHealth != null && !PlayerHealth.IsAlive)
{
    StateManager?.TransitionTo(GameState.GameOver);
    return; // Stop processing combat
}
```

The `Update()` method already gates on `StateManager.CurrentState != GameState.InCombat`, so once we transition to `GameOver`, the combat loop stops naturally.

### Input Freeze

The `PlayerMovementScript` already checks `GameStateManager.CurrentState` — but currently it only blocks movement during `InCombat`. Extend to also block during `GameOver`:

No — actually, `PlayerMovementScript` runs independently. The simplest freeze for M0: `CombatSyncScript.Update()` stops processing (done via the state check). `PlayerMovementScript` needs a guard:

```csharp
// In PlayerMovementScript.Update():
if (StateManager?.CurrentState == GameState.GameOver)
    return;
```

This requires adding a `GameStateManager? StateManager` property to `PlayerMovementScript`, wired in Program.cs.

### GameOverOverlayScript

A `SyncScript` that listens to game state and shows/hides the overlay.

```csharp
public class GameOverOverlayScript : SyncScript
{
    public GameStateManager? StateManager { get; set; }

    private UIComponent? _uiComponent;
    private Border? _overlay;
    private TextBlock? _titleText;
    private TextBlock? _subtitleText;

    /// <summary>
    /// Exposes the current overlay text for automation queries.
    /// </summary>
    public string? CurrentTitle => _titleText?.Text;

    /// <summary>
    /// Whether the overlay is currently visible.
    /// </summary>
    public bool IsVisible => _overlay?.Visibility == Visibility.Visible;

    private GameState _lastState;

    public override void Start()
    {
        base.Start();

        _titleText = new TextBlock
        {
            Text = "",
            TextSize = 48,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _subtitleText = new TextBlock
        {
            Text = "",
            TextSize = 20,
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 60, 0, 0),
        };

        var textStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _titleText, _subtitleText },
        };

        _overlay = new Border
        {
            BackgroundColor = new Color(0, 0, 0, 180),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = textStack,
            Visibility = Visibility.Collapsed,
        };

        var page = new UIPage { RootElement = _overlay };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);

        _lastState = StateManager?.CurrentState ?? GameState.Loading;
    }

    public override void Update()
    {
        if (StateManager == null || _overlay == null) return;

        var currentState = StateManager.CurrentState;
        if (currentState == _lastState) return;

        // Detect transitions
        if (currentState == GameState.GameOver)
        {
            ShowOverlay("GAME OVER", "You have been killed.");
        }
        else if (_lastState == GameState.InCombat && currentState == GameState.Exploring)
        {
            // Combat ended with player alive = victory
            ShowOverlay("ENEMIES DEFEATED", "Returning to exploration...");
            // Auto-dismiss after 2 seconds
            _dismissTimer = 2f;
        }
        else if (_overlay.Visibility == Visibility.Visible && currentState != GameState.GameOver)
        {
            HideOverlay();
        }

        _lastState = currentState;
    }

    // --- Victory auto-dismiss ---
    private float _dismissTimer;

    // (called from Update, continued)
    // After the state check above, add timer logic:
    // if (_dismissTimer > 0)
    // {
    //     _dismissTimer -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
    //     if (_dismissTimer <= 0) HideOverlay();
    // }

    private void ShowOverlay(string title, string subtitle)
    {
        _titleText!.Text = title;
        _subtitleText!.Text = subtitle;
        _overlay!.Visibility = Visibility.Visible;
    }

    private void HideOverlay()
    {
        _overlay!.Visibility = Visibility.Collapsed;
        _titleText!.Text = "";
        _subtitleText!.Text = "";
    }
}
```

> **Implementation note:** The victory auto-dismiss timer should be in the main `Update()` method body, after the state-change detection block.

---

## C5 — Victory Text

Handled by `GameOverOverlayScript` (C4). When `CombatSyncScript.CleanupDead()` removes the last enemy, `CombatStateManager.ExitCombat()` transitions to `Exploring`. The overlay script detects the `InCombat → Exploring` transition and shows "ENEMIES DEFEATED" for 2 seconds.

No separate script needed.

---

## C6 — Floating Damage Numbers

### Design

A `FloatingDamageScript` that subscribes to `AttackResolvedEvent` and shows brief damage numbers at the target's screen position. Numbers float upward and fade over 1 second.

### Approach

Use the same Canvas-based screen-space positioning as enemy HP bars. Each damage number is a `TextBlock` placed at the target's projected screen position and animated upward by adjusting its Y position each frame.

### Script Design

```csharp
public class FloatingDamageScript : SyncScript
{
    public Entity? CameraEntity { get; set; }
    public IEventBus? EventBus { get; set; }

    // The script needs to know target positions. Since AttackResolvedEvent doesn't
    // include a target entity reference, we listen to the event and read the last-known
    // target from CombatSyncScript.
    public CombatSyncScript? CombatScript { get; set; }

    private Canvas? _canvas;
    private UIComponent? _uiComponent;
    private readonly List<DamagePopup> _popups = [];

    private const float PopupDuration = 1.0f;
    private const float FloatSpeed = 60f; // pixels per second upward
    private const float PopupTextSize = 18f;

    private sealed class DamagePopup
    {
        public TextBlock Text { get; init; } = null!;
        public float ScreenX { get; init; }
        public float ScreenY { get; set; }
        public float TimeRemaining { get; set; }
    }

    public override void Start()
    {
        base.Start();
        _canvas = new Canvas();
        var page = new UIPage { RootElement = _canvas };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);

        EventBus?.Subscribe<AttackResolvedEvent>(OnAttackResolved);
    }

    private void OnAttackResolved(AttackResolvedEvent e)
    {
        if (!e.Hit) return;

        // Determine target screen position
        // For M0, use the last action from CombatScript's Queue processing.
        // The FloatingDamageScript is wired with a reference to get the target entity.
        // We'll queue the popup data and process it next frame.
        _pendingDamage.Enqueue((e.Damage, e.Critical));
    }

    private readonly Queue<(int Damage, bool Critical)> _pendingDamage = new();

    // The target entity for the most recent attack is tracked by CombatSyncScript.
    // We add a public property there: Entity? LastHitTarget { get; set; }
    // Set in ProcessNextAction() right before StartHitFlash().

    public override void Update()
    {
        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;

        // Spawn pending popups
        while (_pendingDamage.TryDequeue(out var dmg))
        {
            var targetEntity = CombatScript?.LastHitTarget;
            if (targetEntity == null) continue;

            var worldPos = targetEntity.Transform.Position + new Vector3(0, 1.2f, 0);
            var screenPos = ProjectToScreen(worldPos);
            if (screenPos == null) continue;

            var color = dmg.Critical
                ? new Color(255, 200, 50)   // Gold for crits
                : new Color(255, 255, 255); // White for normal
            var prefix = dmg.Critical ? "CRIT " : "";

            var text = new TextBlock
            {
                Text = $"{prefix}{dmg.Damage}",
                TextSize = dmg.Critical ? PopupTextSize + 4 : PopupTextSize,
                TextColor = color,
            };

            _canvas!.Children.Add(text);
            text.SetCanvasAbsolutePosition(new Vector3(screenPos.Value.X, screenPos.Value.Y, 0));

            _popups.Add(new DamagePopup
            {
                Text = text,
                ScreenX = screenPos.Value.X,
                ScreenY = screenPos.Value.Y,
                TimeRemaining = PopupDuration,
            });
        }

        // Update existing popups
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            var popup = _popups[i];
            popup.TimeRemaining -= dt;
            popup.ScreenY -= FloatSpeed * dt; // Float upward

            if (popup.TimeRemaining <= 0)
            {
                _canvas!.Children.Remove(popup.Text);
                _popups.RemoveAt(i);
                continue;
            }

            // Fade out
            var alpha = Math.Clamp(popup.TimeRemaining / 0.3f, 0f, 1f);
            var baseColor = popup.Text.TextColor;
            popup.Text.TextColor = new Color(
                baseColor.R, baseColor.G, baseColor.B, (byte)(alpha * 255));
            popup.Text.SetCanvasAbsolutePosition(
                new Vector3(popup.ScreenX, popup.ScreenY, 0));
        }
    }

    private Vector2? ProjectToScreen(Vector3 worldPos)
    {
        // Same projection logic as EnemyHpBarScript (see C2)
        // Shared via a static helper or duplicated for M0 simplicity
        var cc = CameraEntity?.Get<CameraComponent>();
        if (cc == null) return null;

        var camScript = CameraEntity!.Get<IsometricCameraScript>();
        if (camScript?.Target == null) return null;

        var targetPos = camScript.Target.Transform.Position;
        var pitchRad = MathUtil.DegreesToRadians(camScript.Pitch);
        var yawRad = MathUtil.DegreesToRadians(camScript.Yaw);

        var offset = new Vector3(
            MathF.Cos(pitchRad) * MathF.Sin(yawRad) * camScript.Distance,
            MathF.Sin(pitchRad) * camScript.Distance,
            MathF.Cos(pitchRad) * MathF.Cos(yawRad) * camScript.Distance);
        var camPos = targetPos + offset;
        var camRot = Quaternion.RotationYawPitchRoll(
            yawRad, MathUtil.DegreesToRadians(-camScript.Pitch), 0f);

        Matrix.RotationQuaternion(ref camRot, out var rotMatrix);
        var worldMatrix = rotMatrix;
        worldMatrix.TranslationVector = camPos;
        Matrix.Invert(ref worldMatrix, out var viewMatrix);

        cc.Update();
        var projMatrix = cc.ProjectionMatrix;
        var viewProj = viewMatrix * projMatrix;

        var clipPos = Vector3.TransformCoordinate(worldPos, viewProj);
        var backBuffer = Game.GraphicsDevice.Presenter.BackBuffer;

        float screenX = (clipPos.X * 0.5f + 0.5f) * backBuffer.Width;
        float screenY = (1f - (clipPos.Y * 0.5f + 0.5f)) * backBuffer.Height;

        float normX = screenX / backBuffer.Width;
        float normY = screenY / backBuffer.Height;

        if (normX < -0.1f || normX > 1.1f || normY < -0.1f || normY > 1.1f)
            return null;

        return new Vector2(screenX, screenY);
    }
}
```

### CombatSyncScript Change

Add a `LastHitTarget` property for floating damage to read:

```csharp
/// <summary>
/// Set during ProcessNextAction() for FloatingDamageScript to read.
/// Reset to null at the start of each frame.
/// </summary>
public Entity? LastHitTarget { get; internal set; }
```

In `Update()`, at the top: `LastHitTarget = null;`

In `ProcessNextAction()`, before `StartHitFlash(targetEntity)`:
```csharp
LastHitTarget = targetEntity;
```

---

## Program.cs Changes

### New using statements

```csharp
// (no new namespace imports needed — all UI.Stride types already imported)
```

### After existing HUD section, add new UI scripts:

```csharp
// --- Notification feed (Phase C) ---
var notificationService = new NotificationService();
eventBus.Subscribe<NotificationEvent>(e => notificationService.Add(e.Message, e.DurationSeconds));

var notificationEntity = new Entity("NotificationFeed");
var notificationFeed = new NotificationFeedScript
{
    Notifications = notificationService,
};
notificationEntity.Add(notificationFeed);
rootScene.Entities.Add(notificationEntity);

// --- Enemy HP bars (Phase C) ---
var enemyHpEntity = new Entity("EnemyHpBars");
var enemyHpBars = new EnemyHpBarScript
{
    StateManager = gameStateManager,
    CameraEntity = cameraEntity,
};
enemyHpBars.Enemies = enemies;
enemyHpEntity.Add(enemyHpBars);
rootScene.Entities.Add(enemyHpEntity);

// --- Game over / victory overlay (Phase C) ---
var gameOverEntity = new Entity("GameOverOverlay");
var gameOverOverlay = new GameOverOverlayScript
{
    StateManager = gameStateManager,
};
gameOverEntity.Add(gameOverOverlay);
rootScene.Entities.Add(gameOverEntity);

// --- Floating damage (Phase C) ---
var floatingDamageEntity = new Entity("FloatingDamage");
var floatingDamage = new FloatingDamageScript
{
    CameraEntity = cameraEntity,
    EventBus = eventBus,
    CombatScript = combatScript,
};
floatingDamageEntity.Add(floatingDamage);
rootScene.Entities.Add(floatingDamageEntity);

// --- Wire player movement freeze on GameOver (Phase C) ---
playerMovement.StateManager = gameStateManager;
```

### Automation handler wiring

Add Phase C references to `OraveyAutomationHandler`:

```csharp
oraveyHandler.SetPhaseC(notificationService, gameOverOverlay);
```

---

## Automation Queries (for UI Tests)

New queries to add to `OraveyAutomationHandler.cs`:

| Query | Args | Returns | Purpose |
|-------|------|---------|---------|
| `GetNotificationFeed` | — | `{ count, messages: [{text, timeRemaining}] }` | Current visible notifications |
| `GetGameOverState` | — | `{ visible, title, subtitle }` | Game over overlay state |
| `GetEnemyHpBars` | — | `{ visible, bars: [{enemyId, hp, maxHp}] }` | Enemy HP bar data |
| `DamagePlayer` | `amount` | `{ newHp, maxHp, isAlive }` | Force damage to player for testing game over |

### Why DamagePlayer

Testing game over requires getting the player to 0 HP. Combat RNG makes this unreliable. `DamagePlayer` forces exact HP reduction for deterministic testing, similar to how `KillEnemy` forces enemy death.

### Implementation: OraveyAutomationHandler additions

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

Reads from the same `Enemies` list that `EnemyHpBarScript` uses:

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

---

## Phase Dependency Order

```
C1 (HUD bars)              — independent, enhances existing HudSyncScript
C2 (enemy HP bars)          — independent, reads from Enemies list
C3 (notification feed)      — independent, wires NotificationService
C4 (game over state)        — requires C1 (visual feedback), GameState change
C5 (victory text)           — part of C4 (GameOverOverlayScript handles both)
C6 (floating damage)        — independent, reads AttackResolvedEvent

Implementation order: C1 → C3 → C2 → C6 → C4/C5
```

---

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| 1 | Player HUD shows colored HP bar (green→yellow→red based on HP%) and AP bar (blue) |
| 2 | HP/AP bars update in real-time during combat |
| 3 | Enemy HP bars appear above enemies during combat, hidden during exploring |
| 4 | Enemy HP bars disappear when enemy dies |
| 5 | Notifications appear at bottom-center and fade after their duration |
| 6 | Player death transitions to GameOver state and shows "GAME OVER" overlay |
| 7 | Input is frozen during GameOver (no movement, no combat, no Tab) |
| 8 | Defeating all enemies shows "ENEMIES DEFEATED" text that auto-dismisses after 2s |
| 9 | Floating damage numbers appear at hit location and float upward |
| 10 | Critical hits show gold "CRIT" text |
| 11 | `GetGameOverState` automation query returns correct overlay state |
| 12 | `DamagePlayer` automation query allows deterministic game over testing |
| 13 | All existing unit tests still pass |
| 14 | All existing UI tests still pass |
