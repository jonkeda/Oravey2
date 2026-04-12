namespace Oravey2.Core.Content;

// --- Items ---

internal sealed record ItemCatalogJson(ItemJson[]? Items);

internal sealed record ItemJson(
    string Id,
    string Name,
    string Description,
    string Category,
    float Weight,
    bool Stackable,
    int Value,
    int? MaxStack = null,
    string? Slot = null,
    Dictionary<string, string>? Effects = null,
    WeaponJson? Weapon = null,
    ArmorJson? Armor = null);

internal sealed record WeaponJson(
    int Damage,
    float Range,
    int ApCost,
    float Accuracy,
    string SkillType,
    float? CritMultiplier = null);

internal sealed record ArmorJson(
    int DamageReduction,
    Dictionary<string, float> CoverageZones);

// --- NPCs ---

internal sealed record NpcCatalogJson(NpcDataJson[]? Npcs);

public sealed record NpcDataJson(
    string Id,
    string DisplayName,
    string Role,
    string DialogueTreeId,
    PositionJson? Position = null,
    ColorJson? Color = null);

public sealed record PositionJson(float X, float Y, float Z);
public sealed record ColorJson(float R, float G, float B);

// --- Enemies ---

internal sealed record EnemyCatalogJson(EnemyJson[]? Enemies);

internal sealed record EnemyJson(
    string GroupId,
    float X,
    float Z,
    int Count,
    int Endurance,
    int Luck,
    int WeaponDamage,
    float WeaponAccuracy,
    string? Tag = null,
    string? RequiredQuestId = null);

// --- Dialogues ---

internal sealed record DialogueTreeJson(
    string Id,
    string StartNodeId,
    Dictionary<string, DialogueNodeJson> Nodes);

internal sealed record DialogueNodeJson(
    string Speaker,
    string Text,
    string? Portrait,
    DialogueChoiceJson[] Choices);

internal sealed record DialogueChoiceJson(
    string Text,
    string? NextNodeId,
    ConditionJson? Condition,
    ConsequenceJson[]? Consequences);

internal sealed record ConditionJson(
    string Type,
    // Flag conditions
    string? Flag = null,
    bool? Expected = null,
    // And conditions
    ConditionJson[]? Conditions = null,
    // Counter conditions
    string? Counter = null,
    int? Target = null,
    // Item conditions
    string? ItemId = null,
    int? Count = null,
    // Level conditions
    int? MinLevel = null,
    // Skill conditions
    string? Skill = null,
    int? MinValue = null);

internal sealed record ConsequenceJson(
    string Type,
    string? Flag = null,
    int? Amount = null,
    string? QuestId = null,
    string? ItemId = null,
    int? Price = null,
    int? Count = null);

// --- Quests ---

internal sealed record QuestCatalogJson(QuestJson[]? Quests);

internal sealed record QuestJson(
    string Id,
    string Title,
    string Description,
    string Type,
    string FirstStageId,
    Dictionary<string, QuestStageJson> Stages,
    int? XpReward = null);

internal sealed record QuestStageJson(
    string Description,
    QuestConditionJson[]? Conditions,
    QuestActionJson[]? OnCompleteActions,
    string? NextStageId,
    QuestConditionJson[]? FailConditions,
    QuestActionJson[]? OnFailActions);

internal sealed record QuestConditionJson(
    string Type,
    string? Flag = null,
    bool? Expected = null,
    string? Counter = null,
    int? Target = null,
    int? MinLevel = null,
    string? ItemId = null,
    int? Count = null,
    string? QuestId = null);

internal sealed record QuestActionJson(
    string Type,
    string? Flag = null,
    bool? Value = null,
    int? Amount = null,
    string? QuestId = null);

// --- Manifest ---

public sealed record ContentManifest(
    string Id,
    string Name,
    string Version,
    string? Description = null,
    string? DefaultScenario = null,
    string[]? Tags = null,
    string? Author = null,
    string? EngineVersion = null,
    string? Parent = null);
