using Brinell.Stride.Communication;
using Brinell.Stride.Interfaces;
using Oravey2.Core.Automation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.UITests;

/// <summary>
/// Client-side projection of enemy HP bar data (derived from CombatStateResponse).
/// </summary>
public record EnemyHpBarDto(string EnemyId, int Hp, int MaxHp);

/// <summary>
/// Client-side projection of enemy HP bars visibility and data.
/// </summary>
public record EnemyHpBarsResponse(bool Visible, List<EnemyHpBarDto> Bars);

/// <summary>
/// Helpers for querying Oravey2 game state through the automation pipe.
/// </summary>
public static class GameQueryHelpers
{
    // --- Phase A: Position / Camera / Screen / Tile / Diagnostics (typed contracts) ---

    public static PositionResponse GetPlayerPosition(IStrideTestContext context)
        => SendQuery<PositionResponse>("GetPlayerPosition", context);

    public static CameraStateResponse GetCameraState(IStrideTestContext context)
        => SendQuery<CameraStateResponse>("GetCameraState", context);

    public static string GetGameState(IStrideTestContext context)
        => GetHudState(context).GameState;

    public static SceneDiagnosticsResponse GetSceneDiagnostics(IStrideTestContext context)
        => SendQuery<SceneDiagnosticsResponse>("GetSceneDiagnostics", context);

    public static string TakeScreenshot(IStrideTestContext context)
    {
        var result = SendQuery<ScreenshotResponse>("TakeScreenshot", context);
        return result.Path;
    }

    public static ScreenPositionResponse WorldToScreen(IStrideTestContext context, double x, double y, double z)
        => SendQuery<ScreenPositionResponse>("WorldToScreen", context, new WorldToScreenRequest(x, y, z));

    public static PlayerScreenPositionResponse GetPlayerScreenPosition(IStrideTestContext context)
        => SendQuery<PlayerScreenPositionResponse>("GetPlayerScreenPosition", context);

    public static TileInfoResponse GetTileAtWorldPos(IStrideTestContext context, double worldX, double worldZ)
        => SendQuery<TileInfoResponse>("GetTileAtWorldPos", context, new TileAtWorldPosRequest(worldX, worldZ));

    public static PositionResponse GetEntityPosition(IStrideTestContext context, string entityName)
        => SendQuery<PositionResponse>("GetEntityPosition", context, new EntityPositionRequest(entityName));

    // --- Combat helpers (typed contracts) ---

    public static CombatStateResponse GetCombatState(IStrideTestContext context)
        => SendQuery<CombatStateResponse>("GetCombatState", context);

    public static PositionResponse TeleportPlayer(IStrideTestContext context, double x, double y, double z)
        => SendQuery<PositionResponse>("TeleportPlayer", context, new TeleportPlayerRequest(x, y, z));

    public static KillEnemyResponse KillEnemy(IStrideTestContext context, string enemyId)
        => SendQuery<KillEnemyResponse>("KillEnemy", context, new KillEnemyRequest(enemyId));

    // --- Phase B: Inventory / Loot / HUD helpers (typed contracts) ---

    public static InventoryStateResponse GetInventoryState(IStrideTestContext context)
        => SendQuery<InventoryStateResponse>("GetInventoryState", context);

    public static EquipmentStateResponse GetEquipmentState(IStrideTestContext context)
        => SendQuery<EquipmentStateResponse>("GetEquipmentState", context);

    public static HudStateResponse GetHudState(IStrideTestContext context)
        => SendQuery<HudStateResponse>("GetHudState", context);

    public static LootEntitiesResponse GetLootEntities(IStrideTestContext context)
        => SendQuery<LootEntitiesResponse>("GetLootEntities", context);

    public static bool GetInventoryOverlayVisible(IStrideTestContext context)
    {
        var result = SendQuery<InventoryOverlayResponse>("GetInventoryOverlayVisible", context);
        return result.Visible;
    }

    // --- Phase C: Notification / Game Over / Enemy Bars helpers (typed contracts) ---

    public static NotificationFeedResponse GetNotificationFeed(IStrideTestContext context)
        => SendQuery<NotificationFeedResponse>("GetNotificationFeed", context);

    public static GameOverStateResponse GetGameOverState(IStrideTestContext context)
        => SendQuery<GameOverStateResponse>("GetGameOverState", context);

    public static EnemyHpBarsResponse GetEnemyHpBars(IStrideTestContext context)
    {
        var combat = GetCombatState(context);
        var bars = combat.Enemies
            .Where(e => e.IsAlive)
            .Select(e => new EnemyHpBarDto(e.Id, e.Hp, e.MaxHp))
            .ToList();
        return new EnemyHpBarsResponse(combat.InCombat, bars);
    }

    public static DamagePlayerResponse DamagePlayer(IStrideTestContext context, int amount)
        => SendQuery<DamagePlayerResponse>("DamagePlayer", context, new DamagePlayerRequest(amount));

    // --- Phase D: Combat config & equip helpers (typed contracts) ---

    public static CombatConfigResponse GetCombatConfig(IStrideTestContext context)
        => SendQuery<CombatConfigResponse>("GetCombatConfig", context);

    public static EquipItemResponse EquipItem(IStrideTestContext context, string itemId)
        => SendQuery<EquipItemResponse>("EquipItem", context, new EquipItemRequest { ItemId = itemId });

    // --- Phase E: Scenario helpers (typed contracts) ---

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static TResponse SendQuery<TResponse>(
        string method, IStrideTestContext context, object? request = null)
    {
        var args = request != null
            ? new object[] { JsonSerializer.Serialize(request, _jsonOpts) }
            : Array.Empty<object>();

        var response = context.SendCommand(AutomationCommand.GameQuery(method, args));
        if (!response.Success)
            throw new InvalidOperationException($"{method} failed: {response.Error}");

        var json = ((JsonElement)response.Result!).GetRawText();
        return JsonSerializer.Deserialize<TResponse>(json, _jsonOpts)
            ?? throw new InvalidOperationException($"{method} returned null");
    }

    public static ScenarioResetResponse ResetScenario(IStrideTestContext context)
        => SendQuery<ScenarioResetResponse>("ResetScenario", context);

    public static SpawnEnemyResponse SpawnEnemy(
        IStrideTestContext context,
        string id, double x, double z,
        int? hp = null, int endurance = 1, int luck = 3,
        int weaponDamage = 4, float weaponAccuracy = 0.50f)
        => SendQuery<SpawnEnemyResponse>("SpawnEnemy", context, new SpawnEnemyRequest
        {
            Id = id, X = x, Z = z, Hp = hp,
            Endurance = endurance, Luck = luck,
            WeaponDamage = weaponDamage, WeaponAccuracy = weaponAccuracy,
        });

    public static SetStatsResponse SetPlayerStats(
        IStrideTestContext context,
        int? endurance = null, int? luck = null,
        int? strength = null, int? hp = null)
        => SendQuery<SetStatsResponse>("SetPlayerStats", context, new SetPlayerStatsRequest
        {
            Endurance = endurance, Luck = luck, Strength = strength, Hp = hp,
        });

    public static SetWeaponResponse SetPlayerWeapon(
        IStrideTestContext context,
        int damage, float accuracy,
        float range = 2f, int apCost = 3, float critMultiplier = 1.5f)
        => SendQuery<SetWeaponResponse>("SetPlayerWeapon", context, new SetPlayerWeaponRequest
        {
            Damage = damage, Accuracy = accuracy,
            Range = range, ApCost = apCost, CritMultiplier = critMultiplier,
        });

    // --- M1 Phase 1: Menu / Save / Load helpers (typed contracts) ---

    public static MenuStateResponse GetMenuState(IStrideTestContext context, string? screen = null)
        => screen == null
            ? SendQuery<MenuStateResponse>("GetMenuState", context)
            : SendQuery<MenuStateResponse>("GetMenuState", context, new GetMenuStateRequest(screen));

    public static ClickMenuButtonResponse ClickMenuButton(IStrideTestContext context, string screen, string button)
        => SendQuery<ClickMenuButtonResponse>("ClickMenuButton", context, new ClickMenuButtonRequest(screen, button));

    public static TriggerSaveResponse TriggerSave(IStrideTestContext context)
        => SendQuery<TriggerSaveResponse>("TriggerSave", context);

    public static TriggerLoadResponse TriggerLoad(IStrideTestContext context)
        => SendQuery<TriggerLoadResponse>("TriggerLoad", context);

    public static SaveExistsResponse GetSaveExists(IStrideTestContext context)
        => SendQuery<SaveExistsResponse>("GetSaveExists", context);

    public static CapsStateResponse GetCapsState(IStrideTestContext context)
        => SendQuery<CapsStateResponse>("GetCapsState", context);

    // --- M1 Phase 2: NPC helpers (typed contracts) ---

    public static NpcListResponse GetNpcList(IStrideTestContext context)
        => SendQuery<NpcListResponse>("GetNpcList", context);

    public static NpcInRangeResponse GetNpcInRange(IStrideTestContext context)
        => SendQuery<NpcInRangeResponse>("GetNpcInRange", context);

    public static InteractResponse InteractWithNpc(IStrideTestContext context, string npcId)
        => SendQuery<InteractResponse>("InteractWithNpc", context, new InteractWithNpcRequest(npcId));

    public static DialogueStateResponse GetDialogueState(IStrideTestContext context)
        => SendQuery<DialogueStateResponse>("GetDialogueState", context);

    public static DialogueChoiceResponse SelectDialogueChoice(IStrideTestContext context, int index)
        => SendQuery<DialogueChoiceResponse>("SelectDialogueChoice", context, new SelectDialogueChoiceRequest(index));

    public static GiveItemToPlayerResponse GiveItemToPlayer(IStrideTestContext context, string itemId, int count = 1)
        => SendQuery<GiveItemToPlayerResponse>("GiveItemToPlayer", context, new GiveItemToPlayerRequest(itemId, count));

    // --- M1 Phase 2.6: Zone helpers ---

    public static CurrentZoneResponse GetCurrentZone(IStrideTestContext context)
        => SendQuery<CurrentZoneResponse>("GetCurrentZone", context);

    // --- M1 Phase 3.3: Quest & World State helpers ---

    public static ActiveQuestsResponse GetActiveQuests(IStrideTestContext context)
        => SendQuery<ActiveQuestsResponse>("GetActiveQuests", context);

    public static WorldFlagResponse GetWorldFlag(IStrideTestContext context, string flag)
        => SendQuery<WorldFlagResponse>("GetWorldFlag", context, new GetWorldFlagRequest(flag));

    public static SetWorldFlagResponse SetWorldFlag(IStrideTestContext context, string flag, bool value)
        => SendQuery<SetWorldFlagResponse>("SetWorldFlag", context, new SetWorldFlagRequest(flag, value));

    public static WorldCounterResponse GetWorldCounter(IStrideTestContext context, string counter)
        => SendQuery<WorldCounterResponse>("GetWorldCounter", context, new GetWorldCounterRequest(counter));

    public static SetWorldCounterResponse SetWorldCounter(IStrideTestContext context, string counter, int value)
        => SendQuery<SetWorldCounterResponse>("SetWorldCounter", context, new SetWorldCounterRequest(counter, value));

    // --- M1 Phase 3.4: Quest Tracker & Journal helpers ---

    public static QuestTrackerStateResponse GetQuestTrackerState(IStrideTestContext context)
        => SendQuery<QuestTrackerStateResponse>("GetQuestTrackerState", context);

    public static QuestJournalStateResponse GetQuestJournalState(IStrideTestContext context)
        => SendQuery<QuestJournalStateResponse>("GetQuestJournalState", context);

    // --- M1 Phase 4: Death & Respawn helpers ---

    public static DeathStateResponse GetDeathState(IStrideTestContext context)
        => SendQuery<DeathStateResponse>("GetDeathState", context);

    public static ForcePlayerDeathResponse ForcePlayerDeath(IStrideTestContext context)
        => SendQuery<ForcePlayerDeathResponse>("ForcePlayerDeath", context);

    public static VictoryStateResponse GetVictoryState(IStrideTestContext context)
        => SendQuery<VictoryStateResponse>("GetVictoryState", context);

    // --- Step 15: Location Description helpers ---

    public static InfoPanelStateResponse GetInfoPanelState(IStrideTestContext context)
        => SendQuery<InfoPanelStateResponse>("GetInfoPanelState", context);

    public static InfoPanelStateResponse ShowInfoPanel(IStrideTestContext context,
        int locationId, string name, string type, string tagline,
        string? summary = null, string? dossier = null)
        => SendQuery<InfoPanelStateResponse>("ShowInfoPanel", context,
            new ShowInfoPanelRequest(locationId, name, type, tagline, summary, dossier));

    public static ExpandInfoPanelResponse ExpandInfoPanel(IStrideTestContext context, string tier)
        => SendQuery<ExpandInfoPanelResponse>("ExpandInfoPanel", context, new ExpandInfoPanelRequest(tier));

    public static InfoPanelStateResponse CloseInfoPanel(IStrideTestContext context)
        => SendQuery<InfoPanelStateResponse>("CloseInfoPanel", context);
}
