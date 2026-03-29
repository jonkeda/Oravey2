using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Input;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;

namespace Oravey2.Core.Camera;

public class IsometricCameraScript : SyncScript
{
    /// <summary>
    /// The entity the camera follows (typically the player).
    /// </summary>
    public Entity? Target { get; set; }

    /// <summary>
    /// Camera pitch angle in degrees from horizontal.
    /// </summary>
    public float Pitch { get; set; } = 30f;

    /// <summary>
    /// Camera yaw angle in degrees (rotated around Y axis).
    /// </summary>
    public float Yaw { get; set; } = 45f;

    /// <summary>
    /// Distance from the camera to the target.
    /// </summary>
    public float Distance { get; set; } = 20f;

    /// <summary>
    /// Smooth follow speed (higher = faster tracking).
    /// </summary>
    public float FollowSmoothing { get; set; } = 5f;

    /// <summary>
    /// Minimum distance the target must move before the camera follows.
    /// </summary>
    public float Deadzone { get; set; } = 0.5f;

    /// <summary>
    /// Minimum orthographic size (closest zoom).
    /// </summary>
    public float ZoomMin { get; set; } = 10f;

    /// <summary>
    /// Maximum orthographic size (farthest zoom).
    /// </summary>
    public float ZoomMax { get; set; } = 40f;

    /// <summary>
    /// Current orthographic zoom size.
    /// </summary>
    public float CurrentZoom { get; set; } = 20f;

    /// <summary>
    /// Zoom speed per scroll tick.
    /// </summary>
    public float ZoomSpeed { get; set; } = 2f;

    /// <summary>
    /// Rotation snap increment in degrees.
    /// </summary>
    public float RotationSnap { get; set; } = 90f;

    private Vector3 _lastFollowTarget;
    private IEventBus? _eventBus;
    private IInputProvider? _inputProvider;

    public override void Start()
    {
        if (ServiceLocator.Instance.TryGet<IEventBus>(out var eventBus))
            _eventBus = eventBus;

        if (ServiceLocator.Instance.TryGet<IInputProvider>(out var inputProvider))
            _inputProvider = inputProvider;

        if (Target != null)
            _lastFollowTarget = Target.Transform.Position;

        // Set initial camera position
        UpdateCameraTransform(true);
    }

    public override void Update()
    {
        HandleZoomInput();
        HandleRotationInput();
        UpdateCameraTransform(false);
    }

    private void HandleZoomInput()
    {
        if (_inputProvider == null) return;

        if (_inputProvider.ScrollDelta != 0)
        {
            var oldZoom = CurrentZoom;
            CurrentZoom -= _inputProvider.ScrollDelta * ZoomSpeed;
            CurrentZoom = MathUtil.Clamp(CurrentZoom, ZoomMin, ZoomMax);

            if (Math.Abs(oldZoom - CurrentZoom) > 0.01f)
                _eventBus?.Publish(new CameraZoomChangedEvent(oldZoom, CurrentZoom));
        }
    }

    private void HandleRotationInput()
    {
        if (_inputProvider == null) return;

        if (_inputProvider.IsActionPressed(GameAction.RotateCameraLeft))
        {
            var oldYaw = Yaw;
            Yaw = (Yaw - RotationSnap) % 360f;
            if (Yaw < 0) Yaw += 360f;
            _eventBus?.Publish(new CameraRotatedEvent(oldYaw, Yaw));
        }

        if (_inputProvider.IsActionPressed(GameAction.RotateCameraRight))
        {
            var oldYaw = Yaw;
            Yaw = (Yaw + RotationSnap) % 360f;
            _eventBus?.Publish(new CameraRotatedEvent(oldYaw, Yaw));
        }
    }

    private void UpdateCameraTransform(bool immediate)
    {
        if (Target == null) return;

        var targetPos = Target.Transform.Position;

        // Deadzone check
        var delta = targetPos - _lastFollowTarget;
        if (delta.Length() < Deadzone && !immediate)
            return;

        // Smooth follow
        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        var followTarget = immediate
            ? targetPos
            : Vector3.Lerp(_lastFollowTarget, targetPos, 1f - MathF.Exp(-FollowSmoothing * dt));
        _lastFollowTarget = followTarget;

        // Calculate camera offset from pitch/yaw/distance
        var pitchRad = MathUtil.DegreesToRadians(Pitch);
        var yawRad = MathUtil.DegreesToRadians(Yaw);

        var offset = new Vector3(
            MathF.Cos(pitchRad) * MathF.Sin(yawRad) * Distance,
            MathF.Sin(pitchRad) * Distance,
            MathF.Cos(pitchRad) * MathF.Cos(yawRad) * Distance
        );

        Entity.Transform.Position = followTarget + offset;

        // Look at target
        var direction = followTarget - Entity.Transform.Position;
        if (direction.LengthSquared() > 0.0001f)
        {
            direction.Normalize();
            Entity.Transform.Rotation = Quaternion.LookRotation(direction, Vector3.UnitY);
        }

        // Set orthographic size on camera component
        var cameraComponent = Entity.Get<CameraComponent>();
        if (cameraComponent != null)
        {
            cameraComponent.Projection = CameraProjectionMode.Orthographic;
            cameraComponent.OrthographicSize = CurrentZoom;
        }
    }
}
