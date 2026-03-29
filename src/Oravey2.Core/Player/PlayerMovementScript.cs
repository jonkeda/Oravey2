using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Input;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Oravey2.Core.Player;

public class PlayerMovementScript : SyncScript
{
    public float MoveSpeed { get; set; } = 5f;

    /// <summary>
    /// Reference to the camera script to read current yaw for movement direction.
    /// </summary>
    public float CameraYaw { get; set; } = 45f;

    private IInputProvider? _inputProvider;
    private IEventBus? _eventBus;

    public override void Start()
    {
        if (ServiceLocator.Instance.TryGet<IInputProvider>(out var inputProvider))
            _inputProvider = inputProvider;

        if (ServiceLocator.Instance.TryGet<IEventBus>(out var eventBus))
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<CameraRotatedEvent>(OnCameraRotated);
        }
    }

    public override void Update()
    {
        if (_inputProvider == null) return;

        var movement = _inputProvider.MovementAxis;
        if (movement.LengthSquared() < 0.001f)
            return;

        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;

        // Convert screen-aligned input to isometric world-space movement
        var yawRad = MathUtil.DegreesToRadians(CameraYaw);
        var cosYaw = MathF.Cos(yawRad);
        var sinYaw = MathF.Sin(yawRad);

        var worldX = movement.X * cosYaw - movement.Y * sinYaw;
        var worldZ = movement.X * sinYaw + movement.Y * cosYaw;

        var worldDir = new Vector3(worldX, 0f, worldZ);
        if (worldDir.LengthSquared() > 0f)
            worldDir.Normalize();

        var oldPosition = Entity.Transform.Position;
        Entity.Transform.Position += worldDir * MoveSpeed * dt;

        _eventBus?.Publish(new PlayerMovedEvent(oldPosition, Entity.Transform.Position));
    }

    public override void Cancel()
    {
        _eventBus?.Unsubscribe<CameraRotatedEvent>(OnCameraRotated);
    }

    private void OnCameraRotated(CameraRotatedEvent e)
    {
        CameraYaw = e.NewYaw;
    }
}
