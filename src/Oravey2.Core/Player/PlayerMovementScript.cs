using Oravey2.Core.Camera;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Input;
using Oravey2.Core.NPC;
using Oravey2.Core.World;
using Oravey2.Core.World.Terrain;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Oravey2.Core.Player;

public class PlayerMovementScript : SyncScript
{
    public float MoveSpeed { get; set; } = 5f;

    /// <summary>
    /// Collision radius matching the player capsule visual (0.3).
    /// </summary>
    public float CollisionRadius { get; set; } = 0.3f;

    /// <summary>
    /// Reference to the camera script to read current yaw for movement direction.
    /// </summary>
    public TacticalCameraScript? CameraScript { get; set; }

    /// <summary>
    /// Tile map used for walkability checks. If null, no collision is enforced.
    /// </summary>
    public TileMapData? MapData { get; set; }

    /// <summary>
    /// Tile size matching the renderer. Defaults to 1.0.
    /// </summary>
    public float TileSize { get; set; } = 1f;

    /// <summary>
    /// Game state manager for input freeze during GameOver.
    /// </summary>
    public GameStateManager? StateManager { get; set; }

    /// <summary>
    /// Terrain height query for Y snapping. If null, player stays at current Y.
    /// </summary>
    public TerrainHeightQuery? HeightQuery { get; set; }

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
        if (StateManager?.CurrentState == GameState.GameOver) return;

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

        // Axis-separated tile collision: check bounding box corners for wall sliding
        if (MapData != null)
        {
            var r = CollisionRadius;

            if (!IsWalkableBBox(MapData, newPos.X, oldPosition.Z, r, TileSize))
                newPos.X = oldPosition.X;

            if (!IsWalkableBBox(MapData, newPos.X, newPos.Z, r, TileSize))
                newPos.Z = oldPosition.Z;
        }

        // NPC circle-circle collision: reject movement that overlaps any NPC
        newPos = ResolveNpcCollision(newPos, oldPosition);

        // Cliff blocking: reject XZ if height delta is a cliff
        if (HeightQuery != null)
        {
            if (HeightQuery.IsCliffBlocking(oldPosition.X, oldPosition.Z, newPos.X, newPos.Z))
            {
                newPos.X = oldPosition.X;
                newPos.Z = oldPosition.Z;
            }

            // Deep liquid blocking
            if (HeightQuery.IsDeepLiquid(newPos.X, newPos.Z))
            {
                newPos.X = oldPosition.X;
                newPos.Z = oldPosition.Z;
            }
        }

        // Terrain height snapping (smooth lerp)
        if (HeightQuery != null)
        {
            float targetY = HeightQuery.GetEffectiveHeight(newPos.X, newPos.Z)
                + TerrainHeightQuery.PlayerHeightOffset;
            float currentY = oldPosition.Y;
            float heightDelta = MathF.Abs(targetY - currentY);

            if (heightDelta > TerrainHeightQuery.SnapThreshold)
            {
                // Large height change (teleport, spawn) — snap immediately
                newPos.Y = targetY;
            }
            else
            {
                // Smooth lerp toward target height
                var dt2 = (float)Game.UpdateTime.Elapsed.TotalSeconds;
                newPos.Y = MathUtil.Lerp(currentY, targetY,
                    1f - MathF.Exp(-TerrainHeightQuery.HeightSmoothing * dt2));
            }
        }

        Entity.Transform.Position = newPos;

        _eventBus?.Publish(new PlayerMovedEvent(oldPosition, Entity.Transform.Position));
    }

    /// <summary>
    /// Returns true only if all four corners of the player's bounding box are on walkable tiles.
    /// </summary>
    private static bool IsWalkableBBox(TileMapData map, float x, float z, float radius, float tileSize)
    {
        return map.IsWalkableAtWorld(x - radius, z - radius, tileSize)
            && map.IsWalkableAtWorld(x + radius, z - radius, tileSize)
            && map.IsWalkableAtWorld(x - radius, z + radius, tileSize)
            && map.IsWalkableAtWorld(x + radius, z + radius, tileSize);
    }

    /// <summary>
    /// Pushes the player out of any overlapping NPC using axis-separated resolution.
    /// </summary>
    private Vector3 ResolveNpcCollision(Vector3 newPos, Vector3 oldPos)
    {
        if (Entity.Scene == null) return newPos;

        foreach (var entity in Entity.Scene.Entities)
        {
            var npc = entity.Get<NpcComponent>();
            if (npc == null) continue;

            var npcPos = entity.Transform.Position;
            var minDist = CollisionRadius + npc.CollisionRadius;

            // Check X axis
            var dx = newPos.X - npcPos.X;
            var dz = oldPos.Z - npcPos.Z;
            if (dx * dx + dz * dz < minDist * minDist)
                newPos.X = oldPos.X;

            // Check Z axis  
            dx = newPos.X - npcPos.X;
            dz = newPos.Z - npcPos.Z;
            if (dx * dx + dz * dz < minDist * minDist)
                newPos.Z = oldPos.Z;
        }

        return newPos;
    }
}
