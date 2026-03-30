using Oravey2.Core.Camera;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.Events;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Color = Stride.Core.Mathematics.Color;

namespace Oravey2.Core.UI.Stride;

/// <summary>
/// Shows floating damage numbers at hit locations.
/// Numbers float upward and fade over 1 second.
/// </summary>
public class FloatingDamageScript : SyncScript
{
    public Entity? CameraEntity { get; set; }
    public IEventBus? EventBus { get; set; }
    public CombatSyncScript? CombatScript { get; set; }

    private Canvas? _canvas;
    private UIComponent? _uiComponent;
    private readonly List<DamagePopup> _popups = [];
    private readonly Queue<(int Damage, bool Critical)> _pendingDamage = new();

    private const float PopupDuration = 1.0f;
    private const float FloatSpeed = 60f;
    private const float PopupTextSize = 18f;

    private sealed class DamagePopup
    {
        public TextBlock Text { get; init; } = null!;
        public float ScreenX { get; init; }
        public float ScreenY { get; set; }
        public float TimeRemaining { get; set; }
        public Color BaseColor { get; init; }
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
        _pendingDamage.Enqueue((e.Damage, e.Critical));
    }

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
                ? new Color(255, 200, 50)
                : new Color(255, 255, 255);
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
                BaseColor = color,
            });
        }

        // Update existing popups
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            var popup = _popups[i];
            popup.TimeRemaining -= dt;
            popup.ScreenY -= FloatSpeed * dt;

            if (popup.TimeRemaining <= 0)
            {
                _canvas!.Children.Remove(popup.Text);
                _popups.RemoveAt(i);
                continue;
            }

            var alpha = Math.Clamp(popup.TimeRemaining / 0.3f, 0f, 1f);
            popup.Text.TextColor = new Color(
                popup.BaseColor.R, popup.BaseColor.G, popup.BaseColor.B, (byte)(alpha * 255));
            popup.Text.SetCanvasAbsolutePosition(
                new Vector3(popup.ScreenX, popup.ScreenY, 0));
        }
    }

    private Vector2? ProjectToScreen(Vector3 worldPos)
    {
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
