using Oravey2.Core.Camera;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.State;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Color = Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Shows small HP bars above each living enemy during combat.
/// Uses Canvas absolute positioning with world→screen projection.
/// </summary>
public class EnemyHpBarScript : SyncScript
{
    public GameStateManager? StateManager { get; set; }
    internal List<EnemyInfo>? Enemies { get; set; }
    public Entity? CameraEntity { get; set; }
    public SpriteFont? Font { get; set; }

    private Canvas? _canvas;
    private UIComponent? _uiComponent;
    private readonly Dictionary<string, (Border Bg, Border Fill, TextBlock Text)> _bars = [];

    private const float EnemyBarWidth = 100f;
    private const float EnemyBarHeight = 8f;
    private const float BarOffsetY = 1.5f; // World units above enemy position

    public override void Start()
    {
        base.Start();
        _canvas = new Canvas { Visibility = Visibility.Collapsed };
        var page = new UIPage { RootElement = _canvas };
        _uiComponent = new UIComponent { Page = page };
        Entity.Add(_uiComponent);
    }

    public override void Update()
    {
        if (StateManager?.CurrentState != GameState.InCombat || Enemies == null)
        {
            if (_canvas != null)
                _canvas.Visibility = Visibility.Collapsed;
            return;
        }

        _canvas!.Visibility = Visibility.Visible;
        var aliveEnemies = Enemies.Where(e => e.Health.IsAlive).ToList();

        // Remove bars for dead enemies
        var aliveIds = new HashSet<string>(aliveEnemies.Select(e => e.Id));
        foreach (var id in _bars.Keys.Where(k => !aliveIds.Contains(k)).ToList())
        {
            var (bg, fill, text) = _bars[id];
            _canvas.Children.Remove(bg);
            _canvas.Children.Remove(fill);
            _canvas.Children.Remove(text);
            _bars.Remove(id);
        }

        foreach (var enemy in aliveEnemies)
        {
            if (!_bars.ContainsKey(enemy.Id))
                CreateEnemyBar(enemy.Id);

            var worldPos = enemy.Entity.Transform.Position + new Vector3(0, BarOffsetY, 0);
            var screenPos = ProjectToScreen(worldPos);
            if (screenPos == null) continue;

            var (bg, fill, text) = _bars[enemy.Id];
            var frac = (float)enemy.Health.CurrentHP / enemy.Health.MaxHP;

            bg.SetCanvasAbsolutePosition(new Vector3(screenPos.Value.X - EnemyBarWidth / 2, screenPos.Value.Y, 0));
            fill.SetCanvasAbsolutePosition(new Vector3(screenPos.Value.X - EnemyBarWidth / 2, screenPos.Value.Y, 0));
            fill.Width = frac * EnemyBarWidth;
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
            Font = Font,
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
        var cc = CameraEntity?.Get<CameraComponent>();
        if (cc == null) return null;

        var camScript = CameraEntity!.Get<TacticalCameraScript>();
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
