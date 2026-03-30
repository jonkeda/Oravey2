using Oravey2.Core.Camera;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Input;
using Oravey2.Core.World;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Oravey2.Core.Player;

public class PlayerMovementScript : SyncScript
{
    public float MoveSpeed { get; set; } = 5f;

    /// <summary>
    /// Reference to the camera script to read current yaw for movement direction.
    /// </summary>
    public IsometricCameraScript? CameraScript { get; set; }

    /// <summary>
    /// Tile map used for walkability checks. If null, no collision is enforced.
    /// </summary>
    public TileMapData? MapData { get; set; }

    /// <summary>
    /// Tile size matching the renderer. Defaults to 1.0.
    /// </summary>
    public float TileSize { get; set; } = 1f;

    private float CameraYaw => CameraScript?.Yaw ?? 45f;

    private IInputProvider? _inputProvider;
    private IEventBus? _eventBus;

    public override void Start()
    {
        if (ServiceLocator.Instance.TryGet<IInputProvider>(out var inputProvider))
            _inputProvider = inputProvider;

        if (ServiceLocator.Instance.TryGet<IEventBus>(out var eventBus))
            _eventBus = eventBus;
    }

    public override void Update()
    {
        if (_inputProvider == null) return;

        var movement = _inputProvider.MovementAxis;
        if (movement.LengthSquared() < 0.001f)
            return;

        var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;

        var yawRad = MathUtil.DegreesToRadians(CameraYaw);
        var cosYaw = MathF.Cos(yawRad);
        var sinYaw = MathF.Sin(yawRad);

        var worldX = movement.X * cosYaw - movement.Y * sinYaw;
        var worldZ = -movement.X * sinYaw - movement.Y * cosYaw;

        var worldDir = new Vector3(worldX, 0f, worldZ);
        if (worldDir.LengthSquared() > 0f)
            worldDir.Normalize();

        var oldPosition = Entity.Transform.Position;
        var delta = worldDir * MoveSpeed * dt;
        var newPos = oldPosition + delta;

        // Axis-separated tile collision: check X and Z independently for wall sliding
        if (MapData != null)
        {
            if (!MapData.IsWalkableAtWorld(newPos.X, oldPosition.Z, TileSize))
                newPos.X = oldPosition.X;

            if (!MapData.IsWalkableAtWorld(newPos.X, newPos.Z, TileSize))
                newPos.Z = oldPosition.Z;
        }

        Entity.Transform.Position = newPos;

        _eventBus?.Publish(new PlayerMovedEvent(oldPosition, Entity.Transform.Position));
    }
}
