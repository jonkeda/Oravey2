using Brinell.Automation;
using Brinell.Automation.Communication;
using Oravey2.Core.Camera;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Loot;
using Oravey2.Core.UI;
using Oravey2.Core.UI.Stride;
using Oravey2.Core.World;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Games;
using System.Text.Json;

namespace Oravey2.Windows;

/// <summary>
/// Custom automation handler that wraps StrideUIHandler and adds
/// Oravey2-specific game state queries (player position, camera state, etc.)
/// so UI tests can make behavioral assertions.
/// </summary>
public sealed class OraveyAutomationHandler : IAutomationHandler
{
    private readonly IAutomationHandler _inner;
    private readonly Scene _rootScene;
    private readonly Game _game;

    // Phase B references
    private InventoryComponent? _playerInventory;
    private EquipmentComponent? _playerEquipment;
    private HealthComponent? _playerHealth;
    private CombatComponent? _playerCombat;
    private LevelComponent? _playerLevel;
    private GameStateManager? _gameStateManager;

    // Phase C references
    private NotificationService? _notificationService;
    private GameOverOverlayScript? _gameOverOverlay;

    public OraveyAutomationHandler(IAutomationHandler inner, Scene rootScene, Game game)
    {
        _inner = inner;
        _rootScene = rootScene;
        _game = game;
    }

    /// <summary>
    /// Wires Phase B component references for inventory/HUD automation queries.
    /// </summary>
    public void SetPhaseB(
        InventoryComponent playerInventory,
        EquipmentComponent playerEquipment,
        HealthComponent playerHealth,
        CombatComponent playerCombat,
        LevelComponent playerLevel,
        GameStateManager gameStateManager)
    {
        _playerInventory = playerInventory;
        _playerEquipment = playerEquipment;
        _playerHealth = playerHealth;
        _playerCombat = playerCombat;
        _playerLevel = playerLevel;
        _gameStateManager = gameStateManager;
    }

    /// <summary>
    /// Wires Phase C component references for notification/game-over automation queries.
    /// </summary>
    public void SetPhaseC(
        NotificationService notificationService,
        GameOverOverlayScript gameOverOverlay)
    {
        _notificationService = notificationService;
        _gameOverOverlay = gameOverOverlay;
    }

    public async Task<AutomationResponse> HandleCommandAsync(AutomationCommand command, CancellationToken cancellationToken = default)
    {
        // Handle Oravey2-specific game queries
        if (command.Type is "GameQuery" or "Query")
        {
            if (command.Method == "TakeScreenshot")
                return await TakeScreenshotAsync();

            var response = HandleGameQuery(command);
            if (response != null)
                return response;
        }

        // Fall through to the default Stride UI handler
        return await _inner.HandleCommandAsync(command, cancellationToken);
    }

    private AutomationResponse? HandleGameQuery(AutomationCommand command)
    {
        return command.Method switch
        {
            "GetPlayerPosition" => GetPlayerPosition(),
            "GetCameraState" => GetCameraState(),
            "GetGameState" => GetCurrentGameState(),
            "GetSceneDiagnostics" => GetSceneDiagnostics(),
            "GetEntityPosition" => GetEntityPosition(command),
            "WorldToScreen" => WorldToScreen(command),
            "GetTileAtWorldPos" => GetTileAtWorldPos(command),
            "GetPlayerScreenPosition" => GetPlayerScreenPosition(),
            "GetCombatState" => GetCombatState(),
            "TeleportPlayer" => TeleportPlayer(command),
            "KillEnemy" => KillEnemy(command),
            "GetInventoryState" => GetInventoryState(),
            "GetEquipmentState" => GetEquipmentState(),
            "GetHudState" => GetHudState(),
            "GetLootEntities" => GetLootEntities(),
            "GetInventoryOverlayVisible" => GetInventoryOverlayVisible(),
            "GetNotificationFeed" => GetNotificationFeed(),
            "GetGameOverState" => GetGameOverState(),
            "GetEnemyHpBars" => GetEnemyHpBars(),
            "DamagePlayer" => DamagePlayer(command),
            _ => null // Let inner handler deal with it
        };
    }

    private AutomationResponse GetPlayerPosition()
    {
        var playerEntity = FindEntity("Player");
        if (playerEntity == null)
            return AutomationResponse.Fail("Player entity not found");

        var pos = playerEntity.Transform.Position;
        var result = new { x = pos.X, y = pos.Y, z = pos.Z };
        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(result));
    }

    private AutomationResponse GetCameraState()
    {
        var cameraEntity = FindEntity("IsometricCamera");
        if (cameraEntity == null)
            return AutomationResponse.Fail("Camera entity not found");

        var cameraScript = cameraEntity.Get<IsometricCameraScript>();
        var pos = cameraEntity.Transform.Position;

        var result = new
        {
            x = pos.X,
            y = pos.Y,
            z = pos.Z,
            yaw = cameraScript?.Yaw ?? 0f,
            pitch = cameraScript?.Pitch ?? 0f,
            zoom = cameraScript?.CurrentFov ?? 0f
        };
        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(result));
    }

    private AutomationResponse GetCurrentGameState()
    {
        if (ServiceLocator.Instance.TryGet<GameStateManager>(out var gsm) && gsm != null)
            return AutomationResponse.Ok(gsm.CurrentState.ToString());

        return AutomationResponse.Fail("GameStateManager not registered");
    }

    private AutomationResponse GetSceneDiagnostics()
    {
        var entities = _rootScene.Entities;
        var modelEntities = new List<object>();
        foreach (var e in entities)
        {
            var mc = e.Get<ModelComponent>();
            if (mc != null)
            {
                var pos = e.Transform.Position;
                modelEntities.Add(new
                {
                    name = e.Name,
                    x = pos.X, y = pos.Y, z = pos.Z,
                    meshCount = mc.Model?.Meshes?.Count ?? 0,
                    materialCount = mc.Model?.Materials?.Count ?? 0
                });
            }
            // Also check children for models
            foreach (var child in e.GetChildren())
            {
                var childMc = child.Get<ModelComponent>();
                if (childMc != null)
                {
                    var cpos = child.Transform.Position;
                    modelEntities.Add(new
                    {
                        name = $"{e.Name}/{child.Name}",
                        x = cpos.X, y = cpos.Y, z = cpos.Z,
                        meshCount = childMc.Model?.Meshes?.Count ?? 0,
                        materialCount = childMc.Model?.Materials?.Count ?? 0
                    });
                }
            }
        }

        // Camera diagnostics
        var cameraEntity = FindEntity("IsometricCamera");
        object? cameraDiag = null;
        if (cameraEntity != null)
        {
            var cc = cameraEntity.Get<CameraComponent>();
            var camPos = cameraEntity.Transform.Position;
            var camRot = cameraEntity.Transform.Rotation;
            var forward = camRot * Vector3.UnitZ;

            cameraDiag = new
            {
                position = new { x = camPos.X, y = camPos.Y, z = camPos.Z },
                forward = new { x = forward.X, y = forward.Y, z = forward.Z },
                rotation = new { x = camRot.X, y = camRot.Y, z = camRot.Z, w = camRot.W },
                projection = cc?.Projection.ToString() ?? "null",
                orthoSize = cc?.OrthographicSize ?? 0f,
                nearClip = cc?.NearClipPlane ?? 0f,
                farClip = cc?.FarClipPlane ?? 0f,
                slotId = cc?.Slot.Id.ToString() ?? "null"
            };
        }

        // Compositor diagnostics
        var result = new
        {
            totalEntities = entities.Count,
            modelEntityCount = modelEntities.Count,
            modelEntitiesSample = modelEntities.Take(5),
            camera = cameraDiag
        };
        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(result));
    }

    private async Task<AutomationResponse> TakeScreenshotAsync()
    {
        var tcs = new TaskCompletionSource<string>();
        var path = Path.Combine(Path.GetTempPath(), $"oravey_screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");

        _game.Script.Scheduler.Add(async () =>
        {
            try
            {
                // Wait two frames to ensure rendering has completed
                await _game.Script.NextFrame();
                await _game.Script.NextFrame();

                _game.TakeScreenShot(path);
                tcs.SetResult(path);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        try
        {
            var resultPath = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new { path = resultPath }));
        }
        catch (Exception ex)
        {
            return AutomationResponse.Fail($"Screenshot failed: {ex.Message}");
        }
    }

    private AutomationResponse GetEntityPosition(AutomationCommand command)
    {
        var name = command.Args?.FirstOrDefault()?.ToString();
        if (string.IsNullOrEmpty(name))
            return AutomationResponse.Fail("Entity name required as first argument");

        var entity = FindEntity(name);
        if (entity == null)
            return AutomationResponse.Fail($"Entity '{name}' not found");

        var pos = entity.Transform.WorldMatrix.TranslationVector;
        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(
            new { x = pos.X, y = pos.Y, z = pos.Z }));
    }

    private AutomationResponse WorldToScreen(AutomationCommand command)
    {
        if (command.Args == null || command.Args.Length < 3)
            return AutomationResponse.Fail("WorldToScreen requires x, y, z arguments");

        float x = Convert.ToSingle(command.Args[0]?.ToString());
        float y = Convert.ToSingle(command.Args[1]?.ToString());
        float z = Convert.ToSingle(command.Args[2]?.ToString());

        var cameraEntity = FindEntity("IsometricCamera");
        if (cameraEntity == null)
            return AutomationResponse.Fail("Camera not found");

        var cc = cameraEntity.Get<CameraComponent>();
        if (cc == null)
            return AutomationResponse.Fail("CameraComponent not found");

        // Build view-projection matrix from the camera script's live properties.
        // We can't use Transform.Position/Rotation because they may be stale
        // (set during the last Update() tick, but a key press may have changed
        // Yaw/Pitch since then without the camera transform being recomputed).
        var camScript = cameraEntity.Get<IsometricCameraScript>();
        var (viewProj, backBuffer) = BuildCameraViewProj(cameraEntity, cc, camScript);

        // Project world point
        var worldPos = new Vector3(x, y, z);
        var clipPos = Vector3.TransformCoordinate(worldPos, viewProj);

        // Clip space [-1,1] to screen space [0, width/height]
        float screenX = (clipPos.X * 0.5f + 0.5f) * backBuffer.Width;
        float screenY = (1f - (clipPos.Y * 0.5f + 0.5f)) * backBuffer.Height;

        // Normalized [0,1] for resolution independence
        float normX = screenX / backBuffer.Width;
        float normY = screenY / backBuffer.Height;

        bool onScreen = normX >= 0f && normX <= 1f && normY >= 0f && normY <= 1f;

        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
        {
            screenX, screenY, normX, normY, onScreen,
            screenWidth = backBuffer.Width,
            screenHeight = backBuffer.Height
        }));
    }

    private AutomationResponse GetTileAtWorldPos(AutomationCommand command)
    {
        if (command.Args == null || command.Args.Length < 2)
            return AutomationResponse.Fail("GetTileAtWorldPos requires worldX, worldZ arguments");

        float worldX = Convert.ToSingle(command.Args[0]?.ToString());
        float worldZ = Convert.ToSingle(command.Args[1]?.ToString());

        var tileMapEntity = FindEntity("TileMap");
        if (tileMapEntity == null)
            return AutomationResponse.Fail("TileMap entity not found");

        var renderer = tileMapEntity.Get<TileMapRendererScript>();
        if (renderer?.MapData == null)
            return AutomationResponse.Fail("TileMapRendererScript or MapData not found");

        var map = renderer.MapData;
        // Reverse the world→tile coordinate mapping from TileMapRendererScript
        int tileX = (int)MathF.Floor(worldX / renderer.TileSize + map.Width / 2f);
        int tileZ = (int)MathF.Floor(worldZ / renderer.TileSize + map.Height / 2f);

        var tileType = map.GetTile(tileX, tileZ);

        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
        {
            tileX, tileZ,
            tileType = tileType.ToString(),
            tileTypeId = (int)tileType
        }));
    }

    private AutomationResponse GetPlayerScreenPosition()
    {
        var playerEntity = FindEntity("Player");
        if (playerEntity == null)
            return AutomationResponse.Fail("Player entity not found");

        var cameraEntity = FindEntity("IsometricCamera");
        if (cameraEntity == null)
            return AutomationResponse.Fail("Camera not found");

        var cc = cameraEntity.Get<CameraComponent>();
        if (cc == null)
            return AutomationResponse.Fail("CameraComponent not found");

        var playerPos = playerEntity.Transform.Position;

        var camScript = cameraEntity.Get<IsometricCameraScript>();
        var (viewProj, backBuffer) = BuildCameraViewProj(cameraEntity, cc, camScript);
        var clipPos = Vector3.TransformCoordinate(playerPos, viewProj);

        float screenX = (clipPos.X * 0.5f + 0.5f) * backBuffer.Width;
        float screenY = (1f - (clipPos.Y * 0.5f + 0.5f)) * backBuffer.Height;
        float normX = screenX / backBuffer.Width;
        float normY = screenY / backBuffer.Height;
        bool onScreen = normX >= 0f && normX <= 1f && normY >= 0f && normY <= 1f;

        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
        {
            worldX = playerPos.X, worldY = playerPos.Y, worldZ = playerPos.Z,
            screenX, screenY, normX, normY, onScreen,
            screenWidth = backBuffer.Width, screenHeight = backBuffer.Height
        }));
    }

    /// <summary>
    /// Build the view-projection matrix from the camera script's live Yaw/Pitch/Distance/Target
    /// properties, mirroring IsometricCameraScript.UpdateCameraTransform(). This ensures we get
    /// the current view even if the script's Update() hasn't run yet this frame.
    /// </summary>
    private (Matrix ViewProj, Stride.Graphics.Texture BackBuffer) BuildCameraViewProj(
        Entity cameraEntity, CameraComponent cc, IsometricCameraScript? camScript)
    {
        Vector3 camPos;
        Quaternion camRot;

        if (camScript?.Target != null)
        {
            // Recompute camera position/rotation from live script properties
            var targetPos = camScript.Target.Transform.Position;
            var pitchRad = MathUtil.DegreesToRadians(camScript.Pitch);
            var yawRad = MathUtil.DegreesToRadians(camScript.Yaw);

            var offset = new Vector3(
                MathF.Cos(pitchRad) * MathF.Sin(yawRad) * camScript.Distance,
                MathF.Sin(pitchRad) * camScript.Distance,
                MathF.Cos(pitchRad) * MathF.Cos(yawRad) * camScript.Distance);

            camPos = targetPos + offset;

            // Use RotationYawPitchRoll matching IsometricCameraScript (see RCA-004)
            camRot = Quaternion.RotationYawPitchRoll(
                yawRad, MathUtil.DegreesToRadians(-camScript.Pitch), 0f);

            // Update camera component to match live script properties
            cc.Projection = CameraProjectionMode.Perspective;
            cc.VerticalFieldOfView = camScript.CurrentFov;
        }
        else
        {
            // Fallback to transform values
            camPos = cameraEntity.Transform.Position;
            camRot = cameraEntity.Transform.Rotation;
        }

        Matrix.RotationQuaternion(ref camRot, out var rotMatrix);
        var worldMatrix = rotMatrix;
        worldMatrix.TranslationVector = camPos;
        Matrix.Invert(ref worldMatrix, out var viewMatrix);

        cc.Update();
        var backBuffer = _game.GraphicsDevice.Presenter.BackBuffer;
        var projMatrix = cc.ProjectionMatrix;
        var viewProj = viewMatrix * projMatrix;

        return (viewProj, backBuffer);
    }

    private Entity? FindEntity(string name)
    {
        foreach (var entity in _rootScene.Entities)
        {
            if (entity.Name == name)
                return entity;
        }
        return null;
    }

    private AutomationResponse GetCombatState()
    {
        var combatManager = FindEntity("CombatManager");
        if (combatManager == null)
            return AutomationResponse.Fail("CombatManager entity not found");

        var script = combatManager.Get<CombatSyncScript>();
        if (script == null)
            return AutomationResponse.Fail("CombatSyncScript not found");

        var enemies = script.Enemies.Select(e => new
        {
            id = e.Id,
            hp = e.Health.CurrentHP,
            maxHp = e.Health.MaxHP,
            ap = (int)e.Combat.CurrentAP,
            maxAp = e.Combat.MaxAP,
            isAlive = e.Health.IsAlive,
            x = e.Entity.Transform.Position.X,
            y = e.Entity.Transform.Position.Y,
            z = e.Entity.Transform.Position.Z,
        });

        var result = new
        {
            inCombat = script.CombatState?.InCombat ?? false,
            enemyCount = script.Enemies.Count,
            enemies,
            playerHp = script.PlayerHealth?.CurrentHP ?? 0,
            playerMaxHp = script.PlayerHealth?.MaxHP ?? 0,
            playerAp = (int)(script.PlayerCombat?.CurrentAP ?? 0),
            playerMaxAp = script.PlayerCombat?.MaxAP ?? 0,
        };
        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(result));
    }

    private AutomationResponse TeleportPlayer(AutomationCommand command)
    {
        if (command.Args == null || command.Args.Length < 3)
            return AutomationResponse.Fail("TeleportPlayer requires x, y, z arguments");

        float x = Convert.ToSingle(command.Args[0]?.ToString());
        float y = Convert.ToSingle(command.Args[1]?.ToString());
        float z = Convert.ToSingle(command.Args[2]?.ToString());

        var player = FindEntity("Player");
        if (player == null)
            return AutomationResponse.Fail("Player entity not found");

        player.Transform.Position = new Vector3(x, y, z);
        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(
            new { x, y, z }));
    }

    private AutomationResponse KillEnemy(AutomationCommand command)
    {
        var enemyId = command.Args?.FirstOrDefault()?.ToString();
        if (string.IsNullOrEmpty(enemyId))
            return AutomationResponse.Fail("Enemy ID required as first argument");

        var combatManager = FindEntity("CombatManager");
        var script = combatManager?.Get<CombatSyncScript>();
        if (script == null)
            return AutomationResponse.Fail("CombatSyncScript not found");

        var enemy = script.Enemies.FirstOrDefault(e => e.Id == enemyId);
        if (enemy == null)
            return AutomationResponse.Fail($"Enemy '{enemyId}' not found");

        enemy.Health.TakeDamage(enemy.Health.CurrentHP);
        var remaining = script.Enemies.Count(e => e.Health.IsAlive);

        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(
            new { killed = true, remainingAlive = remaining }));
    }

    // ---- Phase B queries ----

    private AutomationResponse GetInventoryState()
    {
        if (_playerInventory == null)
            return AutomationResponse.Fail("Player inventory not initialized");

        var items = _playerInventory.Items.Select(i => new
        {
            id = i.Definition.Id,
            name = i.Definition.Name,
            category = i.Definition.Category.ToString(),
            count = i.StackCount,
            weight = i.TotalWeight,
        });

        var result = new
        {
            itemCount = _playerInventory.Items.Count,
            currentWeight = _playerInventory.CurrentWeight,
            maxWeight = _playerInventory.MaxCarryWeight,
            isOverweight = _playerInventory.IsOverweight,
            items,
        };
        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(result));
    }

    private AutomationResponse GetEquipmentState()
    {
        if (_playerEquipment == null)
            return AutomationResponse.Fail("Player equipment not initialized");

        var slots = new Dictionary<string, object?>();
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            var item = _playerEquipment.GetEquipped(slot);
            slots[slot.ToString()] = item != null
                ? new { id = item.Definition.Id, name = item.Definition.Name }
                : null;
        }

        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new { slots }));
    }

    private AutomationResponse GetHudState()
    {
        var result = new
        {
            hp = _playerHealth?.CurrentHP ?? 0,
            maxHp = _playerHealth?.MaxHP ?? 0,
            ap = (int)(_playerCombat?.CurrentAP ?? 0),
            maxAp = _playerCombat?.MaxAP ?? 0,
            level = _playerLevel?.Level ?? 0,
            gameState = _gameStateManager?.CurrentState.ToString() ?? "Unknown",
        };
        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(result));
    }

    private AutomationResponse GetLootEntities()
    {
        var lootEntities = _rootScene.Entities
            .Where(e => LootDropScript.HasLoot(e))
            .Select(e =>
            {
                LootDropScript.TryGetLootItems(e, out var items);
                return new
                {
                    name = e.Name,
                    x = e.Transform.Position.X,
                    y = e.Transform.Position.Y,
                    z = e.Transform.Position.Z,
                    itemCount = items?.Count ?? 0,
                };
            })
            .ToList();

        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new
        {
            count = lootEntities.Count,
            entities = lootEntities,
        }));
    }

    private AutomationResponse GetInventoryOverlayVisible()
    {
        var overlayEntity = FindEntity("InventoryOverlay");
        var script = overlayEntity?.Get<InventoryOverlayScript>();
        if (script == null)
            return AutomationResponse.Fail("InventoryOverlay entity not found");

        var visible = script.IsVisible;
        return AutomationResponse.Ok(JsonSerializer.SerializeToElement(new { visible }));
    }

    // ---- Phase C queries ----

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
}
