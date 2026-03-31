using System.Text.Json.Serialization;

namespace Oravey2.Core.Automation;

// ---- Phase A: Position / Camera / Screen / Tile / Diagnostics ----

public record PositionResponse(double X, double Y, double Z);

public record WorldToScreenRequest(double X, double Y, double Z);

public record TileAtWorldPosRequest(double WorldX, double WorldZ);

public record TeleportPlayerRequest(double X, double Y, double Z);

public record EntityPositionRequest(string EntityName);

public record KillEnemyRequest(string EnemyId);

public record DamagePlayerRequest(int Amount);

public record CameraStateResponse(
    double X, double Y, double Z,
    double Yaw, double Pitch, double Zoom);

public record ScreenPositionResponse(
    double ScreenX, double ScreenY,
    double NormX, double NormY,
    bool OnScreen,
    int ScreenWidth, int ScreenHeight);

public record PlayerScreenPositionResponse(
    double WorldX, double WorldY, double WorldZ,
    double ScreenX, double ScreenY,
    double NormX, double NormY,
    bool OnScreen,
    int ScreenWidth, int ScreenHeight);

public record TileInfoResponse(int TileX, int TileZ, string TileType, int TileTypeId);

public record ScreenshotResponse(string Path);

public record ModelEntityDto(string Name, double X, double Y, double Z, int MeshCount, int MaterialCount);

public record CameraDiagnosticsDto(
    PositionResponse Position,
    PositionResponse Forward,
    string Projection,
    double OrthoSize,
    double NearClip,
    double FarClip,
    string SlotId);

public record SceneDiagnosticsResponse(
    int TotalEntities,
    int ModelEntityCount,
    List<ModelEntityDto> ModelEntitiesSample,
    CameraDiagnosticsDto? Camera);

// ---- Phase E: Scenario commands ----

public record SpawnEnemyRequest
{
    public string? Id { get; init; }
    public double X { get; init; }
    public double Z { get; init; }
    public int? Hp { get; init; }
    public int? Endurance { get; init; }
    public int? Luck { get; init; }
    public int? WeaponDamage { get; init; }
    public float? WeaponAccuracy { get; init; }
}

public record SpawnEnemyResponse(bool Success, string Id, int Hp, int MaxHp);

public record ScenarioResetResponse(bool Success, int PlayerHp, int EnemyCount);

public record SetPlayerStatsRequest
{
    public int? Endurance { get; init; }
    public int? Luck { get; init; }
    public int? Strength { get; init; }
    public int? Hp { get; init; }
}

public record SetStatsResponse(bool Success, int Hp, int MaxHp);

public record SetPlayerWeaponRequest
{
    public int Damage { get; init; }
    public float Accuracy { get; init; }
    public float Range { get; init; } = 2f;
    public int ApCost { get; init; } = 3;
    public float CritMultiplier { get; init; } = 1.5f;
}

public record SetWeaponResponse(bool Success, int Damage, float Accuracy);

// ---- Phase D: Combat config & equip commands ----

public record WeaponConfigDto(int Damage, float Accuracy, float Range, float CritMultiplier, int ApCost);

public record CombatConfigResponse(WeaponConfigDto Player, WeaponConfigDto Enemy, float MeleeDistance);

public record EquipItemRequest
{
    public string ItemId { get; init; } = "";
}

public record EquipItemResponse(bool Success, string Slot, string ItemName);

// ---- Combat state commands ----

public record EnemyStateDto(
    string Id, int Hp, int MaxHp, int Ap, int MaxAp,
    bool IsAlive, double X, double Y, double Z);

public record CombatStateResponse(
    bool InCombat, int EnemyCount,
    List<EnemyStateDto> Enemies,
    int PlayerHp, int PlayerMaxHp,
    int PlayerAp, int PlayerMaxAp);

public record KillEnemyResponse(bool Killed, int RemainingAlive);

// ---- Phase B: Inventory / Equipment / HUD / Loot commands ----

public record InventoryItemDto(string Id, string Name, string Category, int Count, double Weight);

public record InventoryStateResponse(
    int ItemCount, double CurrentWeight, double MaxWeight,
    bool IsOverweight, List<InventoryItemDto> Items);

public record EquipmentSlotDto(string? Id, string? Name);

public record EquipmentStateResponse(Dictionary<string, EquipmentSlotDto?> Slots);

public record HudStateResponse(int Hp, int MaxHp, int Ap, int MaxAp, int Level, string GameState);

public record LootEntityDto(string Name, double X, double Y, double Z, int ItemCount);

public record LootEntitiesResponse(int Count, List<LootEntityDto> Entities);

public record InventoryOverlayResponse(bool Visible);

// ---- Phase C: Notification / GameOver / EnemyHpBars / DamagePlayer commands ----

public record NotificationMessageDto(string Text, double TimeRemaining);

public record NotificationFeedResponse(int Count, List<NotificationMessageDto> Messages);

public record GameOverStateResponse(bool Visible, string Title);

public record EnemyHpBarDto(string EnemyId, int Hp, int MaxHp);

public record EnemyHpBarsResponse(bool Visible, List<EnemyHpBarDto> Bars);

public record DamagePlayerResponse(int NewHp, int MaxHp, bool IsAlive);

// ---- M1 Phase 1: Menu / Save / Load commands ----

public record MenuStateResponse(string Screen, List<string> Buttons, bool Visible);

public record ClickMenuButtonRequest(string Screen, string Button);

public record ClickMenuButtonResponse(bool Success);

public record TriggerSaveResponse(bool Success, string Path);

public record TriggerLoadResponse(bool Success);

public record SaveExistsResponse(bool Exists);

public record CapsStateResponse(int Caps);

// ---- M1 Phase 2: NPC commands ----

public record NpcDto(string Id, string DisplayName, string Role, double X, double Y, double Z);

public record NpcListResponse(int Count, List<NpcDto> Npcs);

public record NpcInRangeResponse(bool InRange, string? NpcId, string? DisplayName, double Distance);

public record InteractWithNpcRequest(string NpcId);

public record InteractResponse(bool Success, string? NpcId, string? DialogueTreeId);

public record DialogueChoiceDto(string Text, bool Available);

public record DialogueStateResponse(
    bool Active, string? Speaker, string? Text, string? TreeId, string? NodeId,
    List<DialogueChoiceDto> Choices);

public record SelectDialogueChoiceRequest(int Index);

public record DialogueChoiceResponse(bool Success, bool DialogueEnded);

// ---- M1 Phase 2.5: Trade test helpers ----

public record GiveItemToPlayerRequest(string ItemId, int Count);

public record GiveItemToPlayerResponse(bool Success);

// ---- M1 Phase 2.6: Zone transitions ----

public record CurrentZoneResponse(string ZoneId, string ZoneName);
