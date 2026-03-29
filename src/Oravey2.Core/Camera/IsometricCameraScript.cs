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
    public float Distance { get; set; } = 50f;

    /// <summary>
    /// Smooth follow speed (higher = faster tracking).
    /// </summary>
    public float FollowSmoothing { get; set; } = 5f;

    /// <summary>
    /// Minimum distance the target must move before the camera follows.
    /// </summary>
    public float Deadzone { get; set; } = 0.5f;

    /// <summary>
    /// Minimum vertical FOV in degrees (most zoomed in).
    /// </summary>
    public float FovMin { get; set; } = 10f;

    /// <summary>
    /// Maximum vertical FOV in degrees (most zoomed out).
    /// </summary>
    public float FovMax { get; set; } = 50f;

    /// <summary>
    /// Current vertical field of view in degrees.
    /// </summary>
    public float CurrentFov { get; set; } = 25f;

    /// <summary>
    /// Zoom speed in degrees per second while zoom key held.
    /// </summary>
    public float ZoomSpeed { get; set; } = 15f;

    /// <summary>
    /// Rotation speed in degrees per second while Q/E is held.
    /// </summary>
    public float RotationSpeed { get; set; } = 120f;

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
        var rotChanged = HandleRotationInput();
        var zoomChanged = HandleZoomInput();
        UpdateCameraTransform(false, rotChanged || zoomChanged);
    }

    private bool HandleZoomInput()
    {
        if (_inputProvider == null) return false;

        var zoomAxis = _inputProvider.ZoomAxis;
        if (zoomAxis == 0f) return false;

        var oldFov = CurrentFov;
        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        CurrentFov += zoomAxis * ZoomSpeed * dt;
        CurrentFov = MathUtil.Clamp(CurrentFov, FovMin, FovMax);

        if (Math.Abs(oldFov - CurrentFov) > 0.01f)
        {
            _eventBus?.Publish(new CameraZoomChangedEvent(oldFov, CurrentFov));
            return true;
        }
        return false;
    }

    private bool HandleRotationInput()
    {
        if (_inputProvider == null) return false;

        var rotAxis = _inputProvider.RotationAxis;
        if (rotAxis == 0f) return false;

        var oldYaw = Yaw;
        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        Yaw = (Yaw + rotAxis * RotationSpeed * dt) % 360f;
        if (Yaw < 0) Yaw += 360f;

        _eventBus?.Publish(new CameraRotatedEvent(oldYaw, Yaw));
        return true;
    }

    private void UpdateCameraTransform(bool immediate, bool forceUpdate = false)
    {
        if (Target == null) return;

        var targetPos = Target.Transform.Position;

        // Deadzone check — skip only if player hasn't moved AND no rotation/zoom change
        var delta = targetPos - _lastFollowTarget;
        if (delta.Length() < Deadzone && !immediate && !forceUpdate)
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

        // Build camera rotation by looking at the target.
        // Compute forward/right/up vectors and build rotation matrix directly
        // to avoid Euler angle sign ambiguity.
        var forward = Vector3.Normalize(followTarget - Entity.Transform.Position);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        var up = Vector3.Cross(forward, right);

        // Stride cameras look along +Z in local space, so forward = +Z column
        var rotMatrix = new Matrix(
            right.X, right.Y, right.Z, 0,
            up.X, up.Y, up.Z, 0,
            forward.X, forward.Y, forward.Z, 0,
            0, 0, 0, 1
        );
        Quaternion.RotationMatrix(ref rotMatrix, out var rotation);
        Entity.Transform.Rotation = rotation;

        // Set perspective projection with narrow FOV for near-isometric look
        var cameraComponent = Entity.Get<CameraComponent>();
        if (cameraComponent != null)
        {
            cameraComponent.Projection = CameraProjectionMode.Perspective;
            cameraComponent.VerticalFieldOfView = CurrentFov;
        }
    }
}
