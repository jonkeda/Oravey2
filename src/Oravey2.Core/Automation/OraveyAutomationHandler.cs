using Brinell.Automation;
using Brinell.Automation.Communication;
using Oravey2.Core.Bootstrap;
using Oravey2.Core.Camera;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
using Oravey2.Core.Dialogue;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.Services;
using Oravey2.Core.Framework.State;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Loot;
using Oravey2.Core.NPC;
using Oravey2.Core.Quests;
using Oravey2.Core.Save;
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

namespace Oravey2.Core.Automation;

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

    // M1 Phase 1 references
    private SaveService? _saveService;
    private AutoSaveTracker? _autoSaveTracker;
    private StartMenuScript? _startMenu;
    private PauseMenuScript? _pauseMenu;
    private SettingsMenuScript? _settingsMenu;
    private ScenarioLoader? _scenarioLoader;
    private ZoneManager? _zoneManager;

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

    /// <summary>
    /// Wires M1 Phase 1 references for menu/save automation queries.
    /// </summary>
    public void SetM1(
        SaveService saveService,
        AutoSaveTracker autoSaveTracker,
        StartMenuScript startMenu,
        PauseMenuScript pauseMenu,
        SettingsMenuScript settingsMenu)
    {
        _saveService = saveService;
        _autoSaveTracker = autoSaveTracker;
        _startMenu = startMenu;
        _pauseMenu = pauseMenu;
        _settingsMenu = settingsMenu;
    }

    /// <summary>
    /// Wires the scenario loader so the handler can read game refs dynamically.
    /// </summary>
    public void SetScenarioLoader(ScenarioLoader scenarioLoader)
    {
        _scenarioLoader = scenarioLoader;
    }

    public void SetZoneManager(ZoneManager zoneManager)
    {
        _zoneManager = zoneManager;
    }

    /// <summary>
    /// Refreshes Phase B/C refs from the current scenario loader state.
    /// Call after a scenario is loaded to ensure the handler uses the latest refs.
    /// </summary>
    private void RefreshFromScenarioLoader()
    {
        if (_scenarioLoader == null) return;
        _playerInventory = _scenarioLoader.PlayerInventory;
        _playerEquipment = _scenarioLoader.PlayerEquipment;
        _playerHealth = _scenarioLoader.PlayerHealth;
        _playerCombat = _scenarioLoader.PlayerCombat;
        _playerLevel = _scenarioLoader.PlayerLevel;
        _notificationService = _scenarioLoader.NotificationService;
        _gameOverOverlay = _scenarioLoader.GameOverOverlay;
    }

    public async Task<AutomationResponse> HandleCommandAsync(AutomationCommand command, CancellationToken cancellationToken = default)
    {
        // Refresh refs from scenario loader in case a scenario was loaded/unloaded
        RefreshFromScenarioLoader();

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
            "DamagePlayer" => DamagePlayer(command),
            "GetCombatConfig" => GetCombatConfig(),
            "EquipItem" => EquipItem(command),
            "ResetScenario" => ResetScenario(),
            "SpawnEnemy" => SpawnEnemy(command),
            "SetPlayerStats" => SetPlayerStats(command),
            "SetPlayerWeapon" => SetPlayerWeapon(command),
            "GetMenuState" => GetMenuState(command),
            "ClickMenuButton" => ClickMenuButton(command),
            "TriggerSave" => TriggerSave(),
            "TriggerLoad" => TriggerLoad(),
            "GetSaveExists" => GetSaveExists(),
            "GetCapsState" => GetCapsState(),
            "GetNpcList" => GetNpcList(),
            "GetNpcInRange" => GetNpcInRange(),
            "InteractWithNpc" => InteractWithNpc(command),
            "GetDialogueState" => GetDialogueState(),
            "SelectDialogueChoice" => SelectDialogueChoice(command),
            "GiveItemToPlayer" => GiveItemToPlayer(command),
            "GetCurrentZone" => GetCurrentZone(),
            "GetActiveQuests" => GetActiveQuests(),
            "GetWorldFlag" => GetWorldFlag(command),
            "SetWorldFlag" => SetWorldFlag(command),
            "GetWorldCounter" => GetWorldCounter(command),
            "SetWorldCounter" => SetWorldCounter(command),
            "GetQuestTrackerState" => GetQuestTrackerState(),
            "GetQuestJournalState" => GetQuestJournalState(),
            "GetDeathState" => GetDeathState(),
            "ForcePlayerDeath" => ForcePlayerDeath(),
            "GetVictoryState" => GetVictoryState(),
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

    private AutomationResponse GetSceneDiagnostics()
    {
        var entities = WorldEntities;
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
            WorldEntities.Count(),
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
        // Search world scene first (game entities), then root scene (camera, infrastructure)
        var worldScene = _scenarioLoader?.WorldScene;
        if (worldScene != null)
        {
            foreach (var entity in worldScene.Entities)
                if (entity.Name == name) return entity;
        }
        foreach (var entity in _rootScene.Entities)
            if (entity.Name == name) return entity;
        return null;
    }

    /// <summary>
    /// Returns the entities in the active world scene, or rootScene if no world scene is loaded.
    /// </summary>
    private IEnumerable<Entity> WorldEntities
        => (IEnumerable<Entity>?)_scenarioLoader?.WorldScene?.Entities ?? _rootScene.Entities;

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
            enemies));
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
        var lootEntities = WorldEntities
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
            _gameOverOverlay.CurrentTitle ?? "",
            _gameOverOverlay.CurrentSubtitle ?? ""));
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

    private EnemySpawner? _enemySpawner;

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
                var lootEntities = WorldEntities
                    .Where(e => e != null && LootDropScript.HasLoot(e))
                    .ToList();
                foreach (var loot in lootEntities)
                    loot.Scene?.Entities.Remove(loot);
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
        var endurance = req.Endurance ?? 1;
        var luck = req.Luck ?? 3;
        var weaponDamage = req.WeaponDamage ?? 4;
        var weaponAccuracy = req.WeaponAccuracy ?? 0.50f;

        ServiceLocator.Instance.TryGet<IEventBus>(out var eventBus);
        _enemySpawner ??= new EnemySpawner((Game)_game, eventBus!);

        var enemyInfo = _enemySpawner.Spawn(
            _scenarioLoader?.WorldScene ?? _rootScene, id, (float)req.X, (float)req.Z,
            endurance, luck, weaponDamage, weaponAccuracy,
            overrideHp: req.Hp);

        // Add to combat + trigger systems
        var combatManager = FindEntity("CombatManager");
        var combatScript = combatManager?.Get<CombatSyncScript>();

        combatScript?.Enemies.Add(enemyInfo);

        return Respond(new SpawnEnemyResponse(true, id, enemyInfo.Health.CurrentHP, enemyInfo.Health.MaxHP));
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

    // ---- M1 Phase 1: Menu / Save / Load commands ----

    private AutomationResponse GetNpcList()
    {
        var npcs = WorldEntities
            .Select(e => (Entity: e, Npc: e.Get<NpcComponent>()))
            .Where(x => x.Npc?.Definition != null)
            .Select(x =>
            {
                var pos = x.Entity.Transform.Position;
                var def = x.Npc!.Definition!;
                return new NpcDto(def.Id, def.DisplayName, def.Role.ToString(), pos.X, pos.Y, pos.Z);
            })
            .ToList();

        return Respond(new NpcListResponse(npcs.Count, npcs));
    }

    private AutomationResponse GetNpcInRange()
    {
        var playerEntity = FindEntity("Player");
        if (playerEntity == null)
            return AutomationResponse.Fail("Player entity not found");

        var playerPos = playerEntity.Transform.Position;
        InteractionTriggerScript? closest = null;
        Entity? closestEntity = null;
        float closestDist = float.MaxValue;

        foreach (var entity in WorldEntities)
        {
            var trigger = entity.Get<InteractionTriggerScript>();
            if (trigger?.NpcDef == null) continue;

            var dist = (playerPos - entity.Transform.Position).Length();
            if (dist <= trigger.InteractionRadius && dist < closestDist)
            {
                closest = trigger;
                closestEntity = entity;
                closestDist = dist;
            }
        }

        if (closest == null)
            return Respond(new NpcInRangeResponse(false, null, null, -1));

        return Respond(new NpcInRangeResponse(true, closest.NpcDef!.Id, closest.NpcDef.DisplayName, closestDist));
    }

    private AutomationResponse InteractWithNpc(AutomationCommand command)
    {
        var req = DeserializeArg<InteractWithNpcRequest>(command);
        var npcId = req?.NpcId;

        foreach (var entity in WorldEntities)
        {
            var npc = entity.Get<NpcComponent>();
            if (npc?.Definition?.Id != npcId) continue;

            var trigger = entity.Get<InteractionTriggerScript>();
            if (trigger?.EventBus == null || trigger.NpcDef == null)
                return AutomationResponse.Fail($"NPC '{npcId}' has no interaction trigger");

            trigger.EventBus.Publish(new NpcInteractionEvent(trigger.NpcDef.Id, trigger.NpcDef.DialogueTreeId));
            return Respond(new InteractResponse(true, trigger.NpcDef.Id, trigger.NpcDef.DialogueTreeId));
        }

        return AutomationResponse.Fail($"NPC '{npcId}' not found");
    }

    private AutomationResponse GetDialogueState()
    {
        var proc = _scenarioLoader?.DialogueProcessor;
        if (proc == null || !proc.IsActive)
            return Respond(new DialogueStateResponse(false, null, null, null, null, []));

        var ctx = _scenarioLoader!.DialogueContext;
        var node = proc.CurrentNode;
        var choices = ctx != null
            ? proc.GetAvailableChoices(ctx)
                .Select(c => new DialogueChoiceDto(c.Choice.Text, c.Available))
                .ToList()
            : new List<DialogueChoiceDto>();

        return Respond(new DialogueStateResponse(
            true,
            node?.Speaker,
            node?.Text,
            proc.ActiveTree?.Id,
            node?.Id,
            choices));
    }

    private AutomationResponse SelectDialogueChoice(AutomationCommand command)
    {
        var proc = _scenarioLoader?.DialogueProcessor;
        var ctx = _scenarioLoader?.DialogueContext;
        if (proc == null || !proc.IsActive || ctx == null)
            return AutomationResponse.Fail("No active dialogue");

        var req = DeserializeArg<SelectDialogueChoiceRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("SelectDialogueChoice requires index argument");

        var selected = proc.SelectChoice(req.Index, ctx);
        if (!selected)
            return AutomationResponse.Fail($"Choice {req.Index} not available");

        var ended = !proc.IsActive;
        if (ended && _gameStateManager?.CurrentState == GameState.InDialogue)
            _gameStateManager.TransitionTo(GameState.Exploring);

        return Respond(new DialogueChoiceResponse(true, ended));
    }

    private AutomationResponse GetMenuState(AutomationCommand command)
    {
        var req = DeserializeArg<GetMenuStateRequest>(command);
        var screenName = req?.Screen ?? "";

        return screenName switch
        {
            "StartMenu" => Respond(new MenuStateResponse("StartMenu",
                _startMenu?.ButtonLabels ?? [], _startMenu?.IsVisible ?? false)),
            "PauseMenu" => Respond(new MenuStateResponse("PauseMenu",
                _pauseMenu?.ButtonLabels ?? [], _pauseMenu?.IsVisible ?? false)),
            "SettingsMenu" => Respond(new MenuStateResponse("SettingsMenu",
                [], _settingsMenu?.IsVisible ?? false)),
            _ => GetActiveMenuState(),
        };
    }

    private AutomationResponse GetActiveMenuState()
    {
        if (_startMenu?.IsVisible == true)
            return Respond(new MenuStateResponse("StartMenu", _startMenu.ButtonLabels, true));
        if (_pauseMenu?.IsVisible == true)
            return Respond(new MenuStateResponse("PauseMenu", _pauseMenu.ButtonLabels, true));
        if (_settingsMenu?.IsVisible == true)
            return Respond(new MenuStateResponse("SettingsMenu", [], true));

        return Respond(new MenuStateResponse("None", [], false));
    }

    private AutomationResponse ClickMenuButton(AutomationCommand command)
    {
        var req = DeserializeArg<ClickMenuButtonRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("ClickMenuButton requires screen and button");

        switch (req.Screen)
        {
            case "StartMenu":
                switch (req.Button)
                {
                    case "New Game": _startMenu?.OnNewGame?.Invoke(); break;
                    case "Continue": _startMenu?.OnContinue?.Invoke(); break;
                    case "Settings": _startMenu?.OnSettings?.Invoke(); break;
                    default: return AutomationResponse.Fail($"Unknown button: {req.Button}");
                }
                break;
            case "PauseMenu":
                switch (req.Button)
                {
                    case "Resume": _pauseMenu?.Resume(); break;
                    case "Save Game": _pauseMenu?.OnSaveGame?.Invoke(); break;
                    case "Settings": _pauseMenu?.OnSettings?.Invoke(); break;
                    case "Quit to Menu": _pauseMenu?.OnQuitToMenu?.Invoke(); break;
                    default: return AutomationResponse.Fail($"Unknown button: {req.Button}");
                }
                break;
            case "SettingsMenu":
                if (req.Button == "Back") _settingsMenu?.OnBack?.Invoke();
                else return AutomationResponse.Fail($"Unknown button: {req.Button}");
                break;
            default:
                return AutomationResponse.Fail($"Unknown screen: {req.Screen}");
        }

        return Respond(new ClickMenuButtonResponse(true));
    }

    private AutomationResponse TriggerSave()
    {
        if (_saveService == null || _playerHealth == null)
            return AutomationResponse.Fail("SaveService not initialized");

        var playerEntity = FindEntity("Player");
        if (playerEntity == null)
            return AutomationResponse.Fail("Player entity not found");

        var data = new SaveDataBuilder()
            .WithHeader("Survivor", _playerLevel?.Level ?? 1, TimeSpan.Zero, "0.1.0")
            .WithStats(new StatsComponent()) // Use default stats for now
            .WithHealth(_playerHealth)
            .WithLevel(_playerLevel ?? new LevelComponent(new StatsComponent()))
            .WithInventory(_playerInventory!, _playerEquipment!)
            .WithWorldState(new DayNightCycleProcessor(new EventBus()), 0, 0,
                playerEntity.Transform.Position.X,
                playerEntity.Transform.Position.Y,
                playerEntity.Transform.Position.Z)
            .WithQuestStates(
                _scenarioLoader?.QuestLog?.Quests is { } qs ? new Dictionary<string, QuestStatus>(qs) : [],
                _scenarioLoader?.QuestLog?.CurrentStages is { } cs ? new Dictionary<string, string>(cs) : [],
                _scenarioLoader?.WorldState?.Flags is { } wf ? new Dictionary<string, bool>(wf) : [],
                _scenarioLoader?.WorldState?.Counters is { } wc ? new Dictionary<string, int>(wc) : [])
            .Build();

        _saveService.SaveGame(data);
        return Respond(new TriggerSaveResponse(true, _saveService.SavePath));
    }

    private AutomationResponse TriggerLoad()
    {
        if (_saveService == null)
            return AutomationResponse.Fail("SaveService not initialized");

        var restorer = _saveService.LoadGame();
        if (restorer == null)
            return AutomationResponse.Fail("No save file found");

        var playerEntity = FindEntity("Player");
        if (playerEntity == null)
            return AutomationResponse.Fail("Player entity not found");

        var (px, py, pz) = restorer.PlayerPosition;
        playerEntity.Transform.Position = new Vector3(px, py, pz);

        if (_playerHealth != null)
            restorer.RestoreHealth(_playerHealth);

        if (_playerInventory != null)
            _playerInventory.Caps = restorer.Caps;

        // Restore quest and world state
        _scenarioLoader?.QuestLog?.RestoreFromSave(restorer.QuestStates, restorer.QuestStages);
        _scenarioLoader?.WorldState?.RestoreFromSave(restorer.WorldFlags, restorer.WorldCounters);

        return Respond(new TriggerLoadResponse(true));
    }

    private AutomationResponse GetSaveExists()
    {
        return Respond(new SaveExistsResponse(_saveService?.HasSaveFile() ?? false));
    }

    private AutomationResponse GetCapsState()
    {
        return Respond(new CapsStateResponse(_playerInventory?.Caps ?? 0));
    }

    private AutomationResponse GiveItemToPlayer(AutomationCommand command)
    {
        if (_playerInventory == null)
            return AutomationResponse.Fail("Player inventory not initialized");

        var req = DeserializeArg<GiveItemToPlayerRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("Invalid GiveItemToPlayer request");

        var def = ItemResolver.Resolve(req.ItemId);
        _playerInventory.Add(new ItemInstance(def, req.Count));
        return Respond(new GiveItemToPlayerResponse(true));
    }

    private AutomationResponse GetCurrentZone()
    {
        if (_zoneManager == null)
            return AutomationResponse.Fail("ZoneManager not initialized");

        return Respond(new CurrentZoneResponse(
            _zoneManager.CurrentZoneId ?? "unknown",
            _zoneManager.CurrentZoneName));
    }

    // ---- M1 Phase 3.3: Quest & World State ----

    private AutomationResponse GetActiveQuests()
    {
        var questLog = _scenarioLoader?.QuestLog;
        if (questLog == null)
            return AutomationResponse.Fail("QuestLog not initialized");

        var quests = new List<QuestInfoDto>();
        foreach (var (questId, status) in questLog.Quests)
        {
            var def = QuestChainDefinitions.GetQuest(questId);
            var stage = questLog.GetCurrentStage(questId);
            var stageDesc = stage != null && def?.Stages.TryGetValue(stage, out var s) == true
                ? s.Description : null;

            quests.Add(new QuestInfoDto(
                questId,
                def?.Title ?? questId,
                status.ToString(),
                stage,
                stageDesc));
        }

        return Respond(new ActiveQuestsResponse(quests.Count, quests));
    }

    private AutomationResponse GetWorldFlag(AutomationCommand command)
    {
        var worldState = _scenarioLoader?.WorldState;
        if (worldState == null)
            return AutomationResponse.Fail("WorldState not initialized");

        var req = DeserializeArg<GetWorldFlagRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("GetWorldFlag requires flag name");

        return Respond(new WorldFlagResponse(req.Flag, worldState.GetFlag(req.Flag)));
    }

    private AutomationResponse SetWorldFlag(AutomationCommand command)
    {
        var worldState = _scenarioLoader?.WorldState;
        if (worldState == null)
            return AutomationResponse.Fail("WorldState not initialized");

        var req = DeserializeArg<SetWorldFlagRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("SetWorldFlag requires flag and value");

        worldState.SetFlag(req.Flag, req.Value);
        return Respond(new SetWorldFlagResponse(true));
    }

    private AutomationResponse GetWorldCounter(AutomationCommand command)
    {
        var worldState = _scenarioLoader?.WorldState;
        if (worldState == null)
            return AutomationResponse.Fail("WorldState not initialized");

        var req = DeserializeArg<GetWorldCounterRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("GetWorldCounter requires counter name");

        return Respond(new WorldCounterResponse(req.Counter, worldState.GetCounter(req.Counter)));
    }

    private AutomationResponse SetWorldCounter(AutomationCommand command)
    {
        var worldState = _scenarioLoader?.WorldState;
        if (worldState == null)
            return AutomationResponse.Fail("WorldState not initialized");

        var req = DeserializeArg<SetWorldCounterRequest>(command);
        if (req == null)
            return AutomationResponse.Fail("SetWorldCounter requires counter and value");

        worldState.SetCounter(req.Counter, req.Value);
        return Respond(new SetWorldCounterResponse(true));
    }

    // ---- M1 Phase 3.4: Quest Tracker & Journal ----

    private AutomationResponse GetQuestTrackerState()
    {
        var tracker = _scenarioLoader?.QuestTracker;
        if (tracker == null)
            return Respond(new QuestTrackerStateResponse(false, null, null, null, null));

        var questId = tracker.TrackedQuestId;
        string? title = null;
        if (questId != null)
        {
            var def = QuestChainDefinitions.GetQuest(questId);
            title = def?.Title;
        }

        return Respond(new QuestTrackerStateResponse(
            tracker.IsVisible, questId, title, tracker.ObjectiveText, tracker.ProgressText));
    }

    private AutomationResponse GetQuestJournalState()
    {
        var journal = _scenarioLoader?.QuestJournal;
        if (journal == null)
            return Respond(new QuestJournalStateResponse(false, [], []));

        var questLog = _scenarioLoader?.QuestLog;
        var worldState = _scenarioLoader?.WorldState;

        var active = new List<QuestJournalEntryDto>();
        var completed = new List<QuestJournalEntryDto>();

        foreach (var def in QuestChainDefinitions.All)
        {
            var status = questLog?.GetStatus(def.Id) ?? QuestStatus.NotStarted;
            if (status == QuestStatus.Active)
            {
                var stageId = questLog!.GetCurrentStage(def.Id);
                string? objective = null;
                string? progress = null;
                if (stageId != null && def.Stages.TryGetValue(stageId, out var stage))
                {
                    objective = stage.Description;
                    progress = GetCounterProgress(stage, worldState);
                }
                active.Add(new QuestJournalEntryDto(def.Id, def.Title, def.Description, objective, progress, def.XPReward));
            }
            else if (status == QuestStatus.Completed)
            {
                completed.Add(new QuestJournalEntryDto(def.Id, def.Title, def.Description, null, null, def.XPReward));
            }
        }

        return Respond(new QuestJournalStateResponse(journal.IsVisible, active, completed));
    }

    private static string? GetCounterProgress(QuestStage stage, WorldStateService? worldState)
    {
        if (worldState == null) return null;
        foreach (var condition in stage.Conditions)
        {
            if (condition is QuestCounterCondition counter)
                return $"({worldState.GetCounter(counter.CounterName)}/{counter.MinValue})";
        }
        return null;
    }

    // ---- M1 Phase 4: Death & Respawn ----

    private AutomationResponse GetDeathState()
    {
        var deathRespawn = _scenarioLoader?.DeathRespawn;
        if (deathRespawn == null)
            return Respond(new DeathStateResponse(false, 0f, 0));

        return Respond(new DeathStateResponse(
            deathRespawn.IsDead,
            deathRespawn.RespawnTimer,
            deathRespawn.CapsLost));
    }

    private AutomationResponse ForcePlayerDeath()
    {
        if (_playerHealth == null)
            return AutomationResponse.Fail("Player health not initialized");

        if (!_playerHealth.IsAlive)
            return Respond(new ForcePlayerDeathResponse(false));

        _playerHealth.TakeDamage(_playerHealth.CurrentHP);

        if (_gameStateManager != null && _gameStateManager.CurrentState != GameState.GameOver)
            _gameStateManager.ForceState(GameState.GameOver);

        return Respond(new ForcePlayerDeathResponse(true));
    }

    private AutomationResponse GetVictoryState()
    {
        var victoryCheck = _scenarioLoader?.VictoryCheck;
        var achieved = victoryCheck?.VictoryAchieved ?? false;
        string? title = achieved ? "HAVEN IS SAFE" : null;
        return Respond(new VictoryStateResponse(achieved, title));
    }
}
