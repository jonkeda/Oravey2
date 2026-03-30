using Brinell.Automation;
using Brinell.Automation.Communication;
using Oravey2.Core.Automation;
using Oravey2.Core.Camera;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.Events;
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
using Stride.Extensions;
using Stride.Games;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            "GetCombatConfig" => GetCombatConfig(),
            "EquipItem" => EquipItem(command),
            "ResetScenario" => ResetScenario(),
            "SpawnEnemy" => SpawnEnemy(command),
            "SetPlayerStats" => SetPlayerStats(command),
            "SetPlayerWeapon" => SetPlayerWeapon(command),
            _ => null // Let inner handler deal with it
        };
    }

    private AutomationResponse GetPlayerPosition()
    {
        var playerEntity = FindEntity("Player");
        if (playerEntity == null)
            return AutomationResponse.Fail("Player entity not found");

        var pos = playerEntity.Transform.Position;
        return Respond(new PositionResponse(pos.X, pos.Y, pos.Z));
    }

    private AutomationResponse GetCameraState()
    {
        var cameraEntity = FindEntity("IsometricCamera");
        if (cameraEntity == null)
            return AutomationResponse.Fail("Camera entity not found");

        var cameraScript = cameraEntity.Get<TacticalCameraScript>();
        var pos = cameraEntity.Transform.Position;

        return Respond(new CameraStateResponse(
            pos.X, pos.Y, pos.Z,
            cameraScript?.Yaw ?? 0f,
            cameraScript?.Pitch ?? 0f,
            cameraScript?.CurrentFov ?? 0f));
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
        var modelEntities = new List<ModelEntityDto>();
        foreach (var e in entities)
        {
            var mc = e.Get<ModelComponent>();
            if (mc != null)
            {
                var pos = e.Transform.Position;
                modelEntities.Add(new ModelEntityDto(
                    e.Name, pos.X, pos.Y, pos.Z,
                    mc.Model?.Meshes?.Count ?? 0,
                    mc.Model?.Materials?.Count ?? 0));
            }
            // Also check children for models
            foreach (var child in e.GetChildren())
            {
                var childMc = child.Get<ModelComponent>();
                if (childMc != null)
                {
                    var cpos = child.Transform.Position;
                    modelEntities.Add(new ModelEntityDto(
                        $"{e.Name}/{child.Name}", cpos.X, cpos.Y, cpos.Z,
                        childMc.Model?.Meshes?.Count ?? 0,
                        childMc.Model?.Materials?.Count ?? 0));
                }
            }
        }

        // Camera diagnostics
        var cameraEntity = FindEntity("IsometricCamera");
        CameraDiagnosticsDto? camDiag = null;
        if (cameraEntity != null)
        {
            var cc = cameraEntity.Get<CameraComponent>();
            var camPos = cameraEntity.Transform.Position;
            var camRot = cameraEntity.Transform.Rotation;
            var forward = camRot * Vector3.UnitZ;

            camDiag = new CameraDiagnosticsDto(
                new PositionResponse(camPos.X, camPos.Y, camPos.Z),
                new PositionResponse(forward.X, forward.Y, forward.Z),
                cc?.Projection.ToString() ?? "null",
                cc?.OrthographicSize ?? 0f,
                cc?.NearClipPlane ?? 0f,
                cc?.FarClipPlane ?? 0f,
                cc?.Slot.Id.ToString() ?? "null");
        }

        return Respond(new SceneDiagnosticsResponse(
            entities.Count,
            modelEntities.Count,
            modelEntities.Take(5).ToList(),
            camDiag));
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
            return Respond(new ScreenshotResponse(resultPath));
        }
        catch (Exception ex)
        {
            return AutomationResponse.Fail($"Screenshot failed: {ex.Message}");
        }
    }

    private AutomationResponse GetEntityPosition(AutomationCommand command)
    {
        var req = DeserializeArg<EntityPositionRequest>(command);
        if (req == null || string.IsNullOrEmpty(req.EntityName))
            return AutomationResponse.Fail("Entity name required");

        var entity = FindEntity(req.EntityName);
        if (entity == null)
            return AutomationResponse.Fail($"Entity '{req.EntityName}' not found");

        var pos = entity.Transform.WorldMatrix.TranslationVector;
        return Respond(new PositionResponse(pos.X, pos.Y, pos.Z));
    }

    private AutomationResponse WorldToScreen(AutomationCommand command)
    {
        var req = DeserializeArg<WorldToScreenRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("WorldToScreen requires x, y, z arguments");

        float x = (float)req.X;
        float y = (float)req.Y;
        float z = (float)req.Z;

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
        var camScript = cameraEntity.Get<TacticalCameraScript>();
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

        return Respond(new ScreenPositionResponse(
            screenX, screenY, normX, normY, onScreen,
            backBuffer.Width, backBuffer.Height));
    }

    private AutomationResponse GetTileAtWorldPos(AutomationCommand command)
    {
        var req = DeserializeArg<TileAtWorldPosRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("GetTileAtWorldPos requires worldX, worldZ arguments");

        float worldX = (float)req.WorldX;
        float worldZ = (float)req.WorldZ;

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

        return Respond(new TileInfoResponse(
            tileX, tileZ, tileType.ToString(), (int)tileType));
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

        var camScript = cameraEntity.Get<TacticalCameraScript>();
        var (viewProj, backBuffer) = BuildCameraViewProj(cameraEntity, cc, camScript);
        var clipPos = Vector3.TransformCoordinate(playerPos, viewProj);

        float screenX = (clipPos.X * 0.5f + 0.5f) * backBuffer.Width;
        float screenY = (1f - (clipPos.Y * 0.5f + 0.5f)) * backBuffer.Height;
        float normX = screenX / backBuffer.Width;
        float normY = screenY / backBuffer.Height;
        bool onScreen = normX >= 0f && normX <= 1f && normY >= 0f && normY <= 1f;

        return Respond(new PlayerScreenPositionResponse(
            playerPos.X, playerPos.Y, playerPos.Z,
            screenX, screenY, normX, normY, onScreen,
            backBuffer.Width, backBuffer.Height));
    }

    /// <summary>
    /// Build the view-projection matrix from the camera script's live Yaw/Pitch/Distance/Target
    /// properties, mirroring TacticalCameraScript.UpdateCameraTransform(). This ensures we get
    /// the current view even if the script's Update() hasn't run yet this frame.
    /// </summary>
    private (Matrix ViewProj, Stride.Graphics.Texture BackBuffer) BuildCameraViewProj(
        Entity cameraEntity, CameraComponent cc, TacticalCameraScript? camScript)
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

            // Use RotationYawPitchRoll matching TacticalCameraScript (see RCA-004)
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

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static T? DeserializeArg<T>(AutomationCommand command) where T : class
    {
        var json = command.Args?.FirstOrDefault()?.ToString();
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json, _jsonOpts);
    }

    private static AutomationResponse Respond<T>(T result)
        => AutomationResponse.Ok(JsonSerializer.SerializeToElement(result, _jsonOpts));

    private AutomationResponse GetCombatState()
    {
        var combatManager = FindEntity("CombatManager");
        if (combatManager == null)
            return AutomationResponse.Fail("CombatManager entity not found");

        var script = combatManager.Get<CombatSyncScript>();
        if (script == null)
            return AutomationResponse.Fail("CombatSyncScript not found");

        var enemies = script.Enemies.Select(e => new EnemyStateDto(
            e.Id, e.Health.CurrentHP, e.Health.MaxHP,
            (int)e.Combat.CurrentAP, e.Combat.MaxAP,
            e.Health.IsAlive,
            e.Entity.Transform.Position.X,
            e.Entity.Transform.Position.Y,
            e.Entity.Transform.Position.Z)).ToList();

        return Respond(new CombatStateResponse(
            script.CombatState?.InCombat ?? false,
            script.Enemies.Count,
            enemies,
            script.PlayerHealth?.CurrentHP ?? 0,
            script.PlayerHealth?.MaxHP ?? 0,
            (int)(script.PlayerCombat?.CurrentAP ?? 0),
            script.PlayerCombat?.MaxAP ?? 0));
    }

    private AutomationResponse TeleportPlayer(AutomationCommand command)
    {
        var req = DeserializeArg<TeleportPlayerRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("TeleportPlayer requires x, y, z arguments");

        float x = (float)req.X;
        float y = (float)req.Y;
        float z = (float)req.Z;

        var player = FindEntity("Player");
        if (player == null)
            return AutomationResponse.Fail("Player entity not found");

        player.Transform.Position = new Vector3(x, y, z);
        return Respond(new PositionResponse(x, y, z));
    }

    private AutomationResponse KillEnemy(AutomationCommand command)
    {
        var req = DeserializeArg<KillEnemyRequest>(command);
        var enemyId = req?.EnemyId;
        if (string.IsNullOrEmpty(enemyId))
            return AutomationResponse.Fail("Enemy ID required");

        var combatManager = FindEntity("CombatManager");
        var script = combatManager?.Get<CombatSyncScript>();
        if (script == null)
            return AutomationResponse.Fail("CombatSyncScript not found");

        var enemy = script.Enemies.FirstOrDefault(e => e.Id == enemyId);
        if (enemy == null)
            return AutomationResponse.Fail($"Enemy '{enemyId}' not found");

        enemy.Health.TakeDamage(enemy.Health.CurrentHP);
        var remaining = script.Enemies.Count(e => e.Health.IsAlive);

        return Respond(new KillEnemyResponse(true, remaining));
    }

    // ---- Phase B queries ----

    private AutomationResponse GetInventoryState()
    {
        if (_playerInventory == null)
            return AutomationResponse.Fail("Player inventory not initialized");

        var items = _playerInventory.Items.Select(i => new InventoryItemDto(
            i.Definition.Id, i.Definition.Name,
            i.Definition.Category.ToString(),
            i.StackCount, i.TotalWeight)).ToList();

        return Respond(new InventoryStateResponse(
            _playerInventory.Items.Count,
            _playerInventory.CurrentWeight,
            _playerInventory.MaxCarryWeight,
            _playerInventory.IsOverweight,
            items));
    }

    private AutomationResponse GetEquipmentState()
    {
        if (_playerEquipment == null)
            return AutomationResponse.Fail("Player equipment not initialized");

        var slots = new Dictionary<string, EquipmentSlotDto?>();
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            var item = _playerEquipment.GetEquipped(slot);
            slots[slot.ToString()] = item != null
                ? new EquipmentSlotDto(item.Definition.Id, item.Definition.Name)
                : null;
        }

        return Respond(new EquipmentStateResponse(slots));
    }

    private AutomationResponse GetHudState()
    {
        return Respond(new HudStateResponse(
            _playerHealth?.CurrentHP ?? 0,
            _playerHealth?.MaxHP ?? 0,
            (int)(_playerCombat?.CurrentAP ?? 0),
            _playerCombat?.MaxAP ?? 0,
            _playerLevel?.Level ?? 0,
            _gameStateManager?.CurrentState.ToString() ?? "Unknown"));
    }

    private AutomationResponse GetLootEntities()
    {
        var lootEntities = _rootScene.Entities
            .Where(e => LootDropScript.HasLoot(e))
            .Select(e =>
            {
                LootDropScript.TryGetLootItems(e, out var items);
                return new LootEntityDto(
                    e.Name,
                    e.Transform.Position.X,
                    e.Transform.Position.Y,
                    e.Transform.Position.Z,
                    items?.Count ?? 0);
            })
            .ToList();

        return Respond(new LootEntitiesResponse(lootEntities.Count, lootEntities));
    }

    private AutomationResponse GetInventoryOverlayVisible()
    {
        var overlayEntity = FindEntity("InventoryOverlay");
        var script = overlayEntity?.Get<InventoryOverlayScript>();
        if (script == null)
            return AutomationResponse.Fail("InventoryOverlay entity not found");

        return Respond(new InventoryOverlayResponse(script.IsVisible));
    }

    // ---- Phase C queries ----

    private AutomationResponse GetNotificationFeed()
    {
        if (_notificationService == null)
            return AutomationResponse.Fail("NotificationService not initialized");

        var active = _notificationService.GetActive();
        var messages = active.Select(n => new NotificationMessageDto(
            n.Message, n.TimeRemaining)).ToList();

        return Respond(new NotificationFeedResponse(active.Count, messages));
    }

    private AutomationResponse GetGameOverState()
    {
        if (_gameOverOverlay == null)
            return AutomationResponse.Fail("GameOverOverlay not initialized");

        return Respond(new GameOverStateResponse(
            _gameOverOverlay.IsVisible,
            _gameOverOverlay.CurrentTitle ?? ""));
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
            .Select(e => new EnemyHpBarDto(e.Id, e.Health.CurrentHP, e.Health.MaxHP))
            .ToList();

        return Respond(new EnemyHpBarsResponse(inCombat, bars));
    }

    private AutomationResponse DamagePlayer(AutomationCommand command)
    {
        if (_playerHealth == null)
            return AutomationResponse.Fail("Player health not initialized");

        var req = DeserializeArg<DamagePlayerRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("DamagePlayer requires amount argument");

        int amount = req.Amount;
        _playerHealth.TakeDamage(amount);

        // Trigger GameOver if player died (normally done inside CombatSyncScript.Update)
        if (!_playerHealth.IsAlive && _gameStateManager != null
            && _gameStateManager.CurrentState != GameState.GameOver)
        {
            _gameStateManager.ForceState(GameState.GameOver);
        }

        return Respond(new DamagePlayerResponse(
            _playerHealth.CurrentHP, _playerHealth.MaxHP, _playerHealth.IsAlive));
    }

    private AutomationResponse GetCombatConfig()
    {
        var combatManager = FindEntity("CombatManager");
        var script = combatManager?.Get<CombatSyncScript>();
        if (script == null)
            return AutomationResponse.Fail("CombatSyncScript not found");

        // Read player's equipped weapon
        var playerWeapon = script.PlayerEquipment
            ?.GetEquipped(EquipmentSlot.PrimaryWeapon)
            ?.Definition.Weapon;

        // Read first enemy's weapon (all M0 enemies share the same weapon)
        WeaponData? enemyWeapon = null;
        if (script.Enemies.Count > 0)
            enemyWeapon = script.Enemies[0].Weapon;

        return Respond(new CombatConfigResponse(
            new WeaponConfigDto(
                playerWeapon?.Damage ?? 5,
                playerWeapon?.Accuracy ?? 0.50f,
                playerWeapon?.Range ?? 1.5f,
                playerWeapon?.CritMultiplier ?? 1.5f,
                playerWeapon?.ApCost ?? 3),
            new WeaponConfigDto(
                enemyWeapon?.Damage ?? 5,
                enemyWeapon?.Accuracy ?? 0.50f,
                enemyWeapon?.Range ?? 1.5f,
                enemyWeapon?.CritMultiplier ?? 1.5f,
                enemyWeapon?.ApCost ?? 3),
            0f));
    }

    private AutomationResponse EquipItem(AutomationCommand command)
    {
        var req = DeserializeArg<EquipItemRequest>(command);
        var itemId = req?.ItemId;
        if (_playerInventory == null || _playerEquipment == null)
            return AutomationResponse.Fail("Inventory not initialized");

        // Find the item in inventory
        var item = _playerInventory.Items
            .FirstOrDefault(i => i.Definition.Id == itemId);

        if (item == null)
        {
            // Item not in inventory — create and add it
            var definition = itemId switch
            {
                "leather_jacket" => M0Items.LeatherJacket(),
                "pipe_wrench" => M0Items.PipeWrench(),
                "rusty_shiv" => M0Items.RustyShiv(),
                _ => null,
            };

            if (definition == null)
                return AutomationResponse.Fail($"Unknown item: {itemId}");

            item = new ItemInstance(definition);
            _playerInventory.Add(item);
        }

        if (item.Definition.Slot == null)
            return AutomationResponse.Fail($"Item '{itemId}' has no equipment slot");

        _playerEquipment.Equip(item, item.Definition.Slot.Value);

        return Respond(new EquipItemResponse(true, item.Definition.Slot.Value.ToString(), item.Definition.Name));
    }

    // ---- Phase E: Scenario commands ----

    private MaterialInstance? _cachedEnemyMaterial;

    private AutomationResponse ResetScenario()
    {
        try
        {
            var combatManager = FindEntity("CombatManager");
            var combatScript = combatManager?.Get<CombatSyncScript>();
            if (combatScript == null)
                return AutomationResponse.Fail("CombatSyncScript not found");

            // Force back to Exploring first to stop script Update from running combat logic
            if (_gameStateManager != null && _gameStateManager.CurrentState != GameState.Exploring)
                _gameStateManager.ForceState(GameState.Exploring);

            // Remove all enemy entities from scene (combatScript.Enemies is shared with triggerScript)
            foreach (var enemy in combatScript.Enemies.ToList())
            {
                enemy.Entity.Scene?.Entities.Remove(enemy.Entity);
                enemy.Combat.InCombat = false;
            }
            combatScript.Enemies.Clear();

            // Exit combat if active
            if (combatScript.CombatState?.InCombat == true)
                combatScript.CombatState.ExitCombat();

            // Reset player combat state
            if (combatScript.PlayerCombat != null)
            {
                combatScript.PlayerCombat.InCombat = false;
                combatScript.PlayerCombat.ResetAP();
            }
            combatScript.Queue?.Clear();

            _playerHealth?.HealToMax();

            // Teleport player to origin
            var player = FindEntity("Player");
            if (player != null)
                player.Transform.Position = new Vector3(0, 0.5f, 0);

            // Remove loot cubes (best-effort, collection may be modified concurrently)
            try
            {
                var lootEntities = _rootScene.Entities
                    .Where(e => e != null && LootDropScript.HasLoot(e))
                    .ToList();
                foreach (var loot in lootEntities)
                    _rootScene.Entities.Remove(loot);
            }
            catch { /* Loot cleanup is best-effort */ }

            return Respond(new ScenarioResetResponse(true, _playerHealth?.CurrentHP ?? 0, 0));
        }
        catch (Exception ex)
        {
            return AutomationResponse.Fail($"ResetScenario error: {ex.Message}");
        }
    }

    private AutomationResponse SpawnEnemy(AutomationCommand command)
    {
        var req = DeserializeArg<SpawnEnemyRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("SpawnEnemy requires JSON config argument");

        var id = req.Id ?? $"enemy_{Guid.NewGuid():N}";
        var x = (float)req.X;
        var z = (float)req.Z;
        var endurance = req.Endurance ?? 1;
        var luck = req.Luck ?? 3;
        var weaponDamage = req.WeaponDamage ?? 4;
        var weaponAccuracy = req.WeaponAccuracy ?? 0.50f;

        // Create enemy entity with visual
        var enemyEntity = new Entity(id);
        enemyEntity.Transform.Position = new Vector3(x, 0.5f, z);

        var enemyVisual = new Entity($"{id}_Visual");
        var mesh = GeometricPrimitive.Capsule.New(_game.GraphicsDevice, 0.3f, 0.8f).ToMeshDraw();
        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = mesh });
        _cachedEnemyMaterial ??= _game.CreateMaterial(new Color(0.8f, 0.15f, 0.15f));
        model.Materials.Add(_cachedEnemyMaterial);
        enemyVisual.Add(new ModelComponent(model));
        enemyEntity.AddChild(enemyVisual);

        _rootScene.Entities.Add(enemyEntity);

        // Stats
        var stats = new StatsComponent(new Dictionary<Stat, int>
        {
            { Stat.Strength, 3 }, { Stat.Perception, 3 }, { Stat.Endurance, endurance },
            { Stat.Charisma, 2 }, { Stat.Intelligence, 2 }, { Stat.Agility, 4 },
            { Stat.Luck, luck },
        });
        var level = new LevelComponent(stats);

        // Resolve IEventBus from ServiceLocator
        ServiceLocator.Instance.TryGet<IEventBus>(out var eventBus);
        var health = new HealthComponent(stats, level, eventBus);
        var combat = new CombatComponent { InCombat = false };

        // Override HP if specified
        if (req.Hp.HasValue && req.Hp.Value < health.MaxHP)
            health.TakeDamage(health.MaxHP - req.Hp.Value);

        // Weapon
        var weapon = new WeaponData(
            Damage: weaponDamage, Range: 1.5f, ApCost: 3,
            Accuracy: weaponAccuracy, SkillType: "melee", CritMultiplier: 1.5f);

        var enemyInfo = new EnemyInfo
        {
            Entity = enemyEntity,
            Id = id,
            Health = health,
            Combat = combat,
            Stats = stats,
            Weapon = weapon,
        };

        // Add to combat + trigger systems
        var combatManager = FindEntity("CombatManager");
        var combatScript = combatManager?.Get<CombatSyncScript>();
        var triggerScript = combatManager?.Get<EncounterTriggerScript>();

        // Add to shared enemy list (combatScript.Enemies and triggerScript.Enemies are the same reference)
        combatScript?.Enemies.Add(enemyInfo);

        return Respond(new SpawnEnemyResponse(true, id, health.CurrentHP, health.MaxHP));
    }

    private AutomationResponse SetPlayerStats(AutomationCommand command)
    {
        var req = DeserializeArg<SetPlayerStatsRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("SetPlayerStats requires JSON config argument");

        var combatManager = FindEntity("CombatManager");
        var script = combatManager?.Get<CombatSyncScript>();
        if (script?.PlayerStats == null || _playerHealth == null)
            return AutomationResponse.Fail("Player stats not initialized");

        if (req.Endurance.HasValue) script.PlayerStats.SetBase(Stat.Endurance, req.Endurance.Value);
        if (req.Luck.HasValue) script.PlayerStats.SetBase(Stat.Luck, req.Luck.Value);
        if (req.Strength.HasValue) script.PlayerStats.SetBase(Stat.Strength, req.Strength.Value);

        _playerHealth.HealToMax();

        if (req.Hp.HasValue && req.Hp.Value < _playerHealth.MaxHP)
            _playerHealth.TakeDamage(_playerHealth.MaxHP - req.Hp.Value);

        return Respond(new SetStatsResponse(true, _playerHealth.CurrentHP, _playerHealth.MaxHP));
    }

    private AutomationResponse SetPlayerWeapon(AutomationCommand command)
    {
        var req = DeserializeArg<SetPlayerWeaponRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("SetPlayerWeapon requires JSON config argument");

        if (_playerEquipment == null || _playerInventory == null)
            return AutomationResponse.Fail("Equipment not initialized");

        var weaponData = new WeaponData(req.Damage, req.Range, req.ApCost, req.Accuracy, "melee", CritMultiplier: req.CritMultiplier);
        var definition = new ItemDefinition(
            Id: "test_weapon",
            Name: "Test Weapon",
            Description: "Custom test weapon",
            Category: ItemCategory.WeaponMelee,
            Weight: 1f,
            Stackable: false,
            Value: 0,
            Slot: EquipmentSlot.PrimaryWeapon,
            Weapon: weaponData);

        var item = new ItemInstance(definition);
        _playerInventory.Add(item);
        _playerEquipment.Equip(item, EquipmentSlot.PrimaryWeapon);

        return Respond(new SetWeaponResponse(true, req.Damage, req.Accuracy));
    }
}
